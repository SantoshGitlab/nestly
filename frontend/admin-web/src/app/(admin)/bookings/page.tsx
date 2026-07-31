"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useState } from "react";
import type { FormEvent } from "react";
import { Alert, Button, Card, Field, PageHeading, Select } from "@/components/ui";
import { describeError } from "@/lib/api";
import { searchBookings } from "@/lib/bookings-api";
import { BookingStatus } from "@/lib/types";

const PAGE_SIZE = 20;

const STATUS_OPTIONS: { value: string; label: string }[] = [
  { value: "", label: "Any status" },
  { value: String(BookingStatus.Initiated), label: "Booking Started" },
  { value: String(BookingStatus.PaymentPending), label: "Awaiting Payment" },
  { value: String(BookingStatus.PaymentFailed), label: "Payment Failed" },
  { value: String(BookingStatus.Confirmed), label: "Confirmed" },
  { value: String(BookingStatus.AwaitingFulfilment), label: "Preparing Service" },
  { value: String(BookingStatus.Assigned), label: "Professional Assigned" },
  { value: String(BookingStatus.InProgress), label: "In Progress" },
  { value: String(BookingStatus.Completed), label: "Completed" },
  { value: String(BookingStatus.CancelledByCustomer), label: "Cancelled by Customer" },
  { value: String(BookingStatus.CancelledByAdmin), label: "Cancelled by Admin" },
  { value: String(BookingStatus.Rescheduled), label: "Rescheduled" },
  { value: String(BookingStatus.RefundPending), label: "Refund in Progress" },
  { value: String(BookingStatus.Refunded), label: "Refunded" },
];

interface FilterFormState {
  bookingId: string;
  customerName: string;
  customerMobile: string;
  status: string;
  city: string;
  couponCode: string;
  slotDateFrom: string;
  slotDateTo: string;
}

const EMPTY_FILTERS: FilterFormState = {
  bookingId: "",
  customerName: "",
  customerMobile: "",
  status: "",
  city: "",
  couponCode: "",
  slotDateFrom: "",
  slotDateTo: "",
};

/**
 * Admin booking list/search screen (SRS 12.11.1, task 116). Filters cover
 * booking id, customer name/mobile, status, city, coupon code and slot date
 * range - the SRS 12.11.1 list also mentions "category/service" and
 * "payment status" filters (supported server-side via
 * AdminBookingSearchParams.serviceId/categoryId, but no lookup picker exists
 * on this screen yet) and a "professional/source" filter that has no backing
 * domain concept at all (see BookingSearchFilter's doc comment).
 */
export default function BookingsPage() {
  const [filters, setFilters] = useState<FilterFormState>(EMPTY_FILTERS);
  const [appliedFilters, setAppliedFilters] = useState<FilterFormState>(EMPTY_FILTERS);
  const [page, setPage] = useState(1);

  const query = useQuery({
    queryKey: ["admin-bookings", appliedFilters, page],
    queryFn: () =>
      searchBookings({
        bookingId: appliedFilters.bookingId || undefined,
        customerName: appliedFilters.customerName || undefined,
        customerMobile: appliedFilters.customerMobile || undefined,
        status: appliedFilters.status === "" ? undefined : (Number(appliedFilters.status) as BookingStatus),
        city: appliedFilters.city || undefined,
        couponCode: appliedFilters.couponCode || undefined,
        slotDateFrom: appliedFilters.slotDateFrom || undefined,
        slotDateTo: appliedFilters.slotDateTo || undefined,
        page,
        pageSize: PAGE_SIZE,
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
    <div className="mx-auto w-full max-w-6xl">
      <PageHeading title="Bookings" subtitle="Search bookings and manage cancellations, reschedules and refunds (SRS 12.11)." />

      <Card title="Filters">
        <form onSubmit={onSubmit} className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <Field
            label="Booking ID"
            value={filters.bookingId}
            onChange={(e) => setFilters((f) => ({ ...f, bookingId: e.target.value }))}
            placeholder="Exact booking ID"
          />
          <Field
            label="Customer name"
            value={filters.customerName}
            onChange={(e) => setFilters((f) => ({ ...f, customerName: e.target.value }))}
          />
          <Field
            label="Customer mobile"
            value={filters.customerMobile}
            onChange={(e) => setFilters((f) => ({ ...f, customerMobile: e.target.value }))}
          />
          <Select
            label="Status"
            options={STATUS_OPTIONS}
            value={filters.status}
            onChange={(e) => setFilters((f) => ({ ...f, status: e.target.value }))}
          />
          <Field
            label="City"
            value={filters.city}
            onChange={(e) => setFilters((f) => ({ ...f, city: e.target.value }))}
          />
          <Field
            label="Coupon code"
            value={filters.couponCode}
            onChange={(e) => setFilters((f) => ({ ...f, couponCode: e.target.value }))}
          />
          <Field
            label="Slot date from"
            type="date"
            value={filters.slotDateFrom}
            onChange={(e) => setFilters((f) => ({ ...f, slotDateFrom: e.target.value }))}
          />
          <Field
            label="Slot date to"
            type="date"
            value={filters.slotDateTo}
            onChange={(e) => setFilters((f) => ({ ...f, slotDateTo: e.target.value }))}
          />
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
          <p className="text-sm text-neutral-500">Loading bookings…</p>
        ) : query.isError ? (
          <Alert>{describeError(query.error)}</Alert>
        ) : query.data.items.length === 0 ? (
          <Card title="No bookings match these filters">
            <p className="text-sm text-neutral-600 dark:text-neutral-400">Try broadening your search criteria.</p>
          </Card>
        ) : (
          <>
            <div className="overflow-x-auto rounded-xl border border-black/10 dark:border-white/15">
              <table className="w-full min-w-[860px] text-left text-sm">
                <thead className="border-b border-black/10 bg-black/5 dark:border-white/15 dark:bg-white/5">
                  <tr>
                    <th className="px-4 py-3 font-medium">Customer</th>
                    <th className="px-4 py-3 font-medium">Service</th>
                    <th className="px-4 py-3 font-medium">City</th>
                    <th className="px-4 py-3 font-medium">Slot date</th>
                    <th className="px-4 py-3 font-medium">Status</th>
                    <th className="px-4 py-3 font-medium">Total</th>
                    <th className="px-4 py-3 font-medium">Created</th>
                  </tr>
                </thead>
                <tbody>
                  {query.data.items.map((booking) => (
                    <tr
                      key={booking.id}
                      className="border-b border-black/5 last:border-0 hover:bg-black/[.03] dark:border-white/10 dark:hover:bg-white/[.05]"
                    >
                      <td className="px-4 py-3">
                        <Link href={`/bookings/${booking.id}`} className="font-medium underline-offset-2 hover:underline">
                          {booking.customerName}
                        </Link>
                        <div className="text-xs text-neutral-500">{booking.customerMobile}</div>
                      </td>
                      <td className="px-4 py-3">{booking.serviceName}</td>
                      <td className="px-4 py-3">{booking.city}</td>
                      <td className="px-4 py-3">{booking.slotDate}</td>
                      <td className="px-4 py-3">{booking.statusLabel}</td>
                      <td className="px-4 py-3">₹{booking.totalPayable.toFixed(2)}</td>
                      <td className="px-4 py-3">{new Date(booking.createdAtUtc).toLocaleDateString()}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <div className="mt-4 flex items-center justify-between text-sm">
              <span className="text-neutral-600 dark:text-neutral-400">
                Page {page} of {totalPages} · {query.data.totalCount} booking{query.data.totalCount === 1 ? "" : "s"}
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
