/**
 * Typed client for the Admin API's admin-user management surface (SRS
 * 12.2.1, tasks 97a-97d): `AdminUsersController`. Every call is
 * authenticated - these are admin-only endpoints gated behind
 * "settings.read"/"settings.write" server-side.
 */
import { API_V1, apiFetch } from "./api";
import type {
  AdminRoleSummary,
  AdminUserDetail,
  AdminUserSearchParams,
  AdminUserSearchResponse,
  AssignAdminRoleRequest,
  CreateAdminUserRequest,
  ResetAdminPasswordResponse,
  UpdateAdminUserRequest,
} from "./admin-users-types";

const ADMIN_USERS_BASE = `${API_V1}/admin-users`;

function query(params: object): string {
  const entries = Object.entries(params as Record<string, string | number | boolean | undefined>)
    .filter(([, value]) => value !== undefined);
  if (entries.length === 0) return "";
  return `?${new URLSearchParams(entries.map(([key, value]) => [key, String(value)])).toString()}`;
}

export const searchAdminUsers = (params: AdminUserSearchParams) =>
  apiFetch<AdminUserSearchResponse>(`${ADMIN_USERS_BASE}${query(params)}`, { authenticated: true });

export const listAdminRoles = () =>
  apiFetch<AdminRoleSummary[]>(`${ADMIN_USERS_BASE}/roles`, { authenticated: true });

export const getAdminUser = (adminUserId: string) =>
  apiFetch<AdminUserDetail>(`${ADMIN_USERS_BASE}/${adminUserId}`, { authenticated: true });

export const createAdminUser = (request: CreateAdminUserRequest) =>
  apiFetch<AdminUserDetail>(ADMIN_USERS_BASE, {
    method: "POST",
    authenticated: true,
    body: JSON.stringify(request),
  });

export const updateAdminUser = (adminUserId: string, request: UpdateAdminUserRequest) =>
  apiFetch<AdminUserDetail>(`${ADMIN_USERS_BASE}/${adminUserId}`, {
    method: "PUT",
    authenticated: true,
    body: JSON.stringify(request),
  });

export const assignAdminRole = (adminUserId: string, request: AssignAdminRoleRequest) =>
  apiFetch<AdminUserDetail>(`${ADMIN_USERS_BASE}/${adminUserId}/role`, {
    method: "PUT",
    authenticated: true,
    body: JSON.stringify(request),
  });

export const activateAdminUser = (adminUserId: string) =>
  apiFetch<AdminUserDetail>(`${ADMIN_USERS_BASE}/${adminUserId}/activate`, {
    method: "POST",
    authenticated: true,
  });

export const deactivateAdminUser = (adminUserId: string) =>
  apiFetch<AdminUserDetail>(`${ADMIN_USERS_BASE}/${adminUserId}/deactivate`, {
    method: "POST",
    authenticated: true,
  });

export const resetAdminUserPassword = (adminUserId: string) =>
  apiFetch<ResetAdminPasswordResponse>(`${ADMIN_USERS_BASE}/${adminUserId}/reset-password`, {
    method: "POST",
    authenticated: true,
  });

// Lives under AdminAuthController's own route, not AdminUsersController -
// see AdminAuthController.Unlock (task 95d's unlock path), gated behind
// "settings.write" the same as the rest of this file's mutations.
export const unlockAdminUser = (adminUserId: string) =>
  apiFetch<void>(`${API_V1}/admin/auth/unlock/${adminUserId}`, {
    method: "POST",
    authenticated: true,
  });
