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
import { ProviderStatusBadge } from "@/components/status-badges";
import { describeError } from "@/lib/api";
import { createProvider, searchProviders } from "@/lib/providers-api";
import { ProviderOnboardingStatus, ProviderStatus } from "@/lib/providers-types";
import type { CreateProviderRequest, ProviderSummary } from "@/lib/providers-types";
import { useAdminClaims } from "@/lib/use-admin-claims";

const PAGE_SIZE = 20;

const STATUS_OPTIONS: { value: string; label: string }[] = [
  { value: "", label: "Any status" },
  { value: String(ProviderStatus.PendingVerification), label: "Pending verification" },
  { value: String(ProviderStatus.Active), label: "Active" },
  { value: String(ProviderStatus.Suspended), label: "Suspended" },
  { value: String(ProviderStatus.Deactivated), label: "Deactivated" },
];

const ONBOARDING_LABELS: Record<ProviderOnboardingStatus, string> = {
  [ProviderOnboardingStatus.Registered]: "Registered",
  [ProviderOnboardingStatus.ProfileCompleted]: "Profile completed",
  [ProviderOnboardingStatus.KycSubmitted]: "KYC submitted",
  [ProviderOnboardingStatus.KycVerified]: "KYC verified",
  [ProviderOnboardingStatus.Completed]: "Onboarding complete",
};

function statusLabel(status: ProviderStatus): string {
  return STATUS_OPTIONS.find((o) => o.value === String(status))?.label ?? "Unknown";
}

interface FilterFormState {
  name: string;
  phone: string;
  status: string;
}

const EMPTY_FILTERS: FilterFormState = { name: "", phone: "", status: "" };
const EMPTY_CREATE: CreateProviderRequest = { legalName: "", displayName: "", phone: "", email: "" };

/**
 * Admin provider directory: search/list plus admin-created provider records
 * (PROVIDER.md API surface "Provider CRUD", task 150a). Mirrors
 * customers/page.tsx's shape. Create is only shown to admins holding
 * "provider.write" - the API enforces this server-side regardless.
 *
 * Built on the task 221 pattern. Creation moved from an inline panel that
 * pushed the whole list down into a `Modal`, which restores focus to the "New
 * provider" button on close. Columns are NOT sortable: the list is paged
 * server-side and the endpoint takes no sort parameter.
 */
export default function ProvidersPage() {
  const claims = useAdminClaims();
  const canWrite = claims?.permissions.includes("provider.write") ?? false;
  const router = useRouter();
  const queryClient = useQueryClient();

  const [filters, setFilters] = useState<FilterFormState>(EMPTY_FILTERS);
  const [appliedFilters, setAppliedFilters] = useState<FilterFormState>(EMPTY_FILTERS);
  const [page, setPage] = useState(1);
  const [showCreateForm, setShowCreateForm] = useState(false);
  const [createForm, setCreateForm] = useState<CreateProviderRequest>(EMPTY_CREATE);
  const [createError, setCreateError] = useState<string | null>(null);

  const query = useQuery({
    queryKey: ["admin-providers", appliedFilters, page],
    queryFn: () =>
      searchProviders({
        name: appliedFilters.name || undefined,
        phone: appliedFilters.phone || undefined,
        status: appliedFilters.status === "" ? undefined : (Number(appliedFilters.status) as ProviderStatus),
        page,
        pageSize: PAGE_SIZE,
      }),
    placeholderData: keepPreviousData,
  });

  const createMutation = useMutation({
    mutationFn: () =>
      createProvider({
        legalName: createForm.legalName,
        displayName: createForm.displayName,
        phone: createForm.phone,
        email: createForm.email || undefined,
      }),
    onSuccess: (created) => {
      setCreateError(null);
      setCreateForm(EMPTY_CREATE);
      setShowCreateForm(false);
      queryClient.invalidateQueries({ queryKey: ["admin-providers"] });
      router.push(`/providers/${created.id}`);
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

  /**
   * Closing without submitting (Cancel, the Modal's own X/Escape/backdrop) has
   * to clear `createForm`/`createError` here, not only in the mutation's
   * `onSuccess` - otherwise the next "New provider" open resumes mid-edit on
   * whatever was typed (or the last error) last time, since `useState`'s
   * initial value only applies once, on mount.
   */
  const closeCreateForm = () => {
    setShowCreateForm(false);
    setCreateForm(EMPTY_CREATE);
    setCreateError(null);
  };

  const createDisabled =
    !createForm.legalName.trim() || !createForm.displayName.trim() || !createForm.phone.trim();

  const columns: DataTableColumn<ProviderSummary>[] = [
    {
      key: "name",
      header: "Name",
      cell: (provider) => (
        <Link
          href={`/providers/${provider.id}`}
          className="font-medium text-fg underline-offset-4 hover:text-brand-600 hover:underline dark:hover:text-brand-400"
        >
          {provider.displayName}
        </Link>
      ),
    },
    { key: "phone", header: "Phone", cell: (provider) => <span className="nums">{provider.phone}</span> },
    { key: "email", header: "Email", cell: (provider) => provider.email ?? "—" },
    {
      key: "status",
      header: "Status",
      cell: (provider) => <ProviderStatusBadge status={provider.status} label={statusLabel(provider.status)} />,
    },
    {
      key: "onboarding",
      header: "Onboarding",
      cell: (provider) => (
        <Badge
          tone={provider.onboardingStatus === ProviderOnboardingStatus.Completed ? "success" : "neutral"}
        >
          {ONBOARDING_LABELS[provider.onboardingStatus]}
        </Badge>
      ),
    },
    {
      key: "created",
      header: "Created",
      cell: (provider) => <span className="nums">{formatDate(provider.createdAt)}</span>,
    },
  ];

  return (
    <div className="mx-auto w-full max-w-7xl">
      <PageHeading
        title="Providers"
        subtitle="Manage service providers: profile, KYC approval and performance (PROVIDER.md)."
        actions={
          canWrite ? <Button onClick={() => setShowCreateForm(true)}>New provider</Button> : undefined
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
          rowKey={(provider) => provider.id}
          isLoading={query.isPending}
          isFetching={query.isFetching}
          error={query.error}
          onRetry={() => query.refetch()}
          skeletonRows={8}
          minWidth="920px"
          caption="Providers matching the current filters"
          emptyTitle="No providers match these filters"
          emptyDescription="Try a partial name or phone number, or clear the filters to see every provider."
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
                itemLabel="provider"
              />
            ) : null
          }
        />
      </div>

      <Modal
        open={showCreateForm}
        onClose={closeCreateForm}
        title="Create provider"
        description="Provider type is always Individual in v1 (PROVIDER.md OPEN DECISIONS #2)."
        footer={
          <>
            <Button variant="secondary" onClick={closeCreateForm} disabled={createMutation.isPending}>
              Cancel
            </Button>
            <Button
              disabled={createDisabled}
              loading={createMutation.isPending}
              onClick={() => createMutation.mutate()}
            >
              Create provider
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
