"use client";

import { keepPreviousData, useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { Suspense, useEffect, useState } from "react";
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
import { listCities } from "@/lib/serviceability-api";
import { CustomerStatus } from "@/lib/types";
import type { CustomerSearchParams, CustomerSearchResponse, CustomerSummary } from "@/lib/types";
import { CustomersTabs } from "../_components/CustomersTabs";

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
  /**
   * Form-hidden (no visible input - see this page's own doc comment): only
   * ever set by the Customer Analytics dashboard's tiles - "With bookings"/
   * "Zero bookings" (`?minBookingCount=1`/`?maxBookingCount=0`) and "New
   * today"/"New last 7 days"/"New last N days" (`?registeredFromUtc=...`,
   * an ISO instant) - never typed by an admin. Still read from the URL and
   * sent to the API like every other filter, or a tile click-through would
   * silently do nothing.
   */
  minBookingCount: string;
  maxBookingCount: string;
  registeredFromUtc: string;
}

const EMPTY_FILTERS: FilterFormState = {
  name: "",
  mobile: "",
  email: "",
  city: "",
  status: "",
  minBookingCount: "",
  maxBookingCount: "",
  registeredFromUtc: "",
};

/**
 * Seeds the filter form from the URL's query params - the Customer Analytics
 * dashboard's status tiles link here as `/customers/directory?status=X` and
 * expect the list to open already filtered, not requiring the admin to
 * re-pick the status by hand (same reasoning, and the same pattern, as
 * providers/directory/page.tsx's own filtersFromSearchParams).
 */
function filtersFromSearchParams(params: URLSearchParams): FilterFormState {
  return {
    name: params.get("name") ?? "",
    mobile: params.get("mobile") ?? "",
    email: params.get("email") ?? "",
    city: params.get("city") ?? "",
    status: params.get("status") ?? "",
    minBookingCount: params.get("minBookingCount") ?? "",
    maxBookingCount: params.get("maxBookingCount") ?? "",
    registeredFromUtc: params.get("registeredFromUtc") ?? "",
  };
}

function buildParamsQuery(params: CustomerSearchParams): string {
  const query = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined) query.set(key, String(value));
  }
  return query.toString();
}

function buildQueryString(filters: FilterFormState, page: number): string {
  return buildParamsQuery({
    name: filters.name || undefined,
    mobile: filters.mobile || undefined,
    email: filters.email || undefined,
    city: filters.city || undefined,
    status: filters.status === "" ? undefined : (Number(filters.status) as CustomerStatus),
    minBookingCount: filters.minBookingCount === "" ? undefined : Number(filters.minBookingCount),
    maxBookingCount: filters.maxBookingCount === "" ? undefined : Number(filters.maxBookingCount),
    registeredFromUtc: filters.registeredFromUtc || undefined,
    page,
    pageSize: PAGE_SIZE,
  });
}

/**
 * Customer search/list screen (SRS 12.4.1, task 102). Visible filters are
 * name, mobile, email, city and account status; registration date
 * (`registeredFromUtc`) and booking count (`minBookingCount`/
 * `maxBookingCount`) are supported by the API and wired into this page's
 * state, but have no visible form inputs of their own - they exist purely so
 * the Customer Analytics dashboard's tile click-throughs actually filter
 * (see FilterFormState's own doc comment). Add real inputs for them here
 * without any backend change when needed.
 *
 * Built on the task 221 pattern. Columns are deliberately NOT sortable: the
 * list is paged server-side and the endpoint takes no sort parameter, so a
 * header sort would silently reorder only the 20 rows on screen.
 *
 * Wrapped in Suspense: `useSearchParams` (reading the Customer Analytics
 * dashboard's click-through filters) opts the tree below it out of static
 * rendering, and Next's App Router requires a Suspense boundary around that
 * or the production build fails (same pattern providers/page.tsx uses).
 */
export default function CustomersPage() {
  return (
    <Suspense fallback={<div className="w-full max-w-7xl px-6 py-10" />}>
      <CustomersPageContent />
    </Suspense>
  );
}

