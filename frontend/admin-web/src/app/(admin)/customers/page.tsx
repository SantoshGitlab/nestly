"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useState } from "react";
import type { FormEvent } from "react";
import { Alert, Button, Card, Field, PageHeading } from "@/components/ui";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import { CustomerStatus } from "@/lib/types";
import type { CustomerSearchParams, CustomerSearchResponse } from "@/lib/types";

const PAGE_SIZE = 20;

const STATUS_OPTIONS: { value: CustomerStatus | ""; label: string }[] = [
  { value: "", label: "Any status" },
  { value: CustomerStatus.Active, label: "Active" },
  { value: CustomerStatus.Blocked, label: "Blocked" },
  { value: CustomerStatus.Unverified, label: "Unverified" },
  { value: CustomerStatus.SoftDeleted, label: "Deleted" },
];

function statusLabel(status: CustomerStatus): string {
  return STATUS_OPTIONS.find((o) => o.value === status)?.label ?? "Unknown";
}

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
    <div className="mx-auto w-full max-w-5xl">
      <PageHeading title="Customers" subtitle="Search and manage customer accounts (SRS 12.4)." />

      <Card title="Filters">
        <form onSubmit={onSubmit} className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
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
            value={filters.email}
            onChange={(e) => setFilters((f) => ({ ...f, email: e.target.value }))}
          />
          <Field
            label="City"
            value={filters.city}
            onChange={(e) => setFilters((f) => ({ ...f, city: e.target.value }))}
          />
          <div className="flex flex-col gap-1.5">
            <label htmlFor="status-filter" className="text-sm font-medium">
              Account status
            </label>
            <select
              id="status-filter"
              value={filters.status}
              onChange={(e) => setFilters((f) => ({ ...f, status: e.target.value }))}
              className="rounded-lg border border-black/15 bg-transparent px-3 py-2 text-sm outline-none focus:border-black focus:ring-1 focus:ring-black dark:border-white/20 dark:focus:border-white dark:focus:ring-white"
            >
              {STATUS_OPTIONS.map((option) => (
                <option key={option.label} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
          </div>
          <div className="flex items-end gap-2">
            <Button type="submit">Search</Button>
            <Button type="button" variant="secondary" onClick={onClear}>
              Clear
            </Button>
          </div>
        </form>
      </Card>

      <div className="mt-6">
        {query.isPending ? (
          <p className="text-sm text-neutral-500">Loading customers…</p>
        ) : query.isError ? (
          <Alert>{describeError(query.error)}</Alert>
        ) : query.data.items.length === 0 ? (
          <Card title="No customers match these filters">
            <p className="text-sm text-neutral-600 dark:text-neutral-400">
              Try broadening your search criteria.
            </p>
          </Card>
        ) : (
          <>
            <div className="overflow-x-auto rounded-xl border border-black/10 dark:border-white/15">
              <table className="w-full min-w-[720px] text-left text-sm">
                <thead className="border-b border-black/10 bg-black/5 dark:border-white/15 dark:bg-white/5">
                  <tr>
                    <th className="px-4 py-3 font-medium">Name</th>
                    <th className="px-4 py-3 font-medium">Mobile</th>
                    <th className="px-4 py-3 font-medium">Email</th>
                    <th className="px-4 py-3 font-medium">City</th>
                    <th className="px-4 py-3 font-medium">Status</th>
                    <th className="px-4 py-3 font-medium">Bookings</th>
                    <th className="px-4 py-3 font-medium">Registered</th>
                  </tr>
                </thead>
                <tbody>
                  {query.data.items.map((customer) => (
                    <tr
                      key={customer.id}
                      className="border-b border-black/5 last:border-0 hover:bg-black/[.03] dark:border-white/10 dark:hover:bg-white/[.05]"
                    >
                      <td className="px-4 py-3">
                        <Link href={`/customers/${customer.id}`} className="font-medium underline-offset-2 hover:underline">
                          {customer.name}
                        </Link>
                      </td>
                      <td className="px-4 py-3">{customer.mobile}</td>
                      <td className="px-4 py-3">{customer.email ?? "—"}</td>
                      <td className="px-4 py-3">{customer.city ?? "—"}</td>
                      <td className="px-4 py-3">{statusLabel(customer.status)}</td>
                      <td className="px-4 py-3">{customer.bookingCount}</td>
                      <td className="px-4 py-3">{new Date(customer.createdAtUtc).toLocaleDateString()}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <div className="mt-4 flex items-center justify-between text-sm">
              <span className="text-neutral-600 dark:text-neutral-400">
                Page {page} of {totalPages} · {query.data.totalCount} customer{query.data.totalCount === 1 ? "" : "s"}
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
