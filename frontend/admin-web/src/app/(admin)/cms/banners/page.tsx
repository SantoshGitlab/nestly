"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { Button, Card, PageHeading, Select } from "@/components/ui";
import { describeError } from "@/lib/api";
import { getSessionClaims, subscribeToAuthChanges } from "@/lib/auth";
import { createBanner, searchBanners, setBannerPublished, updateBanner } from "@/lib/cms-api";
import { CmsContentStatus, type BannerCreateRequest, type BannerResponse, type BannerUpdateRequest } from "@/lib/cms-types";
import { canWriteModule } from "@/lib/permissions";
import type { AdminSessionClaims } from "@/lib/types";
import { BannerForm } from "../_components/BannerForm";
import { BannersTable } from "../_components/BannersTable";
import { PLACEMENT_OPTIONS } from "../_components/cmsDisplay";
import { CmsTabs } from "../_components/CmsTabs";

const PAGE_SIZE = 20;

const STATUS_FILTER_OPTIONS = [
  { value: "", label: "All statuses" },
  { value: String(CmsContentStatus.Draft), label: "Draft" },
  { value: String(CmsContentStatus.Published), label: "Published" },
] as const;

const PLACEMENT_FILTER_OPTIONS = [{ value: "", label: "All placements" }, ...PLACEMENT_OPTIONS] as const;

/**
 * Promotional banner management (SRS 12.16.1, task 125a): search/filter by
 * placement and status, create, edit, and publish/unpublish. Gated behind
 * the "cms" permission module - every mutating control checks
 * `canWriteModule` the same way CmsPagesPage does; the route itself is only
 * reachable once AdminSidebar already filtered it in by "cms.read".
 */
export default function CmsBannersPage() {
  const [claims, setClaims] = useState<AdminSessionClaims | null>(null);
  const [placementFilter, setPlacementFilter] = useState("");
  const [statusFilter, setStatusFilter] = useState("");
  const [page, setPage] = useState(1);
  const [editingBanner, setEditingBanner] = useState<BannerResponse | null>(null);
  const [formError, setFormError] = useState<string | null>(null);

  useEffect(() => {
    const sync = () => setClaims(getSessionClaims());
    sync();
    return subscribeToAuthChanges(sync);
  }, []);

  const canWrite = canWriteModule(claims, "cms");
  const queryClient = useQueryClient();

  const placement = placementFilter === "" ? undefined : (Number(placementFilter) as BannerResponse["placement"]);
  const status = statusFilter === "" ? undefined : (Number(statusFilter) as CmsContentStatus);

  const bannersQuery = useQuery({
    queryKey: ["cms", "banners", "search", placementFilter, statusFilter, page] as const,
    queryFn: () => searchBanners({ placement, status, page, pageSize: PAGE_SIZE }),
  });

  const invalidateBanners = () => queryClient.invalidateQueries({ queryKey: ["cms", "banners", "search"] });

  const createMutation = useMutation({
    mutationFn: (request: BannerCreateRequest) => createBanner(request),
    onSuccess: () => {
      invalidateBanners();
      setFormError(null);
    },
    onError: (error) => setFormError(describeError(error)),
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, request }: { id: string; request: BannerUpdateRequest }) => updateBanner(id, request),
    onSuccess: () => {
      invalidateBanners();
      setFormError(null);
      setEditingBanner(null);
    },
    onError: (error) => setFormError(describeError(error)),
  });

  const toggleMutation = useMutation({
    mutationFn: ({ id, published }: { id: string; published: boolean }) => setBannerPublished(id, published),
    onSuccess: invalidateBanners,
  });

  const handleSubmit = (request: BannerCreateRequest | BannerUpdateRequest) => {
    if (editingBanner) {
      updateMutation.mutate({ id: editingBanner.id, request: request as BannerUpdateRequest });
    } else {
      createMutation.mutate(request as BannerCreateRequest);
    }
  };

  const totalPages = bannersQuery.data ? Math.max(1, Math.ceil(bannersQuery.data.totalCount / PAGE_SIZE)) : 1;

  return (
    <div className="mx-auto w-full max-w-6xl">
      <PageHeading
        title="CMS & Content"
        subtitle="Static pages, banners, and site-level FAQs with draft/publish, scheduling, and placement (SRS 12.16)."
      />

      <CmsTabs />

      <div className="flex flex-col gap-6">
        {canWrite ? (
          <Card
            title={editingBanner ? `Edit banner: ${editingBanner.title}` : "Create a banner"}
            description="Image, link, placement, sort order, and an optional publish window (SRS 12.16.1/12.16.2)."
          >
            <BannerForm
              banner={editingBanner}
              isSubmitting={createMutation.isPending || updateMutation.isPending}
              submitError={formError}
              onSubmit={handleSubmit}
              onCancel={
                editingBanner
                  ? () => {
                      setEditingBanner(null);
                      setFormError(null);
                    }
                  : undefined
              }
            />
          </Card>
        ) : null}

        <Card title="Banners" description="Search and manage every promotional banner (SRS 12.16.1).">
          <div className="mb-4 flex flex-wrap items-end gap-3">
            <div className="w-52">
              <Select
                label="Placement"
                options={PLACEMENT_FILTER_OPTIONS}
                value={placementFilter}
                onChange={(event) => {
                  setPage(1);
                  setPlacementFilter(event.target.value);
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

          <BannersTable
            banners={bannersQuery.data?.items}
            isLoading={bannersQuery.isLoading}
            errorMessage={bannersQuery.error ? describeError(bannersQuery.error) : null}
            canWrite={canWrite}
            onEdit={(banner) => {
              setEditingBanner(banner);
              setFormError(null);
            }}
            onTogglePublished={(banner) =>
              toggleMutation.mutate({ id: banner.id, published: banner.status !== CmsContentStatus.Published })
            }
            togglingId={toggleMutation.isPending ? toggleMutation.variables?.id : undefined}
          />

          {bannersQuery.data && bannersQuery.data.totalCount > PAGE_SIZE ? (
            <div className="mt-4 flex items-center justify-between text-sm text-neutral-600 dark:text-neutral-400">
              <span>
                Page {page} of {totalPages} ({bannersQuery.data.totalCount} total)
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
