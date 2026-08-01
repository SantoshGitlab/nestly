"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import { Alert, Button, Card, Field, PageHeading, Select } from "@/components/ui";
import { describeError } from "@/lib/api";
import { getSessionClaims, subscribeToAuthChanges } from "@/lib/auth";
import { createPartner, searchPartners } from "@/lib/partners-api";
import { PartnerOnboardingStatus, PartnerStatus } from "@/lib/partners-types";
import type { CreatePartnerRequest } from "@/lib/partners-types";
import type { AdminSessionClaims } from "@/lib/types";

const PAGE_SIZE = 20;

function useAdminClaims(): AdminSessionClaims | null {
  const [claims, setClaims] = useState<AdminSessionClaims | null>(null);
  useEffect(() => {
    const sync = () => setClaims(getSessionClaims());
    sync();
    return subscribeToAuthChanges(sync);
  }, []);
  return claims;
}

const STATUS_OPTIONS: { value: PartnerStatus | ""; label: string }[] = [
  { value: "", label: "Any status" },
  { value: PartnerStatus.PendingVerification, label: "Pending verification" },
  { value: PartnerStatus.Active, label: "Active" },
  { value: PartnerStatus.Suspended, label: "Suspended" },
  { value: PartnerStatus.Deactivated, label: "Deactivated" },
];

const ONBOARDING_LABELS: Record<PartnerOnboardingStatus, string> = {
  [PartnerOnboardingStatus.Registered]: "Registered",
  [PartnerOnboardingStatus.ProfileCompleted]: "Profile completed",
  [PartnerOnboardingStatus.KycSubmitted]: "KYC submitted",
  [PartnerOnboardingStatus.KycVerified]: "KYC verified",
  [PartnerOnboardingStatus.Completed]: "Onboarding complete",
};

function statusLabel(status: PartnerStatus): string {
  return STATUS_OPTIONS.find((o) => o.value === status)?.label ?? "Unknown";
}

interface FilterFormState {
  name: string;
  phone: string;
  status: string;
}

const EMPTY_FILTERS: FilterFormState = { name: "", phone: "", status: "" };
const EMPTY_CREATE: CreatePartnerRequest = { legalName: "", displayName: "", phone: "", email: "" };

/**
 * Admin partner directory: search/list plus admin-created partner records
 * (PARTNER.md API surface "Partner CRUD", task 150a). Mirrors
 * customers/page.tsx's shape. Create is only shown to admins holding
 * "partner.write" - the API enforces this server-side regardless.
 */
