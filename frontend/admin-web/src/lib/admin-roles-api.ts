/**
 * Typed client for the Admin API's role CRUD and permission-matrix editor
 * surface (SRS 12.2.2, 12.2.3, task 313): `AdminRolesController`. Every call
 * is authenticated - gated behind "settings.read"/"settings.write"
 * server-side, same as `admin-users-api.ts`.
 */
import { API_V1, apiFetch } from "./api";
import type {
  AdminPermissionCatalogEntry,
  AdminRoleDetail,
  CreateAdminRoleRequest,
  SetAdminRolePermissionsRequest,
  UpdateAdminRoleRequest,
} from "./admin-roles-types";

const ADMIN_ROLES_BASE = `${API_V1}/admin-roles`;

export const getAdminPermissionCatalog = () =>
  apiFetch<AdminPermissionCatalogEntry[]>(`${ADMIN_ROLES_BASE}/permissions`, { authenticated: true });

export const listAdminRolesWithPermissions = () =>
  apiFetch<AdminRoleDetail[]>(ADMIN_ROLES_BASE, { authenticated: true });

export const getAdminRole = (roleId: string) =>
  apiFetch<AdminRoleDetail>(`${ADMIN_ROLES_BASE}/${roleId}`, { authenticated: true });

export const createAdminRole = (request: CreateAdminRoleRequest) =>
  apiFetch<AdminRoleDetail>(ADMIN_ROLES_BASE, {
    method: "POST",
    authenticated: true,
    body: JSON.stringify(request),
  });

export const updateAdminRole = (roleId: string, request: UpdateAdminRoleRequest) =>
  apiFetch<AdminRoleDetail>(`${ADMIN_ROLES_BASE}/${roleId}`, {
    method: "PUT",
    authenticated: true,
    body: JSON.stringify(request),
  });

export const setAdminRolePermissions = (roleId: string, request: SetAdminRolePermissionsRequest) =>
  apiFetch<AdminRoleDetail>(`${ADMIN_ROLES_BASE}/${roleId}/permissions`, {
    method: "PUT",
    authenticated: true,
    body: JSON.stringify(request),
  });
