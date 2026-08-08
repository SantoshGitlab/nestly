/**
 * Typed fetch wrapper for the Consumer API.
 * Base URL comes from NEXT_PUBLIC_API_URL (see .env.example).
 */
import { clearSession, getAccessToken, getRefreshToken, storeSession } from "./auth";
import type { LoginResponse } from "./types";

/** Exported for the chat SignalR connection (ChatWidget), which talks to the same origin outside of `apiFetch`. */
export const API_BASE_URL =
  process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5257";

/** All customer endpoints are served under the v1 route prefix. */
export const API_V1 = "/api/v1";

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
 * ValidationProblem responses carry per-field messages under `errors`; a
 * plain failure carries only `detail`. Falling back to the generic Error
 * message keeps callers from having to branch on the error type.
 */
export function describeError(error: unknown): string {
  if (error instanceof ApiError) {
    const fieldErrors = error.problem?.errors;
    if (Array.isArray(fieldErrors) && fieldErrors.length > 0) {
      return fieldErrors.map((e) => e.message).join(" ");
    }
    // ASP.NET's ValidationProblem serialises errors as an object keyed by
    // field name, not the array shape above — handle both.
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
 * The backend's machine-readable error code for a failed request, or null if
 * this wasn't an API failure.
 *
 * Every domain error the API returns carries its code (`Coupon.NotActive`,
 * `Booking.SlotCapacityReached`, …) as the ProblemDetails `title`, with the
 * human wording in `detail`. Branching on the code lets a screen react to the
 * *kind* of failure without matching on message text, which changes.
 */
export function errorCode(error: unknown): string | null {
  return error instanceof ApiError ? (error.problem?.title ?? null) : null;
}

export interface ApiFetchOptions extends RequestInit {
  /** Attaches the stored bearer token. Required by every [Authorize] endpoint. */
  authenticated?: boolean;
}

/**
 * Exchanges the stored refresh token for a new access/refresh pair.
 *
 * Module-level promise so a burst of concurrent 401s (several queries firing
 * at once when the access token expires mid-session) triggers exactly one
 * refresh call instead of one per request; every caller awaits the same
 * in-flight promise, then it's cleared so the next expiry starts a fresh one.
 */
let refreshPromise: Promise<boolean> | null = null;

function refreshAccessToken(): Promise<boolean> {
  if (!refreshPromise) {
    refreshPromise = (async () => {
      const refreshToken = getRefreshToken();
      if (!refreshToken) return false;
      try {
        const response = await fetch(`${API_BASE_URL}${API_V1}/auth/refresh`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ refreshToken }),
        });
        if (!response.ok) return false;
        storeSession((await response.json()) as LoginResponse);
        return true;
      } catch {
        return false;
      }
    })().finally(() => {
      refreshPromise = null;
    });
  }
  return refreshPromise;
}

export async function apiFetch<T>(
  path: string,
  init?: ApiFetchOptions,
  isRetry = false,
): Promise<T> {
  const { authenticated, ...requestInit } = init ?? {};

  const headers: Record<string, string> = {
    "Content-Type": "application/json",
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
    // A 401 on an authenticated call usually just means the short-lived
    // access token expired mid-session - silently refresh it and retry the
    // request once before treating this as a real auth failure. Only
    // authenticated calls attempt this (login/refresh itself never sets
    // `authenticated`, so it can't recurse into itself).
    if (authenticated && response.status === 401 && !isRetry) {
      const refreshed = await refreshAccessToken();
      if (refreshed) {
        return apiFetch<T>(path, init, true);
      }
    }

    let problem: ProblemDetails | null = null;
    try {
      problem = (await response.json()) as ProblemDetails;
    } catch {
      // Non-JSON error body; keep problem null.
    }

    // A 401 on an authenticated call (after the refresh attempt above has
    // already failed or been skipped) means the session truly can't continue
    // - clear it so every mounted guard (RequireAuth) reacts to the
    // auth-changed event and sends the customer back to /login. An
    // unauthenticated call rejecting with 401 must NOT clear anything - there
    // is nothing to clear, and this is the expected "invalid credentials"
    // outcome.
    if (authenticated && response.status === 401) {
      clearSession();
    }

    throw new ApiError(response.status, problem);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  // A 200 with an empty body is not the same as a 204, and several endpoints
  // return exactly that — ASP.NET's `Ok()` with no argument sends 200 with
  // Content-Length: 0 (profile mobile/otp and email/otp, auth, payments).
  // Calling response.json() on those rejects with a SyntaxError, which
  // surfaced as "Unexpected end of JSON input" on a request that had in fact
  // succeeded: the OTP really was sent, but the profile screen showed an
  // error and never advanced to the code-entry step. Read the body as text
  // first and treat empty as no content.
  const body = await response.text();
  if (body === "") {
    return undefined as T;
  }

  return JSON.parse(body) as T;
}
