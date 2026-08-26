"use client";

import { motion } from "motion/react";
import { Children } from "react";
import type { ReactNode } from "react";
import { Reveal, revealItem } from "@/components/motion";
import { Button, Card, Skeleton } from "@/components/ui";
import { SectionError } from "@/components/screen-states";

/**
 * One report on the reports screen, with the three-state contract applied
 * consistently (task 222).
 *
 * Every card here previously rendered its own `Loading…` paragraph and a bare
 * `<Alert>` with no way to retry, so a single transient failure left that
 * report permanently blank until the admin reloaded the browser. This renders
 * a skeleton shaped like the card's real content, an error with a working
 * Retry, and keeps the CSV export button out of reach while there is nothing
 * to export.
 */
export function ReportCard({
  title,
  description,
  isLoading,
  error,
  onRetry,
  skeleton,
  onExport,
  exporting = false,
  children,
}: {
  title: string;
  description: string;
  isLoading: boolean;
  error: unknown;
  onRetry: () => void;
  /** Loading placeholder — must match the loaded layout or the card jumps. */
  skeleton: ReactNode;
  /** Omit to render a report with no CSV export. */
  onExport?: () => void;
  exporting?: boolean;
  children: ReactNode;
}) {
  const failed = Boolean(error);

  return (
    <Card
      title={title}
      description={description}
      actions={
        onExport ? (
          <Button
            type="button"
            size="sm"
            variant="secondary"
            loading={exporting}
            // Exporting a report that failed to load, or has not loaded yet,
            // can only produce a confusing file or a second error.
            disabled={isLoading || failed}
            onClick={onExport}
          >
            Export CSV
          </Button>
        ) : undefined
      }
    >
      {isLoading ? skeleton : failed ? <SectionError error={error} onRetry={onRetry} /> : children}
    </Card>
  );
}

/** Responsive row of `StatTile`s — the shape every report's headline numbers use. */
export function StatGrid({ children, columns = 3 }: { children: ReactNode; columns?: 2 | 3 }) {
  return (
    <Reveal
      className={
        columns === 2
          ? "grid grid-cols-1 gap-4 sm:grid-cols-2"
          : "grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3"
      }
    >
      {Children.map(children, (child) => (
        <motion.div variants={revealItem}>{child}</motion.div>
      ))}
    </Reveal>
  );
}

/** Matches a `StatTile`'s height so the tiles do not jump when the numbers land. */
export function StatTileSkeleton() {
  return (
    <div className="rounded-2xl bg-surface p-5 shadow-sm">
      <Skeleton className="h-4 w-32" />
      <Skeleton className="mt-3 h-9 w-24" />
    </div>
  );
}

/** Loading placeholder for a card whose body is a row of `count` stat tiles. */
export function StatGridSkeleton({ count = 3, columns = 3 }: { count?: number; columns?: 2 | 3 }) {
  return (
    <StatGrid columns={columns}>
      {Array.from({ length: count }, (_, index) => (
        <StatTileSkeleton key={index} />
      ))}
    </StatGrid>
  );
}

/**
 * Loading placeholder shaped like the `DataTable` breakdowns that sit under
 * the tiles. Without it those cards grew by several hundred pixels the moment
 * their rows arrived, pushing the rest of the page down under the cursor.
 */
export function TableSkeleton({ rows = 4 }: { rows?: number }) {
  return (
    <div className="overflow-hidden rounded-2xl bg-surface shadow-sm">
      <div className="border-b border-line px-4 py-3">
        <Skeleton className="h-4 w-32" />
      </div>
      <div className="divide-y divide-line">
        {Array.from({ length: rows }, (_, index) => (
          <div key={index} className="flex items-center gap-4 px-4 py-3">
            <Skeleton className="h-4 w-40" />
            <Skeleton className="ml-auto h-4 w-16" />
          </div>
        ))}
      </div>
    </div>
  );
}
