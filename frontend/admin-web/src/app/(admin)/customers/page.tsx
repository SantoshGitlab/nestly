"use client";

import { keepPreviousData, useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useState } from "react";
import { Button, Field, PageHeading, Select } from "@/components/ui";
import {
  DataTable,
  FilterBar,
  Pagination,
  countActiveFilters,
  formatDate,
} from "@/components/data-table";
import type { DataTableColumn } from "@/components/data-table";
import { CustomerStatusBadge } from "@/components/status-badges";
import { API_V1, apiFetch } from "@/lib/api";
import { CustomerStatus } from "@/lib/types";
import type { CustomerSearchParams, CustomerSearchResponse, CustomerSummary } from "@/lib/types";

const PAGE_SIZE = 20;

const STATUS_OPTIONS: { value: string; label: string }[] = [
  { value: "", label: "Any status" },
  { value: String(CustomerStatus.Active), label: "Active" },
  { value: String(CustomerStatus.Blocked), label: "Blocked" },
  { value: String(CustomerStatus.Unverified), label: "Unverified" },
  { value: String(CustomerStatus.SoftDeleted), label: "Deleted" },
];

interface FilterFormState {
  name: string;
  mobile: string;
  email: string;
  city: string;
  status: string;
}

const EMPTY_FILTERS: FilterFormState = { name: "", mobile: "", email: "", city: "", status: "" };

function buildQueryString(filters: FilterFormState, page: number): string {
  const params: CustomerSearchParams = {
    name: filters.name || undefined,
    mobile: filters.mobile || undefined,
    email: filters.email || undefined,
    city: filters.city || undefined,
    status: filters.status === "" ? undefined : (Number(filters.status) as CustomerStatus),
    page,
    pageSize: PAGE_SIZE,
  };

  const query = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined) query.set(key, String(value));
  }
  return query.toString();
}

/**
 * Customer search/list screen (SRS 12.4.1, task 102). Filters by name,
 * mobile, email, city and account status - the full SRS 12.4.1 filter list
 * also includes registration date and booking count, which are supported by
 * the API (CustomerSearchParams) but left off this form for now to keep the
 * first cut usable; add fields here without any backend change when needed.
 *
 * Built on the task 221 pattern. Columns are deliberately NOT sortable: the
 * list is paged server-side and the endpoint takes no sort parameter, so a
 * header sort would silently reorder only the 20 rows on screen.
 */
export default function CustomersPage() {
  const [filters, setFilters] = useState<FilterFormState>(EMPTY_FILTERS);
  const [appliedFilters, setAppliedFilters] = useState<FilterFormState>(EMPTY_FILTERS);
  const [page, setPage] = useState(1);

  const query = useQuery({
    queryKey: ["admin-customers", appliedFilters, page],
    queryFn: () =>
      apiFetch<CustomerSearchResponse>(`${API_V1}/customers?${buildQueryString(appliedFilters, page)}`, {
        authenticated: true,
      }),
    placeholderData: keepPreviousData,
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

  const columns: DataTableColumn<CustomerSummary>[] = [
    {
      key: "name",
      header: "Name",
      cell: (customer) => (
        <Link
          href={`/customers/${customer.id}`}
          className="font-medium text-fg underline-offset-4 hover:text-brand-600 hover:underline dark:hover:text-brand-400"
        >
          {customer.name}
        </Link>
      ),
    },
    {
      key: "mobile",
      header: "Mobile",
      cell: (customer) => <span className="nums">{customer.mobile}</span>,
    },
    { key: "email", header: "Email", cell: (customer) => customer.email ?? "—" },
    { key: "city", header: "City", cell: (customer) => customer.city ?? "—" },
    {
      key: "status",
      header: "Status",
      cell: (customer) => <CustomerStatusBadge status={customer.status} />,
    },
    {
      key: "bookings",
      header: "Bookings",
      numeric: true,
      cell: (customer) => customer.bookingCount,
    },
    {
      key: "registered",
      header: "Registered",
      cell: (customer) => <span className="nums">{formatDate(customer.createdAtUtc)}</span>,
    },
  ];

  return (
    <div className="mx-auto w-full max-w-7xl">
      <PageHeading title="Customers" subtitle="Search and manage customer accounts (SRS 12.4)." />

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
          label="Mobile"
          value={filters.mobile}
          onChange={(e) => setFilters((f) => ({ ...f, mobile: e.target.value }))}
        />
        <Field
          label="Email"
          type="email"
          value={filters.email}
          onChange={(e) => setFilters((f) => ({ ...f, email: e.target.value }))}
        />
        <Field
          label="City"
          value={filters.city}
          onChange={(e) => setFilters((f) => ({ ...f, city: e.target.value }))}
        />
        <Select
          label="Account status"
          options={STATUS_OPTIONS}
          value={filters.status}
          onChange={(e) => setFilters((f) => ({ ...f, status: e.target.value }))}
        />
      </FilterBar>

      <div className="mt-6">
        <DataTable
          title="Results"
          columns={columns}
          rows={query.data?.items}
          rowKey={(customer) => customer.id}
          isLoading={query.isPending}
          isFetching={query.isFetching}
          error={query.error}
          onRetry={() => query.refetch()}
          skeletonRows={8}
          minWidth="920px"
          caption="Customers matching the current filters"
          emptyTitle="No customers match these filters"
          emptyDescription="Try a partial name or mobile number, or clear the filters to see every customer."
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
                itemLabel="customer"
              />
            ) : null
          }
        />
      </div>
    </div>
  );
}
