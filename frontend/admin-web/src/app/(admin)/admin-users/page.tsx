"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import { Alert, Button, Card, Field, PageHeading, Select } from "@/components/ui";
import { createAdminUser, listAdminRoles, searchAdminUsers } from "@/lib/admin-users-api";
import { AdminUserStatus } from "@/lib/admin-users-types";
import { describeError } from "@/lib/api";
import { getSessionClaims, subscribeToAuthChanges } from "@/lib/auth";
import { canWriteModule } from "@/lib/permissions";
import type { AdminSessionClaims } from "@/lib/types";

const PAGE_SIZE = 20;

function useAdminClaims(): AdminSessionClaims | null {
  const [claims, setClaims] = useState<AdminSessionClaims | null>(null);
  useEffect(() => {
    const sync = () => setClaims(getSessionClaims());
    sync();
    return subscribeToAuthChanges(sync);
  }, []);
  return claims;
}

const STATUS_OPTIONS: { value: AdminUserStatus | ""; label: string }[] = [
  { value: "", label: "Any status" },
  { value: AdminUserStatus.Active, label: "Active" },
  { value: AdminUserStatus.Inactive, label: "Inactive" },
];

function statusLabel(status: AdminUserStatus): string {
  return status === AdminUserStatus.Active ? "Active" : "Inactive";
}

interface FilterFormState {
  email: string;
  name: string;
  status: string;
}

const EMPTY_FILTERS: FilterFormState = { email: "", name: "", status: "" };

interface CreateFormState {
  email: string;
  fullName: string;
  password: string;
  roleId: string;
}

const EMPTY_CREATE_FORM: CreateFormState = { email: "", fullName: "", password: "", roleId: "" };

/**
 * Admin user management (SRS 12.2.1, tasks 97a-97d): search/filter, create,
 * and a link into each account's detail page for role assignment,
 * activate/deactivate and password reset. Gated on "settings.write" for the
 * create form the same way every other admin screen gates its mutating
 * controls - the API enforces this server-side regardless.
 */
