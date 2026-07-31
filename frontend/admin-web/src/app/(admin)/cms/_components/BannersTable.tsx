"use client";

import { Button } from "@/components/ui";
import { CmsContentStatus, type BannerResponse } from "@/lib/cms-types";
import { formatPlacement } from "./cmsDisplay";

function formatSchedule(banner: BannerResponse): string {
  if (!banner.publishStartUtc && !banner.publishEndUtc) return "Always";
  const start = banner.publishStartUtc ? new Date(banner.publishStartUtc).toLocaleString() : "now";
  const end = banner.publishEndUtc ? new Date(banner.publishEndUtc).toLocaleString() : "indefinitely";
  return `${start} – ${end}`;
}

export function BannersTable({
  banners,
  isLoading,
  errorMessage,
  canWrite,
  onEdit,
  onTogglePublished,
  togglingId,
}: {
  banners: BannerResponse[] | undefined;
  isLoading: boolean;
  errorMessage: string | null;
  canWrite: boolean;
  onEdit: (banner: BannerResponse) => void;
  onTogglePublished: (banner: BannerResponse) => void;
  togglingId?: string;
}) {
  if (isLoading) {
    return <p className="text-sm text-neutral-600 dark:text-neutral-400">Loading…</p>;
  }

  if (errorMessage) {
    return <p className="text-sm text-red-600 dark:text-red-400">{errorMessage}</p>;
  }

  if (!banners || banners.length === 0) {
    return <p className="text-sm text-neutral-600 dark:text-neutral-400">No banners match the current filters.</p>;
  }

  return (
    <div className="overflow-x-auto">
      <table className="w-full min-w-full text-left text-sm">
        <thead>
          <tr className="border-b border-black/10 text-xs uppercase tracking-wide text-neutral-500 dark:border-white/15 dark:text-neutral-400">
            <th scope="col" className="px-3 py-2 font-medium">Title</th>
            <th scope="col" className="px-3 py-2 font-medium">Image</th>
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
          {banners.map((banner) => (
            <tr key={banner.id} className="border-b border-black/5 last:border-0 dark:border-white/10">
              <td className="px-3 py-2 font-medium">{banner.title}</td>
              <td className="px-3 py-2">
                <a
                  href={banner.mediaUrl}
                  target="_blank"
                  rel="noreferrer"
                  className="text-neutral-600 underline hover:text-black dark:text-neutral-400 dark:hover:text-white"
                >
                  View
                </a>
              </td>
              <td className="px-3 py-2">
                {formatPlacement(banner.placement)}
                {banner.categoryName ? ` (${banner.categoryName})` : ""}
              </td>
              <td className="px-3 py-2">{banner.sortOrder}</td>
              <td className="px-3 py-2">{formatSchedule(banner)}</td>
              <td className="px-3 py-2">
                <span
                  className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${
                    banner.status === CmsContentStatus.Published
                      ? "bg-green-100 text-green-800 dark:bg-green-950 dark:text-green-200"
                      : "bg-neutral-200 text-neutral-700 dark:bg-neutral-800 dark:text-neutral-300"
                  }`}
                >
                  {banner.status === CmsContentStatus.Published ? "Published" : "Draft"}
                </span>
              </td>
              <td className="px-3 py-2">
                {canWrite ? (
                  <div className="flex flex-wrap gap-2">
                    <Button type="button" variant="secondary" className="px-2 py-1 text-xs" onClick={() => onEdit(banner)}>
                      Edit
                    </Button>
                    <Button
                      type="button"
                      variant={banner.status === CmsContentStatus.Published ? "danger" : "secondary"}
                      className="px-2 py-1 text-xs"
                      disabled={togglingId === banner.id}
                      onClick={() => onTogglePublished(banner)}
                    >
                      {togglingId === banner.id
                        ? "Saving…"
                        : banner.status === CmsContentStatus.Published
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
