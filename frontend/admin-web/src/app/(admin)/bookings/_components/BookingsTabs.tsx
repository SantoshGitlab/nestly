"use client";

import { NavTabs } from "@/components/nav-tabs";

/**
 * Sub-nav for the Bookings module. Recurring plans live here rather than as
 * their own sidebar entry because they are gated by the same "bookings.read"
 * permission and describe the same domain - see RecurringPlansController's doc
 * comment. A sidebar entry would imply a module of its own, which is exactly
 * the RBAC split task 299 concluded against.
 */
export function BookingsTabs() {
  return (
    <NavTabs
      label="Booking sections"
      tabs={[
        { href: "/bookings", label: "All bookings" },
        { href: "/bookings/recurring-plans", label: "Recurring plans" },
      ]}
    />
  );
}
