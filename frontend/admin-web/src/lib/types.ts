/**
 * Response/request shapes for the Admin API's auth surface.
 *
 * NOTE ON PROVENANCE: as of this writing, `AdminAuthController` /
 * `AdminTokenService` (SRS 12.1, tasks 94-95) have not landed on this branch
 * yet (verified by filesystem inspection, not by trusting tasks.csv - see
 * AGENTS.md). These types describe the contract this client is built
 * against: a JWT bearer pair shaped the same way the Consumer API's
 * `LoginResponse` is (see customer-web/src/lib/types.ts), since that is the
 * only concrete precedent in this codebase for a Nestly login response. When
 * the real controller lands, reconcile field names against it - if it
 * differs, only this file and `lib/auth.ts` should need to change.
 */

export interface AdminLoginRequestBody {
  email: string;
  password: string;
}

export interface AdminLoginResponse {
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshToken: string;
}

/**
 * Claims this client reads off the decoded access token for UI purposes
 * (nav filtering, "signed in as" display) - never for authorization
 * decisions, which remain the API's job.
 *
 * `role` is expected to exist today (a JWT is not useful for RBAC without
 * one). `permissions` is anticipated from a parallel task and may be absent
 * or empty until that lands - every reader of this field must tolerate
 * that (see `lib/permissions.ts`).
 */
export interface AdminSessionClaims {
  subject: string | null;
  email: string | null;
  role: string | null;
  permissions: string[];
}

/**
 * Dashboard KPI filters (SRS 12.3.2), sent as query-string parameters to
 * `GET {API_V1}/dashboard/kpis`. Every field is optional - `dateFrom`/`dateTo`
 * are `yyyy-MM-dd` (what an `<input type="date">` produces, and what
 * ASP.NET Core's `DateOnly` model binder parses directly); an unset pair
 * makes the API default to today, and an unset `city`/`category` applies no
 * restriction on that dimension.
 */
export interface DashboardKpiFilters {
  dateFrom?: string;
  dateTo?: string;
  city?: string;
  category?: string;
}

/**
 * SRS 12.3.1's KPI widget set, mirroring the Admin API's `DashboardKpiResponse`
 * (task 99). `dateFrom`/`dateTo` echo back the window the API actually
 * resolved - relevant when the caller left them unset and the API defaulted
 * to today.
 */
export interface DashboardKpiResponse {
  dateFrom: string;
  dateTo: string;
  bookingsCount: number;
  revenueTotal: number;
  cancellationsCount: number;
  refundAmountTotal: number;
  openSupportTicketsCount: number;
}