function CustomersPageContent() {
  const searchParams = useSearchParams();
  const [filters, setFilters] = useState<FilterFormState>(() => filtersFromSearchParams(searchParams));
  const [page, setPage] = useState(1);

  // Real city list to suggest against the City field, which stays a plain
  // text input (never a dropdown) - the search endpoint matches city with a
  // case-insensitive Contains (CustomerRepository.SearchAsync), so a real
  // city name posts cleanly whether typed or picked from the datalist.
  const citiesQuery = useQuery({
    queryKey: ["cities"],
    queryFn: () => listCities(),
    staleTime: 5 * 60 * 1000,
  });

  // Live filtering (no Search button - task: "auto search when searching
  // something"): every free-text field (Name, Mobile, Email, City) is
  // debounced 300ms before it hits the query, same convention as the Name
  // typeahead below and as payments/reconciliation/page.tsx's search box;
  // Account status (a dropdown) applies immediately.
  const [debouncedName, setDebouncedName] = useState(filters.name);
  useEffect(() => {
    const handle = window.setTimeout(() => setDebouncedName(filters.name.trim()), 300);
    return () => window.clearTimeout(handle);
  }, [filters.name]);

  const [debouncedMobile, setDebouncedMobile] = useState(filters.mobile);
  useEffect(() => {
    const handle = window.setTimeout(() => setDebouncedMobile(filters.mobile.trim()), 300);
    return () => window.clearTimeout(handle);
  }, [filters.mobile]);

  const [debouncedEmail, setDebouncedEmail] = useState(filters.email);
  useEffect(() => {
    const handle = window.setTimeout(() => setDebouncedEmail(filters.email.trim()), 300);
    return () => window.clearTimeout(handle);
  }, [filters.email]);

  const [debouncedCity, setDebouncedCity] = useState(filters.city);
  useEffect(() => {
    const handle = window.setTimeout(() => setDebouncedCity(filters.city.trim()), 300);
    return () => window.clearTimeout(handle);
  }, [filters.city]);

  // Any filter change resets to page 1 - staying on page 3 of a now-smaller
  // result set would just show an empty page (same pattern as
  // payments/reconciliation/page.tsx).
  useEffect(() => {
    setPage(1);
  }, [
    debouncedName,
    debouncedMobile,
    debouncedEmail,
    debouncedCity,
    filters.status,
    filters.minBookingCount,
    filters.maxBookingCount,
    filters.registeredFromUtc,
  ]);

  // Live typeahead for Name - reuses the same customer search this page
  // already calls, same pattern as bookings/page.tsx's Booking # suggestions
  // (there is no standalone exported customer-search client to reuse from
  // elsewhere, so this reuses the page's own endpoint with a small pageSize).
  // Shares `debouncedName` with the main query below rather than debouncing
  // twice.
  const nameSuggestionsQuery = useQuery({
    queryKey: ["admin-customers-name-suggestions", debouncedName],
    queryFn: () =>
      apiFetch<CustomerSearchResponse>(
        `${API_V1}/customers?${buildParamsQuery({ name: debouncedName, page: 1, pageSize: 8 })}`,
        { authenticated: true },
      ),
    enabled: debouncedName.length >= 2,
    placeholderData: keepPreviousData,
  });

  const query = useQuery({
    queryKey: [
      "admin-customers",
      page,
      debouncedName,
      debouncedMobile,
      debouncedEmail,
      debouncedCity,
      filters.status,
      filters.minBookingCount,
      filters.maxBookingCount,
      filters.registeredFromUtc,
    ] as const,
    queryFn: () =>
      apiFetch<CustomerSearchResponse>(
        `${API_V1}/customers?${buildQueryString(
          {
            name: debouncedName,
            mobile: debouncedMobile,
            email: debouncedEmail,
            city: debouncedCity,
            status: filters.status,
            minBookingCount: filters.minBookingCount,
            maxBookingCount: filters.maxBookingCount,
            registeredFromUtc: filters.registeredFromUtc,
          },
          page,
        )}`,
        { authenticated: true },
      ),
    placeholderData: keepPreviousData,
  });

  const onClear = () => {
    setFilters(EMPTY_FILTERS);
    setDebouncedName("");
    setDebouncedMobile("");
    setDebouncedEmail("");
    setDebouncedCity("");
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
    <div className="w-full max-w-7xl">
      <PageHeading title="Customers" subtitle="Search and manage customer accounts (SRS 12.4)." />
      <CustomersTabs />

      <FilterBar
        onClear={onClear}
        activeCount={countActiveFilters(filters)}
        busy={query.isFetching}
        columns={3}
      >
        <Field
          label="Name"
          name="name"
          autoComplete="name"
          list="customer-name-suggestions"
          value={filters.name}
          onChange={(e) => setFilters((f) => ({ ...f, name: e.target.value }))}
        />
        <datalist id="customer-name-suggestions">
          {(nameSuggestionsQuery.data?.items ?? []).map((customer) => (
            <option key={customer.id} value={customer.name} />
          ))}
        </datalist>
        <Field
          label="Mobile"
          name="mobile"
          autoComplete="tel"
          value={filters.mobile}
          onChange={(e) => setFilters((f) => ({ ...f, mobile: e.target.value }))}
        />
        <Field
          label="Email"
          type="email"
          name="email"
          autoComplete="email"
          value={filters.email}
          onChange={(e) => setFilters((f) => ({ ...f, email: e.target.value }))}
        />
        <Field
          label="City"
          name="city"
          autoComplete="address-level2"
          list="customer-city-suggestions"
          value={filters.city}
          onChange={(e) => setFilters((f) => ({ ...f, city: e.target.value }))}
        />
        {/* Options only appear once the admin has typed something - an empty
            field must not pop the entire city list on click. */}
        <datalist id="customer-city-suggestions">
          {filters.city.trim()
            ? (citiesQuery.data ?? [])
                .filter((city) => city.name.toLowerCase().includes(filters.city.trim().toLowerCase()))
                .map((city) => <option key={city.id} value={city.name} />)
            : null}
        </datalist>
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
