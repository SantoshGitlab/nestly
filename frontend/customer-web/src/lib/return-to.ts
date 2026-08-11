/**
 * Carrying the customer's destination through the sign-in wall.
 *
 * Every authenticated screen used to send a signed-out visitor to a bare
 * `/login` and every successful sign-in landed on `/profile`, so someone one
 * click from placing a booking was dropped onto a form asking for their date
 * of birth, with everything they had chosen gone. The guard also redirected
 * with `router.replace`, which removed the destination from history, so Back
 * could not recover it either.
 */

/** Query parameter carrying the path to return to after signing in. */
export const RETURN_TO_PARAM = "next";

/**
 * Where sign-in lands when there is no pending destination. The home page,
 * not the profile form - dropping a customer who just signed in with
 * nothing pending straight onto a form asking for their date of birth reads
 * as unfinished, not welcoming.
 */
export const DEFAULT_POST_LOGIN_PATH = "/";

/**
 * Whether a return-to value is a path on this site.
 *
 * Anything else is discarded: the value arrives in a URL the customer can be
 * handed by someone else, and following it unchecked after a successful
 * sign-in is a textbook open redirect. Protocol-relative (`//evil.example`)
 * and backslash (`/\evil.example`) forms are rejected alongside absolute URLs
 * because browsers normalise them to another origin.
 */
export function isSafeReturnPath(value: string | null | undefined): value is string {
  if (!value || !value.startsWith("/")) return false;
  if (value.startsWith("//") || value.startsWith("/\\")) return false;
  return true;
}

/** `/login` carrying the current location, so sign-in can come back to it. */
export function buildLoginHref(pathname: string, search?: string): string {
  const target = `${pathname}${search ?? ""}`;
  if (!isSafeReturnPath(target) || target === "/login") return "/login";
  return `/login?${RETURN_TO_PARAM}=${encodeURIComponent(target)}`;
}

/** Where to send the customer once signed in - their pending destination, or the default. */
export function resolvePostLoginPath(returnTo: string | null | undefined): string {
  return isSafeReturnPath(returnTo) ? returnTo : DEFAULT_POST_LOGIN_PATH;
}
