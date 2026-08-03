"use client";

import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { useState } from "react";
import type { ChangeEvent, FormEvent } from "react";
import { Alert, Button, Card, Field, PageHeading, StatTile } from "@/components/ui";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import type { DashboardKpiFilters, DashboardKpiResponse } from "@/lib/types";
import { toLocalIsoDate } from "@/lib/date";

const isoDate = toLocalIsoDate;

interface DatePreset {
  key: string;
  label: string;
  range: () => { dateFrom: string; dateTo: string };
}

/**
 * Date-range presets (SRS 12.3.1's "Bookings today/this week/this month" plus
 * the interaction convention of offering presets before a custom range).
 * "Last 7/30 days" rather than calendar week/month: a rolling window needs no
 * day-of-week/month-boundary logic and is the more common preset shape.
 */
const PRESETS: DatePreset[] = [
  {
    key: "today",
    label: "Today",
    range: () => {
      const today = isoDate(new Date());
      return { dateFrom: today, dateTo: today };
    },
  },
  {
    key: "last7",
    label: "Last 7 days",
    range: () => {
      const to = new Date();
      const from = new Date(to);
      from.setDate(from.getDate() - 6);
      return { dateFrom: isoDate(from), dateTo: isoDate(to) };
    },
  },
  {
    key: "last30",
    label: "Last 30 days",
    range: () => {
      const to = new Date();
      const from = new Date(to);
      from.setDate(from.getDate() - 29);
      return { dateFrom: isoDate(from), dateTo: isoDate(to) };
    },
  },
];

function buildQueryString(filters: DashboardKpiFilters): string {
  const params = new URLSearchParams();
  if (filters.dateFrom) params.set("dateFrom", filters.dateFrom);
  if (filters.dateTo) params.set("dateTo", filters.dateTo);
  if (filters.city) params.set("city", filters.city);
  if (filters.category) params.set("category", filters.category);
  const queryString = params.toString();
  return queryString ? `?${queryString}` : "";
}

/**
 * Dashboard KPI widgets (SRS 12.3, task 100): bookings, revenue,
 * cancellations, refunds, and open support tickets, scoped by the date
 * range/city/category filters above them - backed by the admin API's
 * dashboard endpoint (task 99, `IDashboardQueryService`).
 *
 * Filters sit in one row above the widgets and scope all of them at once
 * (never a per-widget filter), and a refetch keeps the previous numbers on
 * screen at reduced opacity rather than flashing a loading skeleton -
 * `keepPreviousData` is exactly this behaviour for react-query v5.
 */
export default function DashboardPage() {
  const todayIso = isoDate(new Date());

  const [filters, setFilters] = useState<DashboardKpiFilters>({ dateFrom: todayIso, dateTo: todayIso });
  const [activePreset, setActivePreset] = useState<string | null>("today");
  const [draftFrom, setDraftFrom] = useState(todayIso);
  const [draftTo, setDraftTo] = useState(todayIso);
  const [draftCity, setDraftCity] = useState("");
  const [draftCategory, setDraftCategory] = useState("");

  const query = useQuery({
    queryKey: ["dashboard-kpis", filters],
    queryFn: () =>
      apiFetch<DashboardKpiResponse>(`${API_V1}/dashboard/kpis${buildQueryString(filters)}`, {
        authenticated: true,
      }),
    placeholderData: keepPreviousData,
  });

  const applyPreset = (preset: DatePreset) => {
    const range = preset.range();
    setDraftFrom(range.dateFrom);
    setDraftTo(range.dateTo);
    setActivePreset(preset.key);
    setFilters((current) => ({ ...current, ...range }));
  };

  const onDraftDateChange = (setter: (value: string) => void) => (event: ChangeEvent<HTMLInputElement>) => {
    setter(event.target.value);
    setActivePreset(null);
  };

  const applyCustomFilters = (event: FormEvent) => {
    event.preventDefault();
    setActivePreset(null);
    setFilters({
      dateFrom: draftFrom || undefined,
      dateTo: draftTo || undefined,
      city: draftCity.trim() || undefined,
      category: draftCategory.trim() || undefined,
    });
  };

  return (
    <div className="mx-auto w-full max-w-5xl">
      <PageHeading title="Dashboard" subtitle="Bookings, revenue, cancellations, refunds, and support activity." />

      <Card title="Filters" description="Scope every widget below by date range, city, and category (SRS 12.3.2).">
        <div className="flex flex-col gap-4">
          <div className="flex flex-wrap gap-2" role="group" aria-label="Date range presets">
            {PRESETS.map((preset) => (
              <Button
                key={preset.key}
                type="button"
                variant={activePreset === preset.key ? "primary" : "secondary"}
                onClick={() => applyPreset(preset)}
              >
                {preset.label}
              </Button>
            ))}
          </div>

          <form onSubmit={applyCustomFilters} className="flex flex-wrap items-end gap-3">
            <Field label="From" type="date" value={draftFrom} onChange={onDraftDateChange(setDraftFrom)} />
            <Field label="To" type="date" value={draftTo} onChange={onDraftDateChange(setDraftTo)} />
            <Field
              label="City"
              placeholder="e.g. Bengaluru"
              value={draftCity}
              onChange={(event) => setDraftCity(event.target.value)}
            />
            <Field
              label="Category"
              placeholder="e.g. cleaning"
              value={draftCategory}
              onChange={(event) => setDraftCategory(event.target.value)}
            />
            <Button type="submit" variant="secondary">
              Apply filters
            </Button>
          </form>
        </div>
      </Card>

      <div className="mt-6">
        {query.isPending ? (
          <p className="text-sm text-neutral-500">Loading dashboard…</p>
        ) : query.isError ? (
          <Alert>{describeError(query.error)}</Alert>
        ) : (
          <>
            <p className="mb-3 text-sm text-neutral-600 dark:text-neutral-400">
              Showing {query.data.dateFrom} to {query.data.dateTo}
              {query.isFetching ? " · refreshing…" : ""}
            </p>
            <div
              className={`grid grid-cols-2 gap-4 transition-opacity sm:grid-cols-3 lg:grid-cols-5 ${
                query.isFetching ? "opacity-60" : ""
              }`}
            >
              <StatTile label="Bookings" value={query.data.bookingsCount.toLocaleString("en-IN")} />
              <StatTile
                label="Revenue"
                value={`₹${query.data.revenueTotal.toFixed(2)}`}
                title={`₹${query.data.revenueTotal.toFixed(2)}`}
              />
              <StatTile label="Cancellations" value={query.data.cancellationsCount.toLocaleString("en-IN")} />
              <StatTile
                label="Refund amount"
                value={`₹${query.data.refundAmountTotal.toFixed(2)}`}
                title={`₹${query.data.refundAmountTotal.toFixed(2)}`}
              />
              <StatTile
                label="Open support tickets"
                value={query.data.openSupportTicketsCount.toLocaleString("en-IN")}
              />
            </div>
          </>
        )}
      </div>
    </div>
  );
}
