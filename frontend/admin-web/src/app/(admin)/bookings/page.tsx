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
  formatCurrency,
  formatDate,
} from "@/components/data-table";
import type { DataTableColumn } from "@/components/data-table";
import { BookingStatusBadge } from "@/components/status-badges";
import { searchBookings } from "@/lib/bookings-api";
import type { AdminBookingListItem } from "@/lib/bookings-types";
import { listCategories } from "@/lib/catalog-api";
import { BookingStatus } from "@/lib/types";
import { BookingsTabs } from "@/components/BookingsTabs";

const PAGE_SIZE = 20;

const STATUS_OPTIONS: { value: string; label: string }[] = [
  { value: "", label: "Any status" },
  { value: String(BookingStatus.Initiated), label: "Booking Started" },
  { value: String(BookingStatus.PaymentPending), label: "Awaiting Payment" },
  { value: String(BookingStatus.PaymentFailed), label: "Payment Failed" },
  { value: String(BookingStatus.Confirmed), label: "Confirmed" },
  { value: String(BookingStatus.AwaitingFulfilment), label: "Preparing Service" },
  { value: String(BookingStatus.Assigned), label: "Professional Assigned" },
  { value: String(BookingStatus.ProviderEnRoute), label: "Professional On the Way" },
  { value: String(BookingStatus.ProviderArrived), label: "Professional Arrived" },
  { value: String(BookingStatus.InProgress), label: "In Progress" },
  { value: String(BookingStatus.Completed), label: "Completed" },
  { value: String(BookingStatus.CancelledByCustomer), label: "Cancelled by Customer" },
  { value: String(BookingStatus.CancelledByAdmin), label: "Cancelled by Admin" },
  { value: String(BookingStatus.Rescheduled), label: "Rescheduled" },
  { value: String(BookingStatus.RefundPending), label: "Refund in Progress" },
  { value: String(BookingStatus.Refunded), label: "Refunded" },
  { value: String(BookingStatus.Expired), label: "Expired" },
];

interface FilterFormState {
  reference: string;
  customerName: string;
  customerMobile: string;
  status: string;
  city: string;
  categoryId: string;
  couponCode: string;
  slotDateFrom: string;
  slotDateTo: string;
}

const EMPTY_FILTERS: FilterFormState = {
  reference: "",
  customerName: "",
  customerMobile: "",
  status: "",
  city: "",
  categoryId: "",
  couponCode: "",
  slotDateFrom: "",
  slotDateTo: "",
};

/**
 * Admin booking list/search screen (SRS 12.11.1, task 116). Filters cover
 * booking reference, customer name/mobile, status, city, category, coupon
 * code and slot date range - the SRS 12.11.1 list also mentions a "service"
 * filter (supported server-side via AdminBookingSearchParams.serviceId, and
 * listServices() could back a dropdown the same way listCategories() backs
 * Category, but no picker exists on this screen yet) and "payment status"/
 * "professional"/"source" filters that have no backing domain concept at all
 * (see BookingSearchFilter's doc comment).
 *
 * Built on the task 221 pattern. Columns are deliberately NOT sortable: this
 * list is paged server-side and the endpoint takes no sort parameter, so a
 * header sort would silently reorder only the 20 rows on screen.
 */
