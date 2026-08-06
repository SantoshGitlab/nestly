/**
 * Booking draft persistence (task 228).
 *
 * Quantity, add-ons, address, date and slot are plain component state on the
 * booking summary screen with nothing else backing them, so navigating away
 * mid-review used to wipe every selection silently - most commonly via the
 * "+ Add a new address" link, which leaves for /addresses/new and always
 * redirects to /addresses on save rather than back, so even a single detour to
 * add an address cost the customer their quantity/add-on choices.
 *
 * The draft also survives *creating* the booking, and carries the resulting
 * `pendingBookingId` from then until payment succeeds. Clearing it at creation
 * time meant a customer who pressed Back from the payment screen - to re-check
 * the address before paying, the most ordinary hesitation there is - found a
 * blank page, had to re-pick everything, and got a second, separate booking
 * for their trouble while the first sat unpaid holding a slot.
 *
 * Lives in sessionStorage: the draft is scoped to the tab the customer is
 * booking in, and should not outlive it. It is only ever read after mount,
 * never during the initial render, so server-rendered markup and the client's
 * first paint stay identical - sessionStorage doesn't exist on the server
 * anyway, same reasoning as lib/location.ts's isBrowser guard.
 */
export interface BookingDraft {
  quantity: number;
  addOnIds: string[];
  addressId: string | null;
  date: string;
  slotWindowId: string | null;
  /** Set once a booking has been created from this draft but not yet paid for. */
  pendingBookingId?: string | null;
}

function draftKey(serviceSlug: string): string {
  return `nestly.booking-draft.${serviceSlug}`;
}

export function readDraft(serviceSlug: string): BookingDraft | null {
  if (typeof window === "undefined") return null;
  try {
    const raw = sessionStorage.getItem(draftKey(serviceSlug));
    return raw ? (JSON.parse(raw) as BookingDraft) : null;
  } catch {
    return null;
  }
}

export function writeDraft(serviceSlug: string, draft: BookingDraft): void {
  if (typeof window === "undefined") return;
  try {
    sessionStorage.setItem(draftKey(serviceSlug), JSON.stringify(draft));
  } catch {
    // Storage can be unavailable (private mode, blocked cookies) - the
    // booking still works, it just won't survive a detour off this page.
  }
}

export function clearDraft(serviceSlug: string): void {
  if (typeof window === "undefined") return;
  try {
    sessionStorage.removeItem(draftKey(serviceSlug));
  } catch {
    // See above.
  }
}
