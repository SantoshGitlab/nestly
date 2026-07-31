"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useParams } from "next/navigation";
import { useEffect, useState } from "react";
import { Alert, Button, Card, Field, PageHeading, Select } from "@/components/ui";
import {
  activateAdminUser,
  assignAdminRole,
  deactivateAdminUser,
  getAdminUser,
  listAdminRoles,
  resetAdminUserPassword,
  updateAdminUser,
} from "@/lib/admin-users-api";
import { AdminUserStatus } from "@/lib/admin-users-types";
import { describeError } from "@/lib/api";
import { getSessionClaims, subscribeToAuthChanges } from "@/lib/auth";
import { canWriteModule } from "@/lib/permissions";
import type { AdminSessionClaims } from "@/lib/types";

function useAdminClaims(): AdminSessionClaims | null {
  const [claims, setClaims] = useState<AdminSessionClaims | null>(null);
  useEffect(() => {
    const sync = () => setClaims(getSessionClaims());
    sync();
    return subscribeToAuthChanges(sync);
  }, []);
  return claims;
}

/**
 * Admin user detail (SRS 12.2.1, tasks 97a-97d): edit profile, assign a
 * role, activate/deactivate, and admin-initiated password reset. Mutating
 * controls are only shown to admins holding "settings.write" - the API
 * enforces this server-side regardless, this is purely to avoid showing
 * controls that would just 403 (same convention as the customer 360 view).
 */
