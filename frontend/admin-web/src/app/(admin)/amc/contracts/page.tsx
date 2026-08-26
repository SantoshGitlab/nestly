"use client";

import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { BookingsTabs } from "@/components/BookingsTabs";
import { Badge, Field, PageHeading, Select } from "@/components/ui";
import type { BadgeTone } from "@/components/ui";
import { DataTable, FilterBar, Pagination, countActiveFilters, formatDate } from "@/components/data-table";
import type { DataTableColumn } from "@/components/data-table";
import { AmcContractDetailModal } from "../_components/AmcContractDetailModal";
import {
  CONTRACT_STATUS_LABELS,
  CustomerAmcContractStatus,
  getAmcContract,
  searchAmcContracts,
} from "../_lib/amc-api";
import type { AmcContractAdminListItemResponse } from "../_lib/amc-api";

const PAGE_SIZE = 20;

const STATUS_OPTIONS = [
  { value: "", label: "Any status" },
  { value: String(CustomerAmcContractStatus.Active), label: "Active" },
  { value: String(CustomerAmcContractStatus.Exhausted), label: "Exhausted" },
  { value: String(CustomerAmcContractStatus.Expired), label: "Expired" },
  { value: String(CustomerAmcContractStatus.Cancelled), label: "Cancelled" },
];

const STATUS_TONES: Record<CustomerAmcContractStatus, BadgeTone> = {
  [CustomerAmcContractStatus.Active]: "success",
  [CustomerAmcContractStatus.Exhausted]: "warning",
  [CustomerAmcContractStatus.Expired]: "neutral",
  [CustomerAmcContractStatus.Cancelled]: "neutral",
};

interface FilterFormState {
  status: string;
  customerSearch: string;
}

const EMPTY_FILTERS: FilterFormState = { status: "", customerSearch: "" };

/**
 * Admin visibility into AMC contracts (docs/AMC.md, Phase 20): filter by
 * status, search by customer, and open a read-only detail dialog per row.
 * Read-only end to end - AmcContractsController exposes no mutating action,
 * the same reasoning `RecurringPlansPage`'s doc comment gives for recurring
 * plans (an admin who needs to act on the booking a redemption produced does
 * so from the "All bookings" tab, which already audits it).
 */
export default function AmcContractsPage() {
  const [filters, setFilters] = useState<FilterFormState>(EMPTY_FILTERS);
  const [appliedFilters, setAppliedFilters] = useState<FilterFormState>(EMPTY_FILTERS);
  const [page, setPage] = useState(1);
  const [openContractId, setOpenContractId] = useState<string | null>(null);

  const listQuery = useQuery({
    queryKey: ["amc-contracts", "list", appliedFilters, page] as const,
    queryFn: () =>
      searchAmcContracts({
        status: appliedFilters.status || undefined,
        customerSearch: appliedFilters.customerSearch || undefined,
        page,
        pageSize: PAGE_SIZE,
      }),
    placeholderData: keepPreviousData,
  });

  const detailQuery = useQuery({
    queryKey: ["amc-contract", openContractId] as const,
    queryFn: () => getAmcContract(openContractId as string),
    enabled: openContractId !== null,
  });

  const onSubmit = () => {
    setAppliedFilters(filters);
    setPage(1);
  };

  const onClear = () => {
    setFilters(EMPTY_FILTERS);
    setAppliedFilters(EMPTY_FILTERS);
    setPage(1);
  };

  const columns: DataTableColumn<AmcContractAdminListItemResponse>[] = [
    {
      key: "customer",
      header: "Customer",
      sortValue: (contract) => contract.customerName,
      cell: (contract) => <span className="font-medium text-fg">{contract.customerName}</span>,
    },
    {
      key: "plan",
      header: "Plan",
      sortValue: (contract) => contract.planName,
      cell: (contract) => contract.planName,
    },
    {
      key: "asset",
      header: "Asset",
      sortValue: (contract) => contract.assetLabel,
      cell: (contract) => contract.assetLabel,
    },
    {
      key: "status",
      header: "Status",
      sortValue: (contract) => contract.status,
      cell: (contract) => (
        <Badge tone={STATUS_TONES[contract.status] ?? "neutral"}>
          {CONTRACT_STATUS_LABELS[contract.status] ?? String(contract.status)}
        </Badge>
      ),
    },
    {
      key: "visits",
      header: "Visits",
      numeric: true,
      sortValue: (contract) => contract.visitsRemaining,
      cell: (contract) => (
        <span className="nums">
          {contract.visitsRemaining} / {contract.visitsIncluded}
        </span>
      ),
    },
    {
      key: "end",
      header: "Cover ends",
      sortValue: (contract) => contract.endDateUtc,
      cell: (contract) => <span className="nums">{formatDate(contract.endDateUtc)}</span>,
    },
    {
      key: "created",
      header: "Purchased",
      sortValue: (contract) => contract.createdAtUtc,
      cell: (contract) => <span className="nums">{formatDate(contract.createdAtUtc)}</span>,
    },
  ];

  return (
    <div className="w-full max-w-7xl">
      <PageHeading
        title="Bookings"
        subtitle="AMC contracts: prepaid entitlement to a fixed number of service visits, drawn down as the customer redeems them."
      />

      <BookingsTabs />

      <div className="flex flex-col gap-6">
        <FilterBar
          onSubmit={onSubmit}
          onClear={onClear}
          activeCount={countActiveFilters(appliedFilters)}
          busy={listQuery.isFetching}
        >
          <Select
            label="Status"
            options={STATUS_OPTIONS}
            value={filters.status}
            onChange={(e) => setFilters((f) => ({ ...f, status: e.target.value }))}
          />
          <Field
            label="Customer"
            name="customerSearch"
            autoComplete="name"
            placeholder="Name or mobile number"
            value={filters.customerSearch}
            onChange={(e) => setFilters((f) => ({ ...f, customerSearch: e.target.value }))}
          />
        </FilterBar>

        <div className="flex flex-col gap-4">
          <DataTable
            title="AMC contracts"
            description="Every contract on the platform, newest first."
            columns={columns}
            rows={listQuery.data?.items}
            rowKey={(contract) => contract.id}
            onRowClick={(contract) => setOpenContractId(contract.id)}
            isLoading={listQuery.isPending}
            isFetching={listQuery.isFetching}
            error={listQuery.error}
            onRetry={() => listQuery.refetch()}
            skeletonRows={8}
            minWidth="920px"
            caption="AMC contracts matching the current filters"
            emptyTitle="No AMC contracts match these filters"
            emptyDescription="Clear the filters to see every contract on the platform."
          />

          {listQuery.data && listQuery.data.totalCount > 0 ? (
            <Pagination
              page={listQuery.data.page}
              pageSize={listQuery.data.pageSize}
              totalCount={listQuery.data.totalCount}
              onPageChange={setPage}
              itemLabel="contract"
              busy={listQuery.isFetching}
            />
          ) : null}
        </div>
      </div>

      <AmcContractDetailModal
        contractId={openContractId}
        contract={detailQuery.data}
        isLoading={detailQuery.isPending}
        error={detailQuery.error}
        onClose={() => setOpenContractId(null)}
      />
    </div>
  );
}
