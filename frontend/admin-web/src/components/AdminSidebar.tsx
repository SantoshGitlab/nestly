"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { getVisibleNavModules } from "@/lib/permissions";
import type { AdminSessionClaims } from "@/lib/types";

/**
 * Sidebar nav, filtered by the current admin's role/permissions (task 98c).
 * See lib/permissions.ts for the filtering rule and why most of these links
 * currently 404 - the pages are separate, not-yet-implemented tasks.
 */
export function AdminSidebar({ claims }: { claims: AdminSessionClaims | null }) {
  const pathname = usePathname();
  const modules = getVisibleNavModules(claims);

  return (
    <nav
      aria-label="Admin sections"
      className="flex w-60 shrink-0 flex-col gap-1 border-r border-black/10 p-4 dark:border-white/15"
    >
      {modules.map((module) => {
        const isActive = pathname === module.href || pathname.startsWith(`${module.href}/`);
        return (
          <Link
            key={module.key}
            href={module.href}
            aria-current={isActive ? "page" : undefined}
            className={`rounded-lg px-3 py-2 text-sm font-medium transition-colors ${
              isActive
                ? "bg-black text-white dark:bg-white dark:text-black"
                : "hover:bg-black/5 dark:hover:bg-white/10"
            }`}
          >
            {module.label}
          </Link>
        );
      })}
    </nav>
  );
}
