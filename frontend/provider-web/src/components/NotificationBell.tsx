"use client";

import { useEffect, useRef, useState } from "react";
import { cx } from "@/components/ui";

/**
 * Header notification bell + popup, matched to the MatDash reference
 * (matdash-nextjs-minisidebar.vercel.app dashboard header's bell menu):
 * 360px panel, "N new" pill next to the title, 44px tinted icon chips per
 * row, title/description/time layout, "See all" footer.
 *
 * There is no backend concept yet of a notification feed belonging to the
 * signed-in admin/provider (only customer-facing `NotificationTemplate`s
 * exist - see NotificationTemplatesController). Rather than invent one or
 * fabricate placeholder rows, this ships as the UI shell only: the badge
 * only renders once `unreadCount` is genuinely nonzero, and the panel shows
 * an honest empty state until a real feed is wired in via `notifications`.
 */
export interface HeaderNotification {
  id: string;
  title: string;
  description: string;
  time: string;
  href?: string;
  tone?: "brand" | "success" | "warning" | "danger" | "info";
}

const TONE_CHIP = {
  brand: "bg-brand-50 text-brand-600 dark:bg-brand-500/15 dark:text-brand-300",
  success: "bg-success-soft text-success",
  warning: "bg-warning-soft text-warning",
  danger: "bg-danger-soft text-danger",
  info: "bg-info-soft text-info",
} as const;

function BellIcon({ className }: { className?: string }) {
  return (
    <svg viewBox="0 0 24 24" fill="none" className={className} aria-hidden>
      <path
        d="M18.75 9.71V9c0-3.87-3.02-7-6.75-7S5.25 5.13 5.25 9v.71c0 .85-.24 1.67-.69 2.38l-1.11 1.72c-1.01 1.58-.24 3.72 1.53 4.21a26.6 26.6 0 0 0 14.04 0c1.76-.49 2.53-2.63 1.52-4.21l-1.11-1.72a4.3 4.3 0 0 1-.68-2.38Z"
        stroke="currentColor"
        strokeWidth="1.5"
      />
      <path
        d="M7.5 19c.66 1.75 2.42 3 4.5 3s3.84-1.25 4.5-3M12 6v4"
        stroke="currentColor"
        strokeWidth="1.5"
        strokeLinecap="round"
        opacity=".5"
      />
    </svg>
  );
}

export function NotificationBell({
  notifications = [],
  unreadCount = 0,
}: {
  notifications?: HeaderNotification[];
  unreadCount?: number;
}) {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;

    const onPointerDown = (event: MouseEvent) => {
      if (!ref.current?.contains(event.target as Node)) setOpen(false);
    };
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") setOpen(false);
    };

    document.addEventListener("mousedown", onPointerDown);
    document.addEventListener("keydown", onKeyDown);
    return () => {
      document.removeEventListener("mousedown", onPointerDown);
      document.removeEventListener("keydown", onKeyDown);
    };
  }, [open]);

  return (
    <div ref={ref} className="relative">
      <button
        type="button"
        onClick={() => setOpen((current) => !current)}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-label="Notifications"
        className="relative inline-flex h-10 w-10 items-center justify-center rounded-full text-fg-muted transition-colors duration-fast ease-out hover:bg-surface-3 hover:text-fg"
      >
        <BellIcon className="h-5 w-5" />
        {unreadCount > 0 ? (
          <span className="absolute right-1 top-1 flex h-4 min-w-4 items-center justify-center rounded-full bg-danger px-1 text-[10px] font-semibold text-white">
            {unreadCount > 9 ? "9+" : unreadCount}
          </span>
        ) : null}
      </button>

      {open ? (
        <div
          role="menu"
          aria-label="Notifications"
          className="absolute right-0 top-full z-50 mt-2 w-[360px] max-w-[calc(100vw-2rem)] animate-pop overflow-hidden rounded-sm bg-surface py-6 shadow-sm"
        >
          <div className="flex items-center justify-between px-6">
            <h3 className="text-lg font-semibold text-fg">Notifications</h3>
            {unreadCount > 0 ? (
              <span className="rounded-full bg-brand-600 px-2.5 py-0.5 text-xs font-semibold text-fg-on-brand">
                {unreadCount} new
              </span>
            ) : null}
          </div>

          <div className="mt-3 max-h-80 overflow-y-auto">
            {notifications.length === 0 ? (
              <p className="px-6 py-10 text-center text-sm text-fg-muted">No notifications yet</p>
            ) : (
              notifications.map((item) => (
                <a
                  key={item.id}
                  href={item.href ?? "#"}
                  role="menuitem"
                  className="flex w-full items-center gap-4 px-6 py-3 transition-colors duration-fast ease-out hover:bg-surface-2"
                >
                  <span
                    className={cx(
                      "flex h-11 w-11 shrink-0 items-center justify-center rounded-full",
                      TONE_CHIP[item.tone ?? "brand"],
                    )}
                  >
                    <BellIcon className="h-5 w-5" />
                  </span>
                  <span className="flex w-full items-start justify-between gap-2">
                    <span className="min-w-0">
                      <span className="block text-[0.9375rem] font-semibold text-fg">{item.title}</span>
                      <span className="line-clamp-1 block text-sm text-fg-muted">{item.description}</span>
                    </span>
                    <span className="shrink-0 pt-0.5 text-xs text-fg-subtle">{item.time}</span>
                  </span>
                </a>
              ))
            )}
          </div>
        </div>
      ) : null}
    </div>
  );
}
