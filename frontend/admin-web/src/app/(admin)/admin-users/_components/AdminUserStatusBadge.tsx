"use client";

import { Badge } from "@/components/ui";
import { AdminUserStatus } from "@/lib/admin-users-types";

/**
 * Account state for a back-office operator (SRS 12.2.1), mirroring
 * `components/status-badges.tsx`'s treatment of the other admin domain enums.
 *
 * Lockout is a *separate* condition from Inactive — an account can be Active
 * and locked out after repeated failed sign-ins — so it renders as its own
 * pill. The list previously rendered this as the bare string
 * `"Active (locked)"`, which read as a fifth status rather than two facts.
 */
export function AdminUserStatusBadge({
  status,
  isLockedOut,
}: {
  status: AdminUserStatus;
  isLockedOut: boolean;
}) {
  return (
    <span className="inline-flex flex-wrap items-center gap-1.5">
      <Badge tone={status === AdminUserStatus.Active ? "success" : "neutral"}>
        {status === AdminUserStatus.Active ? "Active" : "Inactive"}
      </Badge>
      {isLockedOut ? <Badge tone="warning">Locked out</Badge> : null}
    </span>
  );
}
