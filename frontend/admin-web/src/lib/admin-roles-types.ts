/**
 * Types for the Admin API's role CRUD and permission-matrix editor surface
 * (SRS 12.2.2, 12.2.3, task 313): `AdminRolesController`. Mirrors
 * `backend/shared/Application/AdminRoleManagement/AdminRoleManagementContracts.cs`
 * field-for-field.
 */

/** Mirrors `Nestly.Domain.AdminPermissionAction`'s declaration order exactly - no enum crosses the wire as a string anywhere in this codebase. */
export enum AdminPermissionAction {
  Read = 0,
  Write = 1,
}

export interface AdminPermissionCatalogEntry {
  code: string;
  module: string;
  action: AdminPermissionAction;
  description: string;
}

export interface AdminRoleDetail {
  id: string;
  name: string;
  description: string;
  permissionCodes: string[];
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface CreateAdminRoleRequest {
  name: string;
  description: string;
  permissionCodes: string[];
}

export interface UpdateAdminRoleRequest {
  name: string;
  description: string;
}

export interface SetAdminRolePermissionsRequest {
  permissionCodes: string[];
}
