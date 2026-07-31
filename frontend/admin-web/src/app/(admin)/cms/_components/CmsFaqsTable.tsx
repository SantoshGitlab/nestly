"use client";

import { Button } from "@/components/ui";
import { CmsContentStatus, type CmsFaqResponse } from "@/lib/cms-types";
import { formatPlacement } from "./cmsDisplay";

function formatSchedule(faq: CmsFaqResponse): string {
  if (!faq.publishStartUtc && !faq.publishEndUtc) return "Always";
  const start = faq.publishStartUtc ? new Date(faq.publishStartUtc).toLocaleString() : "now";
  const end = faq.publishEndUtc ? new Date(faq.publishEndUtc).toLocaleString() : "indefinitely";
  return `${start} – ${end}`;
}

export function CmsFaqsTable({
  faqs,
  isLoading,
  errorMessage,
  canWrite,
  onEdit,
  onTogglePublished,
  togglingId,
}: {
  faqs: CmsFaqResponse[] | undefined;
  isLoading: boolean;
  errorMessage: string | null;
  canWrite: boolean;
  onEdit: (faq: CmsFaqResponse) => void;
  onTogglePublished: (faq: CmsFaqResponse) => void;
  togglingId?: string;
}) {
  if (isLoading) {
    return <p className="text-sm text-neutral-600 dark:text-neutral-400">Loading…</p>;
  }

  if (errorMessage) {
    return <p className="text-sm text-red-600 dark:text-red-400">{errorMessage}</p>;
  }

  if (!faqs || faqs.length === 0) {
    return <p className="text-sm text-neutral-600 dark:text-neutral-400">No FAQs match the current filters.</p>;
  }

  return (
    <div className="overflow-x-auto">
      <table className="w-full min-w-full text-left text-sm">
        <thead>
          <tr className="border-b border-black/10 text-xs uppercase tracking-wide text-neutral-500 dark:border-white/15 dark:text-neutral-400">
            <th scope="col" className="px-3 py-2 font-medium">Question</th>
            <th scope="col" className="px-3 py-2 font-medium">Placement</th>
            <th scope="col" className="px-3 py-2 font-medium">Sort</th>
            <th scope="col" className="px-3 py-2 font-medium">Schedule</th>
            <th scope="col" className="px-3 py-2 font-medium">Status</th>
            <th scope="col" className="px-3 py-2 font-medium">
              <span className="sr-only">Actions</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {faqs.map((faq) => (
            <tr key={faq.id} className="border-b border-black/5 last:border-0 dark:border-white/10">
              <td className="px-3 py-2 font-medium">{faq.question}</td>
              <td className="px-3 py-2">{formatPlacement(faq.placement)}</td>
              <td className="px-3 py-2">{faq.sortOrder}</td>
              <td className="px-3 py-2">{formatSchedule(faq)}</td>
              <td className="px-3 py-2">
                <span
                  className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${
                    faq.status === CmsContentStatus.Published
                      ? "bg-green-100 text-green-800 dark:bg-green-950 dark:text-green-200"
                      : "bg-neutral-200 text-neutral-700 dark:bg-neutral-800 dark:text-neutral-300"
                  }`}
                >
                  {faq.status === CmsContentStatus.Published ? "Published" : "Draft"}
                </span>
              </td>
              <td className="px-3 py-2">
                {canWrite ? (
                  <div className="flex flex-wrap gap-2">
                    <Button type="button" variant="secondary" className="px-2 py-1 text-xs" onClick={() => onEdit(faq)}>
                      Edit
                    </Button>
                    <Button
                      type="button"
                      variant={faq.status === CmsContentStatus.Published ? "danger" : "secondary"}
                      className="px-2 py-1 text-xs"
                      disabled={togglingId === faq.id}
                      onClick={() => onTogglePublished(faq)}
                    >
                      {togglingId === faq.id
                        ? "Saving…"
                        : faq.status === CmsContentStatus.Published
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