export default function BookingsPage() {
  const [filters, setFilters] = useState<FilterFormState>(EMPTY_FILTERS);
  const [appliedFilters, setAppliedFilters] = useState<FilterFormState>(EMPTY_FILTERS);
  const [page, setPage] = useState(1);

  // Real category list for the Category filter's dropdown, not free text -
  // the search endpoint takes a CategoryId GUID (BookingRepository.SearchAsync),
  // so this can't be a typed field the way City/Coupon code are.
  const categoriesQuery = useQuery({
    queryKey: ["catalog-categories"],
    queryFn: listCategories,
    staleTime: 5 * 60 * 1000,
  });
  const categoryOptions = [
    { value: "", label: "Any category" },
    ...(categoriesQuery.data ?? []).map((category) => ({ value: category.id, label: category.name })),
  ];

  const query = useQuery({
    queryKey: ["admin-bookings", appliedFilters, page],
    queryFn: () =>
      searchBookings({
        reference: appliedFilters.reference || undefined,
        customerName: appliedFilters.customerName || undefined,
        customerMobile: appliedFilters.customerMobile || undefined,
        status: appliedFilters.status === "" ? undefined : (Number(appliedFilters.status) as BookingStatus),
        city: appliedFilters.city || undefined,
        categoryId: appliedFilters.categoryId || undefined,
        couponCode: appliedFilters.couponCode || undefined,
        slotDateFrom: appliedFilters.slotDateFrom || undefined,
        slotDateTo: appliedFilters.slotDateTo || undefined,
        page,
        pageSize: PAGE_SIZE,
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

  const columns: DataTableColumn<AdminBookingListItem>[] = [
    {
      key: "reference",
      header: "Booking #",
      cell: (booking) => (
        <Link
          href={`/bookings/${booking.id}`}
          className="nums font-medium text-fg underline-offset-4 hover:text-brand-600 hover:underline dark:hover:text-brand-400"
        >
          {booking.reference}
        </Link>
      ),
    },
    {
      key: "customer",
      header: "Customer",
      cell: (booking) => (
        <>
          <span className="text-fg">{booking.customerName}</span>
          <div className="nums text-xs text-fg-subtle">{booking.customerMobile}</div>
        </>
      ),
    },
    { key: "service", header: "Service", cell: (booking) => booking.serviceName },
    { key: "city", header: "City", cell: (booking) => booking.city },
    {
      key: "slotDate",
      header: "Slot date",
      cell: (booking) => <span className="nums">{booking.slotDate}</span>,
    },
    {
      key: "status",
      header: "Status",
      cell: (booking) => <BookingStatusBadge status={booking.status} label={booking.statusLabel} />,
    },
    {
      key: "total",
      header: "Total",
      numeric: true,
      cell: (booking) => formatCurrency(booking.totalPayable),
    },
    {
      key: "created",
      header: "Created",
      cell: (booking) => <span className="nums">{formatDate(booking.createdAtUtc)}</span>,
    },
  ];

  return (
    <div className="w-full max-w-7xl">
      <PageHeading
        title="Bookings"
        subtitle="Search bookings and manage cancellations, reschedules and refunds (SRS 12.11)."
      />

      <BookingsTabs />

      <FilterBar
        onSubmit={onSubmit}
        onClear={onClear}
        activeCount={countActiveFilters(appliedFilters)}
        busy={query.isFetching}
      >
        <Field
          label="Booking #"
          name="reference"
          autoComplete="on"
          value={filters.reference}
          onChange={(e) => setFilters((f) => ({ ...f, reference: e.target.value }))}
          placeholder="e.g. NST-260825-K7F3M"
        />
        <Field
          label="Customer name"
          name="customerName"
          autoComplete="name"
          value={filters.customerName}
          onChange={(e) => setFilters((f) => ({ ...f, customerName: e.target.value }))}
        />
        <Field
          label="Customer mobile"
          name="customerMobile"
          autoComplete="tel"
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
          name="city"
          autoComplete="address-level2"
          value={filters.city}
          onChange={(e) => setFilters((f) => ({ ...f, city: e.target.value }))}
        />
        <Select
          label="Category"
          options={categoryOptions}
          value={filters.categoryId}
          onChange={(e) => setFilters((f) => ({ ...f, categoryId: e.target.value }))}
        />
        <Field
          label="Coupon code"
          name="couponCode"
          autoComplete="on"
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
      </FilterBar>

      <div className="mt-6">
        <DataTable
          title="Results"
          columns={columns}
          rows={query.data?.items}
          rowKey={(booking) => booking.id}
          isLoading={query.isPending}
          isFetching={query.isFetching}
          error={query.error}
          onRetry={() => query.refetch()}
          skeletonRows={8}
          minWidth="920px"
          caption="Bookings matching the current filters"
          emptyTitle="No bookings match these filters"
          emptyDescription="Try broadening the date range, or clear the filters to see every booking."
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
                itemLabel="booking"
              />
            ) : null
          }
        />
      </div>
    </div>
  );
}
