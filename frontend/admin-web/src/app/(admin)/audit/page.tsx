"use client";

import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import type { FormEvent } from "react";
import { Alert, Button, Card, Field, PageHeading } from "@/components/ui";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import {
  AUDIT_ACTOR_TYPES,
  AUDIT_OUTCOMES,
  DEFAULT_AUDIT_LOG_FILTERS,
  actorTypeLabel,
  buildAuditLogQuery,
  outcomeLabel,
  type AuditLogEntry,
  type AuditLogFilters,
  type PagedAuditLogResponse,
} from "@/lib/audit";

/**
 * Audit log viewer (task 130, SRS 21): a filterable read over the same
 * audit trail task 95g's login audit and task 96d's permission-check audit
 * already write to - see `AuditLogController`/`AuditLogQueryService` on the
 * API side. Gated server-side behind "audit.read"; nav visibility for this
 * page follows the same permission (lib/permissions.ts).
 */

const selectClassName =
  "rounded-lg border border-black/15 bg-transparent px-3 py-2 text-sm outline-none focus:border-black focus:ring-1 focus:ring-black dark:border-white/20 dark:focus:border-white dark:focus:ring-white";
const labelClassName = "flex flex-col gap-1.5 text-sm font-medium";

function OutcomeBadge({ outcome }: { outcome: number }) {
  const label = outcomeLabel(outcome);
  const tone =
    label === "Grant" || label === "Success"
      ? "border-green-300 bg-green-50 text-green-900 dark:border-green-900 dark:bg-green-950 dark:text-green-100"
      : "border-red-300 bg-red-50 text-red-900 dark:border-red-900 dark:bg-red-950 dark:text-red-100";

  return (
    <span className={`inline-flex rounded-full border px-2 py-0.5 text-xs font-medium ${tone}`}>
      {label}
    </span>
  );
}

function formatTimestamp(occurredOnUtc: string): string {
  const normalised = /[Zz]|[+-]\d{2}:\d{2}$/.test(occurredOnUtc) ? occurredOnUtc : `${occurredOnUtc}Z`;
  return new Date(normalised).toLocaleString();
}

function AuditRow({ entry }: { entry: AuditLogEntry }) {
  return (
    <tr className="border-b border-black/5 last:border-0 dark:border-white/10">
      <td className="whitespace-nowrap px-3 py-2 align-top text-sm">{formatTimestamp(entry.occurredOnUtc)}</td>
      <td className="px-3 py-2 align-top text-sm">
        <div>{actorTypeLabel(entry.actorType)}</div>
        {entry.actorId ? (
          <div className="text-xs text-neutral-500 dark:text-neutral-400">{entry.actorId}</div>
        ) : null}
      </td>
      <td className="px-3 py-2 align-top text-sm">
        <div>{entry.entityName}</div>
        <div className="text-xs text-neutral-500 dark:text-neutral-400">{entry.entityId}</div>
      </td>
      <td className="px-3 py-2 align-top text-sm">{entry.action}</td>
      <td className="px-3 py-2 align-top">
        <OutcomeBadge outcome={entry.outcome} />
      </td>
      <td className="whitespace-nowrap px-3 py-2 align-top text-sm text-neutral-500 dark:text-neutral-400">
        {entry.ipAddress ?? "—"}
      </td>
    </tr>
  );
}

