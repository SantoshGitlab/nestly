"use client";

import { Button } from "@/components/ui";
import { CmsContentStatus, type CmsPageResponse } from "@/lib/cms-types";
import { formatPlacement } from "./cmsDisplay";

function formatSchedule(page: CmsPageResponse): string {
  if (!page.publishStartUtc && !page.publishEndUtc) return "Always";
  const start = page.publishStartUtc ? new Date(page.publishStartUtc).toLocaleString() : "now";
  const end = page.publishEndUtc ? new Date(page.publishEndUtc).toLocaleString() : "indefinitely";
  return `${start} – ${end}`;
}

export function CmsPagesTable({
  pages,
  isLoading,
  errorMessage,
  canWrite,
  onEdit,
  onTogglePublished,
  togglingId,
}: {
  pages: CmsPageResponse[] | undefined;
  isLoading: boolean;
  errorMessage: string | null;
  canWrite: boolean;
  onEdit: (page: CmsPageResponse) => void;
  onTogglePublished: (page: CmsPageResponse) => void;
  togglingId?: string;
}) {
  if (isLoading) {
    return <p className="text-sm text-neutral-600 dark:text-neutral-400">Loading…</p>;
  }

  if (errorMessage) {
    return <p className="text-sm text-red-600 dark:text-red-400">{errorMessage}</p>;
  }

  if (!pages || pages.length === 0) {
    return <p className="text-sm text-neutral-600 dark:text-neutral-400">No pages match the current filters.</p>;
  }

  return (
    <div className="overflow-x-auto">
      <table className="w-full min-w-full text-left text-sm">
        <thead>
          <tr className="border-b border-black/10 text-xs uppercase tracking-wide text-neutral-500 dark:border-white/15 dark:text-neutral-400">
            <th scope="col" className="px-3 py-2 font-medium">Title</th>
            <th scope="col" className="px-3 py-2 font-medium">Slug</th>
            <th scope="col" className="px-3 py-2 font-medium">Placement</th>
            <th scope="col" className="px-3 py-2 font-medium">Schedule</th>
            <th scope="col" className="px-3 py-2 font-medium">Status</th>
            <th scope="col" className="px-3 py-2 font-medium">
              <span className="sr-only">Actions</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {pages.map((page) => (
            <tr key={page.id} className="border-b border-black/5 last:border-0 dark:border-white/10">
              <td className="px-3 py-2 font-medium">{page.title}</td>
              <td className="px-3 py-2 text-neutral-600 dark:text-neutral-400">/{page.slug}</td>
              <td className="px-3 py-2">{formatPlacement(page.placement)}</td>
              <td className="px-3 py-2">{formatSchedule(page)}</td>
              <td className="px-3 py-2">
                <span
                  className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${
                    page.status === CmsContentStatus.Published
                      ? "bg-green-100 text-green-800 dark:bg-green-950 dark:text-green-200"
                      : "bg-neutral-200 text-neutral-700 dark:bg-neutral-800 dark:text-neutral-300"
                  }`}
                >
                  {page.status === CmsContentStatus.Published ? "Published" : "Draft"}
                </span>
              </td>
              <td className="px-3 py-2">
                {canWrite ? (
                  <div className="flex flex-wrap gap-2">
                    <Button type="button" variant="secondary" className="px-2 py-1 text-xs" onClick={() => onEdit(page)}>
                      Edit
                    </Button>
                    <Button
                      type="button"
                      variant={page.status === CmsContentStatus.Published ? "danger" : "secondary"}
                      className="px-2 py-1 text-xs"
                      disabled={togglingId === page.id}
                      onClick={() => onTogglePublished(page)}
                    >
                      {togglingId === page.id
                        ? "Saving…"
                        : page.status === CmsContentStatus.Published
                          ? "Unpublish"
                          : "Publish"}
                    </Button>
                  </div>
                ) : null}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
