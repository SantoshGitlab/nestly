/**
 * Types for the Admin API's admin-user management surface (SRS 12.2.1, tasks
 * 97a-97d): `AdminUsersController`. Mirrors
 * `backend/shared/Application/AdminUserManagement/AdminUserManagementContracts.cs`
 * field-for-field.
 */

/** Mirrors `Nestly.Domain.AdminUserStatus`'s declaration order exactly. */
export enum AdminUserStatus {
  Active = 0,
  Inactive = 1,
}

export interface AdminUserSummary {
  id: string;
  email: string;
  fullName: string;
  status: AdminUserStatus;
  roleId: string | null;
  roleName: string | null;
  isLockedOut: boolean;
  createdAtUtc: string;
}

export interface AdminUserSearchResponse {
  items: AdminUserSummary[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface AdminUserSearchParams {
  email?: string;
  name?: string;
  status?: AdminUserStatus;
  roleId?: string;
  page?: number;
  pageSize?: number;
}

export interface AdminUserDetail {
  id: string;
  email: string;
  fullName: string;
  status: AdminUserStatus;
  roleId: string | null;
  roleName: string | null;
  isLockedOut: boolean;
  lockedUntilUtc: string | null;
  failedLoginAttempts: number;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface CreateAdminUserRequest {
  email: string;
  fullName: string;
  password: string;
  roleId: string | null;
}

export interface UpdateAdminUserRequest {
  email: string;
  fullName: string;
}

export interface AssignAdminRoleRequest {
  roleId: string | null;
}

export interface ResetAdminPasswordResponse {
  temporaryPassword: string;
}

export interface AdminRoleSummary {
  id: string;
  name: string;
  description: string;
}
