"use client";

import type { LoginResponse } from "./types";

/**
 * Client-side session storage.
 *
 * Known limitation: tokens held in Web Storage are readable by any script on
 * the origin, so an XSS bug becomes a session-theft bug. The backend currently
 * returns the token pair in the response body (see AuthController), so there
 * is no httpOnly cookie to use instead. sessionStorage rather than
 * localStorage narrows the window: the session dies with the browser tab.
 * Moving issuance to a Set-Cookie header is the real fix and is tracked as
 * hardening work, not something this client can do on its own.
 */
const ACCESS_TOKEN_KEY = "nestly.accessToken";
const REFRESH_TOKEN_KEY = "nestly.refreshToken";
const EXPIRES_AT_KEY = "nestly.accessTokenExpiresAt";

/** Notifies subscribed components (the header, guards) that auth state moved. */
const AUTH_CHANGED_EVENT = "nestly:auth-changed";

function isBrowser(): boolean {
  return typeof window !== "undefined";
}

export function storeSession(session: LoginResponse): void {
  if (!isBrowser()) return;
  sessionStorage.setItem(ACCESS_TOKEN_KEY, session.accessToken);
  sessionStorage.setItem(REFRESH_TOKEN_KEY, session.refreshToken);
  sessionStorage.setItem(EXPIRES_AT_KEY, session.accessTokenExpiresAtUtc);
  window.dispatchEvent(new Event(AUTH_CHANGED_EVENT));
}

export function clearSession(): void {
  if (!isBrowser()) return;
  sessionStorage.removeItem(ACCESS_TOKEN_KEY);
  sessionStorage.removeItem(REFRESH_TOKEN_KEY);
  sessionStorage.removeItem(EXPIRES_AT_KEY);
  window.dispatchEvent(new Event(AUTH_CHANGED_EVENT));
}

export function getAccessToken(): string | null {
  if (!isBrowser()) return null;
  return sessionStorage.getItem(ACCESS_TOKEN_KEY);
}

export function getRefreshToken(): string | null {
  if (!isBrowser()) return null;
  return sessionStorage.getItem(REFRESH_TOKEN_KEY);
}

/** True only when a token is present *and* has not already expired. */
export function isAuthenticated(): boolean {
  const token = getAccessToken();
  if (!token) return false;

  const expiresAt = sessionStorage.getItem(EXPIRES_AT_KEY);
  if (!expiresAt) return false;

  // The backend serialises the expiry as UTC; append the marker when it is
  // missing so Date does not read it as local time and over-report validity.
  const normalised = /[Zz]|[+-]\d{2}:\d{2}$/.test(expiresAt)
    ? expiresAt
    : `${expiresAt}Z`;

  return new Date(normalised).getTime() > Date.now();
}

export function subscribeToAuthChanges(listener: () => void): () => void {
  if (!isBrowser()) return () => undefined;
  window.addEventListener(AUTH_CHANGED_EVENT, listener);
  return () => window.removeEventListener(AUTH_CHANGED_EVENT, listener);
}