export default function AdminUserDetailPage() {
  const params = useParams<{ adminUserId: string }>();
  const adminUserId = params.adminUserId;
  const claims = useAdminClaims();
  const canWrite = canWriteModule(claims, "admin-users");
  const currentAdminUserId = claims?.subject ?? null;
  const queryClient = useQueryClient();

  const detailQuery = useQuery({
    queryKey: ["admin-user-detail", adminUserId],
    queryFn: () => getAdminUser(adminUserId),
  });

  const rolesQuery = useQuery({
    queryKey: ["admin-user-roles"],
    queryFn: listAdminRoles,
  });

  const [profileForm, setProfileForm] = useState({ email: "", fullName: "" });
  const [actionError, setActionError] = useState<string | null>(null);
  const [temporaryPassword, setTemporaryPassword] = useState<string | null>(null);

  // Seeds the editable form from the fetched detail exactly once per load
  // (and again if the underlying id changes) - not on every refetch, so it
  // does not clobber in-progress edits when other mutations on this page
  // invalidate and refresh the same query.
  useEffect(() => {
    if (detailQuery.data) {
      setProfileForm({ email: detailQuery.data.email, fullName: detailQuery.data.fullName });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [adminUserId, detailQuery.data?.id]);

  const invalidateDetail = () =>
    queryClient.invalidateQueries({ queryKey: ["admin-user-detail", adminUserId] });

  const updateMutation = useMutation({
    mutationFn: () => updateAdminUser(adminUserId, profileForm),
    onSuccess: () => {
      setActionError(null);
      invalidateDetail();
      queryClient.invalidateQueries({ queryKey: ["admin-users"] });
    },
    onError: (err) => setActionError(describeError(err)),
  });

  const assignRoleMutation = useMutation({
    mutationFn: (roleId: string | null) => assignAdminRole(adminUserId, { roleId }),
    onSuccess: () => {
      setActionError(null);
      invalidateDetail();
      queryClient.invalidateQueries({ queryKey: ["admin-users"] });
    },
    onError: (err) => setActionError(describeError(err)),
  });

  const activateMutation = useMutation({
    mutationFn: () => activateAdminUser(adminUserId),
    onSuccess: () => {
      setActionError(null);
      invalidateDetail();
      queryClient.invalidateQueries({ queryKey: ["admin-users"] });
    },
    onError: (err) => setActionError(describeError(err)),
  });

  const deactivateMutation = useMutation({
    mutationFn: () => deactivateAdminUser(adminUserId),
    onSuccess: () => {
      setActionError(null);
      invalidateDetail();
      queryClient.invalidateQueries({ queryKey: ["admin-users"] });
    },
    onError: (err) => setActionError(describeError(err)),
  });

  const resetPasswordMutation = useMutation({
    mutationFn: () => resetAdminUserPassword(adminUserId),
    onSuccess: (response) => {
      setActionError(null);
      setTemporaryPassword(response.temporaryPassword);
    },
    onError: (err) => setActionError(describeError(err)),
  });

  if (detailQuery.isPending) {
    return <p className="text-sm text-neutral-500">Loading admin user…</p>;
  }

  if (detailQuery.isError) {
    return <Alert>{describeError(detailQuery.error)}</Alert>;
  }

  const adminUser = detailQuery.data;
  const isSelf = currentAdminUserId === adminUser.id;
  const roleOptions = (rolesQuery.data ?? []).map((role) => ({ value: role.id, label: role.name }));

  return (
    <div className="mx-auto flex w-full max-w-3xl flex-col gap-6">
      <div className="flex items-center justify-between">
        <PageHeading title={adminUser.fullName} subtitle={adminUser.email} />
        <Link href="/admin-users" className="text-sm underline-offset-2 hover:underline">
          Back to admin users
        </Link>
      </div>

      {actionError ? <Alert>{actionError}</Alert> : null}
      {temporaryPassword ? (
        <Alert tone="success">
          Temporary password generated: <span className="font-mono font-semibold">{temporaryPassword}</span>. Relay this
          to the account owner securely - it will not be shown again.
        </Alert>
      ) : null}

      <Card
        title="Profile"
        description={`Status: ${adminUser.status === AdminUserStatus.Active ? "Active" : "Inactive"}${
          adminUser.isLockedOut ? " · Locked out" : ""
        }`}
      >
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <Field
            label="Full name"
            value={profileForm.fullName}
            disabled={!canWrite}
            onChange={(e) => setProfileForm((f) => ({ ...f, fullName: e.target.value }))}
          />
          <Field
            label="Email"
            type="email"
            value={profileForm.email}
            disabled={!canWrite}
            onChange={(e) => setProfileForm((f) => ({ ...f, email: e.target.value }))}
          />
        </div>

        {canWrite ? (
          <div className="mt-4">
            <Button disabled={updateMutation.isPending} onClick={() => updateMutation.mutate()}>
              {updateMutation.isPending ? "Saving…" : "Save profile"}
            </Button>
          </div>
        ) : null}

        <dl className="mt-5 grid grid-cols-2 gap-x-4 gap-y-2 border-t border-black/10 pt-5 text-sm dark:border-white/15 sm:grid-cols-3">
          <div>
            <dt className="text-neutral-500">Created</dt>
            <dd>{new Date(adminUser.createdAtUtc).toLocaleDateString()}</dd>
          </div>
          <div>
            <dt className="text-neutral-500">Failed login attempts</dt>
            <dd>{adminUser.failedLoginAttempts}</dd>
          </div>
          {adminUser.lockedUntilUtc ? (
            <div>
              <dt className="text-neutral-500">Locked until</dt>
              <dd>{new Date(adminUser.lockedUntilUtc).toLocaleString()}</dd>
            </div>
          ) : null}
        </dl>
      </Card>

      <Card title="Role" description="Assigns which permission set this account carries (SRS 12.2.1 'Assign role(s)').">
        <Select
          label="Role"
          placeholder="No role assigned"
          options={roleOptions}
          disabled={!canWrite || assignRoleMutation.isPending}
          value={adminUser.roleId ?? ""}
          onChange={(e) => assignRoleMutation.mutate(e.target.value || null)}
        />
      </Card>

      {canWrite ? (
        <Card title="Account actions" description="Activate/deactivate and password reset (SRS 12.2.1).">
          <div className="flex flex-col gap-3 sm:flex-row">
            {adminUser.status === AdminUserStatus.Active ? (
              <Button
                variant="danger"
                disabled={isSelf || deactivateMutation.isPending}
                title={isSelf ? "You cannot deactivate your own account." : undefined}
                onClick={() => deactivateMutation.mutate()}
              >
                {deactivateMutation.isPending ? "Deactivating…" : "Deactivate account"}
              </Button>
            ) : (
              <Button variant="secondary" disabled={activateMutation.isPending} onClick={() => activateMutation.mutate()}>
                {activateMutation.isPending ? "Activating…" : "Activate account"}
              </Button>
            )}
            <Button
              variant="secondary"
              disabled={resetPasswordMutation.isPending}
              onClick={() => resetPasswordMutation.mutate()}
            >
              {resetPasswordMutation.isPending ? "Resetting…" : "Reset password"}
            </Button>
          </div>
        </Card>
      ) : null}
    </div>
  );
}
