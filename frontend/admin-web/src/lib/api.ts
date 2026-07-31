/**
 * Typed fetch wrapper for the Admin API.
 * Base URL comes from NEXT_PUBLIC_API_URL (see .env.example).
 */
import { clearSession, getAccessToken } from "./auth";

const API_BASE_URL =
  process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5177";

/** All admin endpoints are served under the v1 route prefix. */
export const API_V1 = "/api/v1/admin";

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly problem: ProblemDetails | null,
  ) {
    super(problem?.detail ?? `Request failed with status ${status}`);
    this.name = "ApiError";
  }
}

/** RFC 7807 problem details returned by the backend on failures. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  correlationId?: string;
  errors?: { code: string; message: string }[];
}

/**
 * Best-effort human-readable message for a failed request.
 *
 * Mirrors customer-web/src/lib/api.ts's describeError: ValidationProblem
 * responses carry per-field messages under `errors` (either the array shape
 * or ASP.NET's object-keyed-by-field-name shape), a plain failure carries
 * only `detail`.
 */
export function describeError(error: unknown): string {
  if (error instanceof ApiError) {
    const fieldErrors = error.problem?.errors;
    if (Array.isArray(fieldErrors) && fieldErrors.length > 0) {
      return fieldErrors.map((e) => e.message).join(" ");
    }
    if (fieldErrors && !Array.isArray(fieldErrors)) {
      const messages = Object.values(
        fieldErrors as unknown as Record<string, string[]>,
      ).flat();
      if (messages.length > 0) return messages.join(" ");
    }
    return error.message;
  }
  return error instanceof Error ? error.message : "Something went wrong.";
}

/**
 * Login-specific error message (SRS 12.1.1: throttling, lockout).
 *
 * `AdminAuthController`'s exact error codes are not confirmed on this branch
 * yet (see lib/types.ts doc comment), so this checks the signals RFC 7807 +
 * the codebase's `Module.Reason` convention (docs/API.md) make available
 * today - HTTP status (401 invalid credentials, 423 locked, 429 throttled)
 * and a "locked"/"disabled" hint in `type` or `detail` - rather than
 * asserting one exact code string. Falls back to describeError otherwise.
 */
export function describeLoginError(error: unknown): string {
  if (error instanceof ApiError) {
    const signal = `${error.problem?.type ?? ""} ${error.problem?.detail ?? ""}`;

    if (error.status === 423 || /locked/i.test(signal)) {
      return "This account has been locked after repeated failed sign-in attempts. Contact another Super Admin to unlock it.";
    }
    if (/inactive|disabled|deactivated/i.test(signal)) {
      return "This account has been deactivated. Contact a Super Admin for access.";
    }
    if (error.status === 429) {
      return "Too many sign-in attempts. Please wait a few minutes and try again.";
    }
    if (error.status === 401) {
      return "Invalid email or password.";
    }
  }
  return describeError(error);
}

export interface ApiFetchOptions extends RequestInit {
  /** Attaches the stored bearer token. Required by every [Authorize] admin endpoint. */
  authenticated?: boolean;
}

/**
 * Shared request/error-handling core behind both `apiFetch` (JSON) and
 * `apiFetchBlob` (raw bytes, e.g. a CSV export) - everything except how the
 * successful body is read back is identical, so that part alone is left to
 * each caller.
 */
async function performFetch(path: string, init: ApiFetchOptions | undefined, defaultHeaders: Record<string, string>): Promise<Response> {
  const { authenticated, ...requestInit } = init ?? {};

  const headers: Record<string, string> = {
    ...defaultHeaders,
    ...(requestInit.headers as Record<string, string> | undefined),
  };

  if (authenticated) {
    const token = getAccessToken();
    if (!token) {
      throw new ApiError(401, { detail: "You are not signed in." });
    }
    headers.Authorization = `Bearer ${token}`;
  }

  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...requestInit,
    headers,
  });

  if (!response.ok) {
    let problem: ProblemDetails | null = null;
    try {
      problem = (await response.json()) as ProblemDetails;
    } catch {
      // Non-JSON error body; keep problem null.
    }

    // Session handling (SRS 25.2): a 401 on an authenticated call means the
    // token the caller had is no longer valid (expired, revoked, or the
    // account was deactivated after login) - clear it so every mounted
    // guard (RequireAdminAuth) reacts to the auth-changed event and sends
    // the admin back to /login. An unauthenticated call rejecting with 401
    // (e.g. a bad login attempt) must NOT clear anything - there is nothing
    // to clear, and this is the expected "invalid credentials" outcome.
    if (authenticated && response.status === 401) {
      clearSession();
    }

    throw new ApiError(response.status, problem);
  }

  return response;
}

export async function apiFetch<T>(
  path: string,
  init?: ApiFetchOptions,
): Promise<T> {
  const response = await performFetch(path, init, { "Content-Type": "application/json" });

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

/**
 * Fetches a raw binary response (e.g. the review moderation CSV export,
 * task 122) rather than parsing it as JSON - the caller is responsible for
 * turning the blob into a download (see reviews/page.tsx).
 */
export async function apiFetchBlob(path: string, init?: ApiFetchOptions): Promise<Blob> {
  const response = await performFetch(path, init, {});
  return response.blob();
}
