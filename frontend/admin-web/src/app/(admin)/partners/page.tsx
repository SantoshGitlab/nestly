"use client";

import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { Alert, Badge, Button, Field, Modal, PageHeading, Select } from "@/components/ui";
import {
  DataTable,
  FilterBar,
  FormGrid,
  Pagination,
  countActiveFilters,
  formatDate,
} from "@/components/data-table";
import type { DataTableColumn } from "@/components/data-table";
import { PartnerStatusBadge } from "@/components/status-badges";
import { describeError } from "@/lib/api";
import { createPartner, searchPartners } from "@/lib/partners-api";
import { PartnerOnboardingStatus, PartnerStatus } from "@/lib/partners-types";
import type { CreatePartnerRequest, PartnerSummary } from "@/lib/partners-types";
import { useAdminClaims } from "@/lib/use-admin-claims";

const PAGE_SIZE = 20;

const STATUS_OPTIONS: { value: string; label: string }[] = [
  { value: "", label: "Any status" },
  { value: String(PartnerStatus.PendingVerification), label: "Pending verification" },
  { value: String(PartnerStatus.Active), label: "Active" },
  { value: String(PartnerStatus.Suspended), label: "Suspended" },
  { value: String(PartnerStatus.Deactivated), label: "Deactivated" },
];

const ONBOARDING_LABELS: Record<PartnerOnboardingStatus, string> = {
  [PartnerOnboardingStatus.Registered]: "Registered",
  [PartnerOnboardingStatus.ProfileCompleted]: "Profile completed",
  [PartnerOnboardingStatus.KycSubmitted]: "KYC submitted",
  [PartnerOnboardingStatus.KycVerified]: "KYC verified",
  [PartnerOnboardingStatus.Completed]: "Onboarding complete",
};

function statusLabel(status: PartnerStatus): string {
  return STATUS_OPTIONS.find((o) => o.value === String(status))?.label ?? "Unknown";
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
 *
 * Built on the task 221 pattern. Creation moved from an inline panel that
 * pushed the whole list down into a `Modal`, which restores focus to the "New
 * partner" button on close. Columns are NOT sortable: the list is paged
 * server-side and the endpoint takes no sort parameter.
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
    placeholderData: keepPreviousData,
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

  const onSubmit = () => {
    setPage(1);
    setAppliedFilters(filters);
  };

  const onClear = () => {
    setFilters(EMPTY_FILTERS);
    setAppliedFilters(EMPTY_FILTERS);
    setPage(1);
  };

  const createDisabled =
    !createForm.legalName.trim() || !createForm.displayName.trim() || !createForm.phone.trim();

  const columns: DataTableColumn<PartnerSummary>[] = [
    {
      key: "name",
      header: "Name",
      cell: (partner) => (
        <Link
          href={`/partners/${partner.id}`}
          className="font-medium text-fg underline-offset-4 hover:text-brand-600 hover:underline dark:hover:text-brand-400"
        >
          {partner.displayName}
        </Link>
      ),
    },
    { key: "phone", header: "Phone", cell: (partner) => <span className="nums">{partner.phone}</span> },
    { key: "email", header: "Email", cell: (partner) => partner.email ?? "—" },
    {
      key: "status",
      header: "Status",
      cell: (partner) => <PartnerStatusBadge status={partner.status} label={statusLabel(partner.status)} />,
    },
    {
      key: "onboarding",
      header: "Onboarding",
      cell: (partner) => (
        <Badge
          tone={partner.onboardingStatus === PartnerOnboardingStatus.Completed ? "success" : "neutral"}
        >
          {ONBOARDING_LABELS[partner.onboardingStatus]}
        </Badge>
      ),
    },
    {
      key: "created",
      header: "Created",
      cell: (partner) => <span className="nums">{formatDate(partner.createdAt)}</span>,
    },
  ];

  return (
    <div className="mx-auto w-full max-w-7xl">
      <PageHeading
        title="Partners"
        subtitle="Manage service partners: profile, KYC approval and performance (PARTNER.md)."
        actions={
          canWrite ? <Button onClick={() => setShowCreateForm(true)}>New partner</Button> : undefined
        }
      />

      <FilterBar
        onSubmit={onSubmit}
        onClear={onClear}
        activeCount={countActiveFilters(appliedFilters)}
        busy={query.isFetching}
        columns={3}
      >
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
          options={STATUS_OPTIONS}
        />
      </FilterBar>

      <div className="mt-6">
        <DataTable
          title="Results"
          columns={columns}
          rows={query.data?.items}
          rowKey={(partner) => partner.id}
          isLoading={query.isPending}
          isFetching={query.isFetching}
          error={query.error}
          onRetry={() => query.refetch()}
          skeletonRows={8}
          minWidth="920px"
          caption="Partners matching the current filters"
          emptyTitle="No partners match these filters"
          emptyDescription="Try a partial name or phone number, or clear the filters to see every partner."
          emptyAction={
            <Button variant="secondary" onClick={onClear}>
              Clear filters
            </Button>
          }
          footer={
            query.data ? (
              <Pagination
                page={page}
                pageSize={PAGE_SIZE}
                totalCount={query.data.totalCount}
                onPageChange={setPage}
                busy={query.isFetching}
                itemLabel="partner"
              />
            ) : null
          }
        />
      </div>

      <Modal
        open={showCreateForm}
        onClose={() => setShowCreateForm(false)}
        title="Create partner"
        description="Partner type is always Individual in v1 (PARTNER.md OPEN DECISIONS #2)."
        footer={
          <>
            <Button variant="secondary" onClick={() => setShowCreateForm(false)} disabled={createMutation.isPending}>
              Cancel
            </Button>
            <Button
              disabled={createDisabled}
              loading={createMutation.isPending}
              onClick={() => createMutation.mutate()}
            >
              Create partner
            </Button>
          </>
        }
      >
        <div className="flex flex-col gap-4">
          {createError ? <Alert>{createError}</Alert> : null}
          <FormGrid>
            <Field
              label="Legal name"
              required
              value={createForm.legalName}
              onChange={(e) => setCreateForm((f) => ({ ...f, legalName: e.target.value }))}
            />
            <Field
              label="Display name"
              required
              hint="Shown to customers."
              value={createForm.displayName}
              onChange={(e) => setCreateForm((f) => ({ ...f, displayName: e.target.value }))}
            />
            <Field
              label="Phone"
              required
              inputMode="numeric"
              value={createForm.phone}
              onChange={(e) => setCreateForm((f) => ({ ...f, phone: e.target.value }))}
            />
            <Field
              label="Email"
              type="email"
              hint="Optional."
              value={createForm.email ?? ""}
              onChange={(e) => setCreateForm((f) => ({ ...f, email: e.target.value }))}
            />
          </FormGrid>
        </div>
      </Modal>
    </div>
  );
}