export default function AdminUsersPage() {
  const claims = useAdminClaims();
  const canWrite = canWriteModule(claims, "admin-users");
  const queryClient = useQueryClient();

  const [filters, setFilters] = useState<FilterFormState>(EMPTY_FILTERS);
  const [appliedFilters, setAppliedFilters] = useState<FilterFormState>(EMPTY_FILTERS);
  const [page, setPage] = useState(1);
  const [showCreateForm, setShowCreateForm] = useState(false);
  const [createForm, setCreateForm] = useState<CreateFormState>(EMPTY_CREATE_FORM);
  const [createError, setCreateError] = useState<string | null>(null);

  const rolesQuery = useQuery({
    queryKey: ["admin-user-roles"],
    queryFn: listAdminRoles,
  });

  const listQuery = useQuery({
    queryKey: ["admin-users", appliedFilters, page],
    queryFn: () =>
      searchAdminUsers({
        email: appliedFilters.email || undefined,
        name: appliedFilters.name || undefined,
        status: appliedFilters.status === "" ? undefined : (Number(appliedFilters.status) as AdminUserStatus),
        page,
        pageSize: PAGE_SIZE,
      }),
  });

  const createMutation = useMutation({
    mutationFn: () =>
      createAdminUser({
        email: createForm.email.trim(),
        fullName: createForm.fullName.trim(),
        password: createForm.password,
        roleId: createForm.roleId || null,
      }),
    onSuccess: () => {
      setCreateForm(EMPTY_CREATE_FORM);
      setCreateError(null);
      setShowCreateForm(false);
      queryClient.invalidateQueries({ queryKey: ["admin-users"] });
    },
    onError: (err) => setCreateError(describeError(err)),
  });

  const onSubmitFilters = (e: FormEvent) => {
    e.preventDefault();
    setPage(1);
    setAppliedFilters(filters);
  };

  const onClearFilters = () => {
    setFilters(EMPTY_FILTERS);
    setAppliedFilters(EMPTY_FILTERS);
    setPage(1);
  };

  const onSubmitCreate = (e: FormEvent) => {
    e.preventDefault();
    createMutation.mutate();
  };

  const totalPages = listQuery.data ? Math.max(1, Math.ceil(listQuery.data.totalCount / PAGE_SIZE)) : 1;
  const roleOptions = (rolesQuery.data ?? []).map((role) => ({ value: role.id, label: role.name }));

  return (
    <div className="mx-auto w-full max-w-5xl">
      <PageHeading
        title="Admin Users"
        subtitle="Manage back-office operator accounts: create, assign roles, activate/deactivate and reset passwords (SRS 12.2.1)."
      />

      {canWrite ? (
        <div className="mb-6">
          {showCreateForm ? (
            <Card title="Create admin user" description="Provisions a new back-office account.">
              <form onSubmit={onSubmitCreate} className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                {createError ? (
                  <div className="sm:col-span-2">
                    <Alert>{createError}</Alert>
                  </div>
                ) : null}
                <Field
                  label="Full name"
                  required
                  value={createForm.fullName}
                  onChange={(e) => setCreateForm((f) => ({ ...f, fullName: e.target.value }))}
                />
                <Field
                  label="Email"
                  type="email"
                  required
                  value={createForm.email}
                  onChange={(e) => setCreateForm((f) => ({ ...f, email: e.target.value }))}
                />
                <Field
                  label="Temporary password"
                  type="password"
                  required
                  value={createForm.password}
                  onChange={(e) => setCreateForm((f) => ({ ...f, password: e.target.value }))}
                />
                <Select
                  label="Role"
                  placeholder="No role assigned"
                  options={roleOptions}
                  value={createForm.roleId}
                  onChange={(e) => setCreateForm((f) => ({ ...f, roleId: e.target.value }))}
                />
                <div className="flex items-end gap-2 sm:col-span-2">
                  <Button type="submit" disabled={createMutation.isPending}>
                    {createMutation.isPending ? "Creating…" : "Create admin user"}
                  </Button>
                  <Button
                    type="button"
                    variant="secondary"
                    onClick={() => {
                      setShowCreateForm(false);
                      setCreateForm(EMPTY_CREATE_FORM);
                      setCreateError(null);
                    }}
                  >
                    Cancel
                  </Button>
                </div>
              </form>
            </Card>
          ) : (
            <Button onClick={() => setShowCreateForm(true)}>Create admin user</Button>
          )}
        </div>
      ) : null}

      <Card title="Filters">
        <form onSubmit={onSubmitFilters} className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          <Field
            label="Name"
            value={filters.name}
            onChange={(e) => setFilters((f) => ({ ...f, name: e.target.value }))}
          />
          <Field
            label="Email"
            value={filters.email}
            onChange={(e) => setFilters((f) => ({ ...f, email: e.target.value }))}
          />
          <Select
            label="Status"
            placeholder="Any status"
            options={STATUS_OPTIONS.filter((o) => o.value !== "").map((o) => ({
              value: String(o.value),
              label: o.label,
            }))}
            value={filters.status}
            onChange={(e) => setFilters((f) => ({ ...f, status: e.target.value }))}
          />
          <div className="flex items-end gap-2">
            <Button type="submit">Search</Button>
            <Button type="button" variant="secondary" onClick={onClearFilters}>
              Clear
            </Button>
          </div>
        </form>
      </Card>

      <div className="mt-6">
        {listQuery.isPending ? (
          <p className="text-sm text-neutral-500">Loading admin users…</p>
        ) : listQuery.isError ? (
          <Alert>{describeError(listQuery.error)}</Alert>
        ) : listQuery.data.items.length === 0 ? (
          <Card title="No admin users match these filters">
            <p className="text-sm text-neutral-600 dark:text-neutral-400">
              Try broadening your search criteria.
            </p>
          </Card>
        ) : (
          <>
            <div className="overflow-x-auto rounded-xl border border-black/10 dark:border-white/15">
              <table className="w-full min-w-[720px] text-left text-sm">
                <thead className="border-b border-black/10 bg-black/5 dark:border-white/15 dark:bg-white/5">
                  <tr>
                    <th className="px-4 py-3 font-medium">Name</th>
                    <th className="px-4 py-3 font-medium">Email</th>
                    <th className="px-4 py-3 font-medium">Role</th>
                    <th className="px-4 py-3 font-medium">Status</th>
                    <th className="px-4 py-3 font-medium">Created</th>
                  </tr>
                </thead>
                <tbody>
                  {listQuery.data.items.map((adminUser) => (
                    <tr
                      key={adminUser.id}
                      className="border-b border-black/5 last:border-0 hover:bg-black/[.03] dark:border-white/10 dark:hover:bg-white/[.05]"
                    >
                      <td className="px-4 py-3">
                        <Link href={`/admin-users/${adminUser.id}`} className="font-medium underline-offset-2 hover:underline">
                          {adminUser.fullName}
                        </Link>
                      </td>
                      <td className="px-4 py-3">{adminUser.email}</td>
                      <td className="px-4 py-3">{adminUser.roleName ?? "—"}</td>
                      <td className="px-4 py-3">
                        {statusLabel(adminUser.status)}
                        {adminUser.isLockedOut ? " (locked)" : ""}
                      </td>
                      <td className="px-4 py-3">{new Date(adminUser.createdAtUtc).toLocaleDateString()}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <div className="mt-4 flex items-center justify-between text-sm">
              <span className="text-neutral-600 dark:text-neutral-400">
                Page {page} of {totalPages} · {listQuery.data.totalCount} admin user
                {listQuery.data.totalCount === 1 ? "" : "s"}
              </span>
              <div className="flex gap-2">
                <Button variant="secondary" disabled={page <= 1} onClick={() => setPage((p) => Math.max(1, p - 1))}>
                  Previous
                </Button>
                <Button
                  variant="secondary"
                  disabled={page >= totalPages}
                  onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                >
                  Next
                </Button>
              </div>
            </div>
          </>
        )}
      </div>
    </div>
  );
}