export default function AuditLogPage() {
  // Draft filters track the form inputs; `applied` only changes on submit
  // (or Reset) so the query does not refetch on every keystroke.
  const [draft, setDraft] = useState<AuditLogFilters>(DEFAULT_AUDIT_LOG_FILTERS);
  const [applied, setApplied] = useState<AuditLogFilters>(DEFAULT_AUDIT_LOG_FILTERS);

  const query = useQuery({
    queryKey: ["admin-audit-log", applied],
    queryFn: () =>
      apiFetch<PagedAuditLogResponse>(`${API_V1}/audit-log?${buildAuditLogQuery(applied)}`, {
        authenticated: true,
      }),
  });

  const onApply = (event: FormEvent) => {
    event.preventDefault();
    setApplied({ ...draft, page: 1 });
  };

  const onReset = () => {
    setDraft(DEFAULT_AUDIT_LOG_FILTERS);
    setApplied(DEFAULT_AUDIT_LOG_FILTERS);
  };

  const goToPage = (page: number) => setApplied((current) => ({ ...current, page }));

  const totalPages = query.data ? Math.max(1, Math.ceil(query.data.totalCount / query.data.pageSize)) : 1;

  return (
    <div className="mx-auto w-full max-w-6xl">
      <PageHeading
        title="Audit Log"
        subtitle="Every critical business and admin action recorded to the audit trail (SRS 21), filterable by actor, date range, entity/action, and outcome."
      />

      <Card title="Filters" description="All fields are optional; leave a field blank to not filter on it.">
        <form onSubmit={onApply} className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          <label className={labelClassName}>
            Actor type
            <select
              className={selectClassName}
              value={draft.actorType}
              onChange={(e) => setDraft({ ...draft, actorType: e.target.value as AuditLogFilters["actorType"] })}
            >
              <option value="">Any</option>
              {AUDIT_ACTOR_TYPES.map((type) => (
                <option key={type} value={type}>
                  {type}
                </option>
              ))}
            </select>
          </label>

          <Field
            label="Actor ID"
            placeholder="Admin user GUID"
            value={draft.actorId}
            onChange={(e) => setDraft({ ...draft, actorId: e.target.value })}
          />

          <Field
            label="Entity / module"
            placeholder="e.g. AdminUser, AdminPermissionCheck"
            value={draft.entityName}
            onChange={(e) => setDraft({ ...draft, entityName: e.target.value })}
          />

          <Field
            label="Action contains"
            placeholder="e.g. Login, PermissionDenied"
            value={draft.action}
            onChange={(e) => setDraft({ ...draft, action: e.target.value })}
          />

          <label className={labelClassName}>
            Outcome
            <select
              className={selectClassName}
              value={draft.outcome}
              onChange={(e) => setDraft({ ...draft, outcome: e.target.value as AuditLogFilters["outcome"] })}
            >
              <option value="">Any</option>
              {AUDIT_OUTCOMES.map((outcome) => (
                <option key={outcome} value={outcome}>
                  {outcome}
                </option>
              ))}
            </select>
          </label>

          <div className="grid grid-cols-2 gap-2">
            <Field
              label="From"
              type="date"
              value={draft.fromDate}
              onChange={(e) => setDraft({ ...draft, fromDate: e.target.value })}
            />
            <Field
              label="To"
              type="date"
              value={draft.toDate}
              onChange={(e) => setDraft({ ...draft, toDate: e.target.value })}
            />
          </div>

          <div className="flex items-end gap-2 sm:col-span-2 lg:col-span-3">
            <Button type="submit">Apply filters</Button>
            <Button type="button" variant="secondary" onClick={onReset}>
              Reset
            </Button>
          </div>
        </form>
      </Card>

      <div className="mt-6">
        <Card
          title="Results"
          description={
            query.data
              ? `${query.data.totalCount} matching entr${query.data.totalCount === 1 ? "y" : "ies"}.`
              : undefined
          }
        >
          {query.isLoading ? (
            <p className="text-sm text-neutral-600 dark:text-neutral-400">Loading audit trail…</p>
          ) : query.isError ? (
            <Alert>{describeError(query.error)}</Alert>
          ) : query.data && query.data.items.length > 0 ? (
            <>
              <div className="overflow-x-auto">
                <table className="w-full min-w-[720px] border-collapse text-left">
                  <thead>
                    <tr className="border-b border-black/10 text-xs uppercase tracking-wide text-neutral-500 dark:border-white/15 dark:text-neutral-400">
                      <th className="px-3 py-2 font-medium">Timestamp (local)</th>
                      <th className="px-3 py-2 font-medium">Actor</th>
                      <th className="px-3 py-2 font-medium">Entity</th>
                      <th className="px-3 py-2 font-medium">Action</th>
                      <th className="px-3 py-2 font-medium">Outcome</th>
                      <th className="px-3 py-2 font-medium">IP</th>
                    </tr>
                  </thead>
                  <tbody>
                    {query.data.items.map((entry) => (
                      <AuditRow key={entry.id} entry={entry} />
                    ))}
                  </tbody>
                </table>
              </div>

              <div className="mt-4 flex items-center justify-between">
                <span className="text-sm text-neutral-600 dark:text-neutral-400">
                  Page {query.data.page} of {totalPages}
                </span>
                <div className="flex gap-2">
                  <Button
                    type="button"
                    variant="secondary"
                    disabled={query.data.page <= 1}
                    onClick={() => goToPage(query.data!.page - 1)}
                  >
                    Previous
                  </Button>
                  <Button
                    type="button"
                    variant="secondary"
                    disabled={query.data.page >= totalPages}
                    onClick={() => goToPage(query.data!.page + 1)}
                  >
                    Next
                  </Button>
                </div>
              </div>
            </>
          ) : (
            <p className="text-sm text-neutral-600 dark:text-neutral-400">
              No audit entries match these filters.
            </p>
          )}
        </Card>
      </div>
    </div>
  );
}