export default function PartnersPage() {
  const claims = useAdminClaims();
  const canWrite = claims?.permissions.includes("partner.write") ?? false;
  const router = useRouter();
  const queryClient = useQueryClient();

  const [filters, setFilters] = useState<FilterFormState>(EMPTY_FILTERS);
  const [appliedFilters, setAppliedFilters] = useState<FilterFormState>(EMPTY_FILTERS);
  const [page, setPage] = useState(1);
  const [showCreateForm, setShowCreateForm] = useState(false);
  const [createForm, setCreateForm] = useState<CreatePartnerRequest>(EMPTY_CREATE);
  const [createError, setCreateError] = useState<string | null>(null);

  const query = useQuery({
    queryKey: ["admin-partners", appliedFilters, page],
    queryFn: () =>
      searchPartners({
        name: appliedFilters.name || undefined,
        phone: appliedFilters.phone || undefined,
        status: appliedFilters.status === "" ? undefined : (Number(appliedFilters.status) as PartnerStatus),
        page,
        pageSize: PAGE_SIZE,
      }),
  });

  const createMutation = useMutation({
    mutationFn: () =>
      createPartner({
        legalName: createForm.legalName,
        displayName: createForm.displayName,
        phone: createForm.phone,
        email: createForm.email || undefined,
      }),
    onSuccess: (created) => {
      setCreateError(null);
      setCreateForm(EMPTY_CREATE);
      setShowCreateForm(false);
      queryClient.invalidateQueries({ queryKey: ["admin-partners"] });
      router.push(`/partners/${created.id}`);
    },
    onError: (err) => setCreateError(describeError(err)),
  });

  const onSubmit = (e: FormEvent) => {
    e.preventDefault();
    setPage(1);
    setAppliedFilters(filters);
  };

  const onClear = () => {
    setFilters(EMPTY_FILTERS);
    setAppliedFilters(EMPTY_FILTERS);
    setPage(1);
  };

  const totalPages = query.data ? Math.max(1, Math.ceil(query.data.totalCount / PAGE_SIZE)) : 1;

  return (
    <div className="mx-auto flex w-full max-w-5xl flex-col gap-6">
      <div className="flex items-center justify-between">
        <PageHeading title="Partners" subtitle="Manage service partners: profile, KYC approval and performance (PARTNER.md)." />
        {canWrite ? (
          <Button variant="secondary" onClick={() => setShowCreateForm((v) => !v)}>
            {showCreateForm ? "Cancel" : "New partner"}
          </Button>
        ) : null}
      </div>

      {showCreateForm ? (
        <Card title="Create partner" description="PartnerType is always Individual in v1 (OPEN DECISIONS #2).">
          {createError ? <Alert>{createError}</Alert> : null}
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <Field
              label="Legal name"
              value={createForm.legalName}
              onChange={(e) => setCreateForm((f) => ({ ...f, legalName: e.target.value }))}
            />
            <Field
              label="Display name"
              value={createForm.displayName}
              onChange={(e) => setCreateForm((f) => ({ ...f, displayName: e.target.value }))}
            />
            <Field
              label="Phone"
              value={createForm.phone}
              onChange={(e) => setCreateForm((f) => ({ ...f, phone: e.target.value }))}
            />
            <Field
              label="Email (optional)"
              type="email"
              value={createForm.email ?? ""}
              onChange={(e) => setCreateForm((f) => ({ ...f, email: e.target.value }))}
            />
          </div>
          <div className="mt-4">
            <Button
              disabled={!createForm.legalName.trim() || !createForm.displayName.trim() || !createForm.phone.trim() || createMutation.isPending}
              onClick={() => createMutation.mutate()}
            >
              {createMutation.isPending ? "Creating…" : "Create partner"}
            </Button>
          </div>
        </Card>
      ) : null}

      <Card title="Filters">
        <form onSubmit={onSubmit} className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          <Field
            label="Name"
            value={filters.name}
            onChange={(e) => setFilters((f) => ({ ...f, name: e.target.value }))}
          />
          <Field
            label="Phone"
            value={filters.phone}
            onChange={(e) => setFilters((f) => ({ ...f, phone: e.target.value }))}
          />
          <Select
            label="Status"
            value={filters.status}
            onChange={(e) => setFilters((f) => ({ ...f, status: e.target.value }))}
            options={STATUS_OPTIONS.map((o) => ({ value: String(o.value), label: o.label }))}
          />
          <div className="flex items-end gap-2">
            <Button type="submit">Search</Button>
            <Button type="button" variant="secondary" onClick={onClear}>
              Clear
            </Button>
          </div>
        </form>
      </Card>

      <div>
        {query.isPending ? (
          <p className="text-sm text-neutral-500">Loading partners…</p>
        ) : query.isError ? (
          <Alert>{describeError(query.error)}</Alert>
        ) : query.data.items.length === 0 ? (
          <Card title="No partners match these filters">
            <p className="text-sm text-neutral-600 dark:text-neutral-400">Try broadening your search criteria.</p>
          </Card>
        ) : (
          <>
            <div className="overflow-x-auto rounded-xl border border-black/10 dark:border-white/15">
              <table className="w-full min-w-[720px] text-left text-sm">
                <thead className="border-b border-black/10 bg-black/5 dark:border-white/15 dark:bg-white/5">
                  <tr>
                    <th className="px-4 py-3 font-medium">Name</th>
                    <th className="px-4 py-3 font-medium">Phone</th>
                    <th className="px-4 py-3 font-medium">Email</th>
                    <th className="px-4 py-3 font-medium">Status</th>
                    <th className="px-4 py-3 font-medium">Onboarding</th>
                    <th className="px-4 py-3 font-medium">Created</th>
                  </tr>
                </thead>
                <tbody>
                  {query.data.items.map((partner) => (
                    <tr
                      key={partner.id}
                      className="border-b border-black/5 last:border-0 hover:bg-black/[.03] dark:border-white/10 dark:hover:bg-white/[.05]"
                    >
                      <td className="px-4 py-3">
                        <Link href={`/partners/${partner.id}`} className="font-medium underline-offset-2 hover:underline">
                          {partner.displayName}
                        </Link>
                      </td>
                      <td className="px-4 py-3">{partner.phone}</td>
                      <td className="px-4 py-3">{partner.email ?? "—"}</td>
                      <td className="px-4 py-3">{statusLabel(partner.status)}</td>
                      <td className="px-4 py-3">{ONBOARDING_LABELS[partner.onboardingStatus]}</td>
                      <td className="px-4 py-3">{new Date(partner.createdAt).toLocaleDateString()}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <div className="mt-4 flex items-center justify-between text-sm">
              <span className="text-neutral-600 dark:text-neutral-400">
                Page {page} of {totalPages} · {query.data.totalCount} partner{query.data.totalCount === 1 ? "" : "s"}
              </span>
              <div className="flex gap-2">
                <Button variant="secondary" disabled={page <= 1} onClick={() => setPage((p) => Math.max(1, p - 1))}>
                  Previous
                </Button>
                <Button
                  variant="secondary"
                  disabled={page >= totalPages}
                  onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                >
                  Next
                </Button>
              </div>
            </div>
          </>
        )}
      </div>
    </div>
  );
}
