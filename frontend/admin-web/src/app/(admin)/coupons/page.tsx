"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { Button, Card, Field, PageHeading, Select } from "@/components/ui";
import { describeError } from "@/lib/api";
import { getSessionClaims, subscribeToAuthChanges } from "@/lib/auth";
import { createCoupon, searchCoupons, setCouponActive, updateCoupon } from "@/lib/coupon-api";
import type { CouponAdminResponse, CouponCreateRequest, CouponUpdateRequest } from "@/lib/coupon-types";
import { canWriteModule } from "@/lib/permissions";
import type { AdminSessionClaims } from "@/lib/types";
import { CouponForm } from "./_components/CouponForm";
import { CouponsTable } from "./_components/CouponsTable";
import { CouponsTabs } from "./_components/CouponsTabs";

const PAGE_SIZE = 20;

const STATUS_FILTER_OPTIONS = [
  { value: "", label: "All statuses" },
  { value: "true", label: "Active" },
  { value: "false", label: "Suspended" },
] as const;

/**
 * Coupon management (SRS 12.12.1, task 119): search/filter, create, edit,
 * and suspend/reactivate. Gated behind the "coupons" permission module -
 * every mutating control checks `canWriteModule` the same way every other
 * admin screen does (see ServiceabilityPage's doc comment); the route itself
 * is only reachable once AdminSidebar already filtered it in by "coupons.read".
 */
export default function CouponsPage() {
  const [claims, setClaims] = useState<AdminSessionClaims | null>(null);
  const [codeFilter, setCodeFilter] = useState("");
  const [statusFilter, setStatusFilter] = useState("");
  const [page, setPage] = useState(1);
  const [editingCoupon, setEditingCoupon] = useState<CouponAdminResponse | null>(null);
  const [formError, setFormError] = useState<string | null>(null);

  useEffect(() => {
    const sync = () => setClaims(getSessionClaims());
    sync();
    return subscribeToAuthChanges(sync);
  }, []);

  const canWrite = canWriteModule(claims, "coupons");
  const queryClient = useQueryClient();

  const isActive = statusFilter === "" ? undefined : statusFilter === "true";

  const couponsQuery = useQuery({
    queryKey: ["coupons", "search", codeFilter, statusFilter, page] as const,
    queryFn: () => searchCoupons({ code: codeFilter || undefined, isActive, page, pageSize: PAGE_SIZE }),
  });

  const invalidateCoupons = () => queryClient.invalidateQueries({ queryKey: ["coupons", "search"] });

  const createMutation = useMutation({
    mutationFn: (request: CouponCreateRequest) => createCoupon(request),
    onSuccess: () => {
      invalidateCoupons();
      setFormError(null);
    },
    onError: (error) => setFormError(describeError(error)),
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, request }: { id: string; request: CouponUpdateRequest }) => updateCoupon(id, request),
    onSuccess: () => {
      invalidateCoupons();
      setFormError(null);
      setEditingCoupon(null);
    },
    onError: (error) => setFormError(describeError(error)),
  });

  const toggleMutation = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => setCouponActive(id, isActive),
    onSuccess: invalidateCoupons,
  });

  const handleSubmit = (request: CouponCreateRequest | CouponUpdateRequest) => {
    if (editingCoupon) {
      updateMutation.mutate({ id: editingCoupon.id, request: request as CouponUpdateRequest });
    } else {
      createMutation.mutate(request as CouponCreateRequest);
    }
  };

  const totalPages = couponsQuery.data ? Math.max(1, Math.ceil(couponsQuery.data.totalCount / PAGE_SIZE)) : 1;

  return (
    <div className="mx-auto w-full max-w-6xl">
      <PageHeading
        title="Coupons & Campaigns"
        subtitle="Discount codes with every rule dimension - type, value, caps, limits, validity, and applicability (SRS 12.12.1)."
      />

      <CouponsTabs />

      <div className="flex flex-col gap-6">
        {canWrite ? (
          <Card
            title={editingCoupon ? `Edit coupon: ${editingCoupon.code}` : "Create a coupon"}
            description={
              editingCoupon
                ? "The coupon code cannot be changed once created."
                : "Every field below maps to one of SRS 12.12.1's coupon creation fields."
            }
          >
            <CouponForm
              coupon={editingCoupon}
              isSubmitting={createMutation.isPending || updateMutation.isPending}
              submitError={formError}
              onSubmit={handleSubmit}
              onCancel={
                editingCoupon
                  ? () => {
                      setEditingCoupon(null);
                      setFormError(null);
                    }
                  : undefined
              }
            />
          </Card>
        ) : null}

        <Card title="Coupons" description="Search and manage every coupon (SRS 12.12.1).">
          <div className="mb-4 flex flex-wrap items-end gap-3">
            <div className="w-56">
              <Field
                label="Code"
                placeholder="Search by code…"
                value={codeFilter}
                onChange={(event) => {
                  setPage(1);
                  setCodeFilter(event.target.value);
                }}
              />
            </div>
            <div className="w-44">
              <Select
                label="Status"
                options={STATUS_FILTER_OPTIONS}
                value={statusFilter}
                onChange={(event) => {
                  setPage(1);
                  setStatusFilter(event.target.value);
                }}
              />
            </div>
          </div>

          <CouponsTable
            coupons={couponsQuery.data?.items}
            isLoading={couponsQuery.isLoading}
            errorMessage={couponsQuery.error ? describeError(couponsQuery.error) : null}
            canWrite={canWrite}
            onEdit={(coupon) => {
              setEditingCoupon(coupon);
              setFormError(null);
            }}
            onToggleActive={(coupon) => toggleMutation.mutate({ id: coupon.id, isActive: !coupon.isActive })}
            togglingId={toggleMutation.isPending ? toggleMutation.variables?.id : undefined}
          />

          {couponsQuery.data && couponsQuery.data.totalCount > PAGE_SIZE ? (
            <div className="mt-4 flex items-center justify-between text-sm text-neutral-600 dark:text-neutral-400">
              <span>
                Page {page} of {totalPages} ({couponsQuery.data.totalCount} total)
              </span>
              <div className="flex gap-2">
                <Button type="button" variant="secondary" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
                  Previous
                </Button>
                <Button
                  type="button"
                  variant="secondary"
                  disabled={page >= totalPages}
                  onClick={() => setPage((p) => p + 1)}
                >
                  Next
                </Button>
              </div>
            </div>
          ) : null}
        </Card>
      </div>
    </div>
  );
}
