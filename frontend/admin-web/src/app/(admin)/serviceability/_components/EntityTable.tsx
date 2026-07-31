"use client";

import type { ReactNode } from "react";
import { Button } from "@/components/ui";

/**
 * Shared list rendering for every serviceability admin screen (SRS 12.9):
 * geography master rows (state/city/zone/locality/pincode) and mapping rows
 * (category-city/service-pincode) all reduce to "columns of data plus an
 * active/inactive toggle", so this is the one place that shape is rendered
 * rather than five/seven near-identical tables.
 */

export interface EntityTableColumn<T> {
  header: string;
  render: (item: T) => ReactNode;
}

export function StatusBadge({ isActive }: { isActive: boolean }) {
  return (
    <span
      className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${
        isActive
          ? "bg-green-100 text-green-800 dark:bg-green-950 dark:text-green-200"
          : "bg-neutral-200 text-neutral-700 dark:bg-neutral-800 dark:text-neutral-300"
      }`}
    >
      {isActive ? "Active" : "Suspended"}
    </span>
  );
}

export function EntityTable<T extends { id: string; isActive: boolean }>({
  items,
  columns,
  isLoading,
  errorMessage,
  emptyMessage,
  canWrite,
  onToggleActive,
  togglingId,
}: {
  items: T[] | undefined;
  columns: readonly EntityTableColumn<T>[];
  isLoading: boolean;
  errorMessage: string | null;
  emptyMessage: string;
  /** Whether the current admin holds the module's write permission (SRS 12.2.3) - hides mutating controls otherwise. */
  canWrite: boolean;
  onToggleActive: (item: T) => void;
  /** Id of the row currently being toggled, to disable just that row's button rather than the whole table. */
  togglingId?: string;
}) {
  if (isLoading) {
    return <p className="text-sm text-neutral-600 dark:text-neutral-400">Loading…</p>;
  }

  if (errorMessage) {
    return <p className="text-sm text-red-600 dark:text-red-400">{errorMessage}</p>;
  }

  if (!items || items.length === 0) {
    return <p className="text-sm text-neutral-600 dark:text-neutral-400">{emptyMessage}</p>;
  }

  return (
    <div className="overflow-x-auto">
      <table className="w-full min-w-full text-left text-sm">
        <thead>
          <tr className="border-b border-black/10 text-xs uppercase tracking-wide text-neutral-500 dark:border-white/15 dark:text-neutral-400">
            {columns.map((column) => (
              <th key={column.header} scope="col" className="px-3 py-2 font-medium">
                {column.header}
              </th>
            ))}
            <th scope="col" className="px-3 py-2 font-medium">
              Status
            </th>
            {canWrite ? (
              <th scope="col" className="px-3 py-2 font-medium">
                <span className="sr-only">Actions</span>
              </th>
            ) : null}
          </tr>
        </thead>
        <tbody>
          {items.map((item) => (
            <tr key={item.id} className="border-b border-black/5 last:border-0 dark:border-white/10">
              {columns.map((column) => (
                <td key={column.header} className="px-3 py-2">
                  {column.render(item)}
                </td>
              ))}
              <td className="px-3 py-2">
                <StatusBadge isActive={item.isActive} />
              </td>
              {canWrite ? (
                <td className="px-3 py-2">
                  <Button
                    type="button"
                    variant={item.isActive ? "danger" : "secondary"}
                    className="px-2 py-1 text-xs"
                    disabled={togglingId === item.id}
                    onClick={() => onToggleActive(item)}
                  >
                    {togglingId === item.id ? "Saving…" : item.isActive ? "Suspend" : "Activate"}
                  </Button>
                </td>
              ) : null}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
