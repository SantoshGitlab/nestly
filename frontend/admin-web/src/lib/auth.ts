"use client";

import { decodeJwtPayload } from "./jwt";
import type { AdminLoginResponse, AdminSessionClaims } from "./types";

/**
 * Client-side session storage for the admin panel.
 *
 * Mirrors customer-web/src/lib/auth.ts for consistency across the two
 * frontends (see docs/FRONTEND.md). Same known limitation applies: tokens in
 * Web Storage are readable by any script on the origin, so an XSS bug
 * becomes a session-theft bug. sessionStorage (not localStorage) narrows the
 * window - the session dies with the browser tab. Moving issuance to an
 * httpOnly cookie is real hardening work, tracked separately, not something
 * this client can do unilaterally.
 *
 * Keys are namespaced "nestly.admin.*" (distinct from customer-web's
 * "nestly.*") so the two apps never collide if ever inspected side by side.
 */
const ACCESS_TOKEN_KEY = "nestly.admin.accessToken";
const REFRESH_TOKEN_KEY = "nestly.admin.refreshToken";
const EXPIRES_AT_KEY = "nestly.admin.accessTokenExpiresAt";

/** Notifies subscribed components (layout guard, header) that auth state moved. */
const AUTH_CHANGED_EVENT = "nestly-admin:auth-changed";

function isBrowser(): boolean {
  return typeof window !== "undefined";
}

export function storeSession(session: AdminLoginResponse): void {
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

  // The backend is expected to serialise the expiry as UTC; append the
  // marker when it is missing so Date does not read it as local time and
  // over-report validity (same defensive parsing as customer-web).
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

/**
 * Raw JWT payload shape, defensive about which claim key the token actually
 * uses - `AdminTokenService`'s exact claim names are not yet confirmed on
 * this branch (see lib/types.ts doc comment). ASP.NET Core's default inbound
 * claim mapping rewrites short claim types to long XML-schema URIs unless
 * the server opts out, so both forms are checked.
 */
interface RawAdminTokenPayload {
  sub?: string;
  nameid?: string;
  email?: string;
  upn?: string;
  role?: string | string[];
  roles?: string | string[];
  permissions?: string | string[];
  permission?: string | string[];
  "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"?: string | string[];
  [claim: string]: unknown;
}

function toStringArray(value: string | string[] | undefined): string[] {
  if (!value) return [];
  return Array.isArray(value) ? value : [value];
}

function firstOf(value: string | string[] | undefined): string | null {
  const list = toStringArray(value);
  return list.length > 0 ? list[0] : null;
}

/**
 * Reads role/permissions off the current access token for nav filtering and
 * "signed in as" display (see lib/permissions.ts). Returns null when there
 * is no token or it cannot be decoded.
 *
 * `permissions` deliberately defaults to an empty array rather than being
 * undefined - every caller of this can rely on it always being an array,
 * whether or not the backend has started sending a permissions claim yet.
 */
export function getSessionClaims(): AdminSessionClaims | null {
  const token = getAccessToken();
  if (!token) return null;

  const payload = decodeJwtPayload<RawAdminTokenPayload>(token);
  if (!payload) return null;

  const role =
    firstOf(payload.role) ??
    firstOf(payload.roles) ??
    firstOf(payload["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"]);

  const permissions = [
    ...toStringArray(payload.permissions),
    ...toStringArray(payload.permission),
  ];

  return {
    subject: payload.sub ?? payload.nameid ?? null,
    email: payload.email ?? payload.upn ?? null,
    role,
    permissions,
  };
}
