"use client";

import { useState } from "react";
import type { ReactNode } from "react";
import { Button } from "@/components/ui";
import { ActiveBadge, ConfirmDialog, DataTable } from "@/components/data-table";
import type { DataTableColumn } from "@/components/data-table";
import { describeError } from "@/lib/api";

/**
 * The "master list with an active/inactive toggle" shape (task 222).
 *
 * Roughly a dozen admin screens — every serviceability geography master and
 * mapping, the three catalog lists, promotional prices — reduce to "columns of
 * data, a status pill, and a suspend/activate control". This renders that shape
 * once, on top of `DataTable`, so those screens get the three-state contract
 * (layout-matching skeleton, empty state with a next action, error state with a
 * working Retry) without each re-deriving it.
 *
 * It replaces the previous `serviceability/_components/EntityTable`, which
 * rendered a bare `<table>`, a "Loading…" paragraph, a raw red error string and
 * a hand-rolled green/neutral status pill built from `neutral-*`/`green-*`
 * classes rather than tokens. It also lived under `serviceability/_components`
 * while being imported from `catalog` and `pricing` — hence the move to
 * `src/components`.
 *
 * Deactivation goes through `ConfirmDialog`: suspending a city, a service or a
 * pincode instantly removes it from what customers can book, and it was
 * previously a single unconfirmed click. Activation is not confirmed — it is
 * not destructive.
 */

export interface EntityTableColumn<T> {
  header: string;
  render: (item: T) => ReactNode;
  /** Right-aligns and applies tabular figures — use for every column of amounts. */
  numeric?: boolean;
  /** Extra classes on the cell (widths, truncation). */
  className?: string;
  /**
   * Makes the column sortable. Safe here because these lists are unpaged —
   * the table holds the whole data set. Do NOT add it to a server-paged list.
   */
  sortValue?: (item: T) => string | number | boolean | null | undefined;
}

/** Active/suspended pill for the detail screens, matching the list's column. */
export function StatusBadge({ isActive }: { isActive: boolean }) {
  return <ActiveBadge active={isActive} inactiveLabel="Suspended" />;
}

export function EntityTable<T extends { id: string; isActive: boolean }>({
  items,
  columns,
  isLoading,
  isFetching = false,
  errorMessage,
  error,
  onRetry,
  emptyMessage,
  emptyAction,
  canWrite,
  onToggleActive,
  togglingId,
  toggleError,
  title,
  description,
  actions,
  entityLabel = "record",
  labelOf,
  minWidth,
  skeletonRows = 5,
  hideDensityToggle = false,
  footer,
}: {
  items: T[] | undefined;
  columns: readonly EntityTableColumn<T>[];
  isLoading: boolean;
  /** Background refetch — dims the body rather than replacing it. */
  isFetching?: boolean;
  errorMessage?: string | null;
  /** Pass `query.error` straight through as an alternative to `errorMessage`. */
  error?: unknown;
  onRetry?: () => void;
  emptyMessage: string;
  /** The next step out of an empty list. */
  emptyAction?: ReactNode;
  /** Whether the current admin holds the module's write permission (SRS 12.2.3) - hides mutating controls otherwise. */
  canWrite: boolean;
  onToggleActive: (item: T) => void;
  /** Id of the row currently being toggled, to disable just that row's button rather than the whole table. */
  togglingId?: string;
  /** Failure of the toggle — keeps the confirmation open with the reason. */
  toggleError?: unknown;
  title?: string;
  description?: string;
  actions?: ReactNode;
  /** Used in the confirmation copy: "Suspend this city?". */
  entityLabel?: string;
  /** Human name of a row, shown in the confirmation so the admin sees what they are acting on. */
  labelOf?: (item: T) => string;
  minWidth?: string;
  skeletonRows?: number;
  hideDensityToggle?: boolean;
  footer?: ReactNode;
}) {
  const [pendingSuspend, setPendingSuspend] = useState<T | null>(null);

  const tableColumns: DataTableColumn<T>[] = [
    ...columns.map((column) => ({
      key: column.header,
      header: column.header,
      cell: column.render,
      numeric: column.numeric,
      className: column.className,
      sortValue: column.sortValue,
    })),
    {
      key: "__status",
      header: "Status",
      cell: (item: T) => <StatusBadge isActive={item.isActive} />,
      sortValue: (item: T) => item.isActive,
    },
  ];

  const busy = pendingSuspend ? togglingId === pendingSuspend.id : false;

  return (
    <>
      <DataTable<T>
        title={title}
        description={description}
        actions={actions}
        columns={tableColumns}
        rows={items}
        rowKey={(item) => item.id}
        isLoading={isLoading}
        isFetching={isFetching}
        error={error}
        errorMessage={errorMessage}
        onRetry={onRetry}
        emptyTitle={emptyMessage}
        emptyDescription={
          canWrite ? undefined : "An admin with write access to this module can add one."
        }
        emptyAction={emptyAction}
        minWidth={minWidth}
        skeletonRows={skeletonRows}
        hideDensityToggle={hideDensityToggle}
        footer={footer}
        rowActions={
          canWrite
            ? (item) => (
                <Button
                  type="button"
                  size="sm"
                  variant={item.isActive ? "secondary" : "subtle"}
                  disabled={togglingId === item.id}
                  loading={togglingId === item.id && !pendingSuspend}
                  onClick={() => {
                    if (item.isActive) {
                      setPendingSuspend(item);
                    } else {
                      onToggleActive(item);
                    }
                  }}
                >
                  {item.isActive ? "Suspend" : "Activate"}
                </Button>
              )
            : undefined
        }
      />

      <ConfirmDialog
        open={pendingSuspend !== null}
        title={`Suspend this ${entityLabel}?`}
        description="It stops being available to customers immediately. You can activate it again at any time."
        confirmLabel="Suspend"
        cancelLabel="Keep active"
        loading={busy}
        error={toggleError ? describeError(toggleError) : null}
        onCancel={() => setPendingSuspend(null)}
        onConfirm={() => {
          if (!pendingSuspend) return;
          onToggleActive(pendingSuspend);
          setPendingSuspend(null);
        }}
      >
        {pendingSuspend && labelOf ? (
          <p className="text-sm text-fg-muted">
            Suspending <span className="font-medium text-fg">{labelOf(pendingSuspend)}</span>.
          </p>
        ) : null}
      </ConfirmDialog>
    </>
  );
}
