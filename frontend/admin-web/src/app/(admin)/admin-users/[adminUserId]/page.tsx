"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useParams } from "next/navigation";
import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import {
  Breadcrumbs,
  ConfirmDialog,
  DescriptionList,
  FormActions,
  FormGrid,
  formatDate,
  formatDateTime,
} from "@/components/data-table";
import { DetailError, DetailSkeleton, SectionError } from "@/components/screen-states";
import { Alert, Button, Card, Field, IconButton, PageHeading, Select, useToast } from "@/components/ui";
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
import { canWriteModule } from "@/lib/permissions";
import { useAdminClaims } from "@/lib/use-admin-claims";
import { AdminUserStatusBadge } from "../_components/AdminUserStatusBadge";

const profileSchema = z.object({
  fullName: z.string().trim().min(1, "Full name is required").max(200),
  email: z.string().trim().min(1, "Email is required").email("Enter a valid email address"),
});
type ProfileFormValues = z.infer<typeof profileSchema>;

/**
 * Admin user detail (SRS 12.2.1, tasks 97a-97d): edit profile, assign a role,
 * activate/deactivate, and admin-initiated password reset. Mutating controls
 * are only shown to admins holding the module's write permission - the API
 * enforces this server-side regardless, this is purely to avoid showing
 * controls that would just 403 (same convention as the customer 360 view).
 *
 * Both irreversible actions - deactivating an account and invalidating its
 * password - go through `ConfirmDialog`. Deactivation was already destructive
 * and unconfirmed; the password reset silently locked the operator out of
 * their own account on a misclick, with the replacement shown exactly once.
 */
export default function AdminUserDetailPage() {
  const { adminUserId } = useParams<{ adminUserId: string }>();
  const claims = useAdminClaims();
  const canWrite = canWriteModule(claims, "admin-users");
  const currentAdminUserId = claims?.subject ?? null;
  const queryClient = useQueryClient();
  const pushToast = useToast();

  const [temporaryPassword, setTemporaryPassword] = useState<string | null>(null);
  const [confirmDeactivate, setConfirmDeactivate] = useState(false);
  const [confirmReset, setConfirmReset] = useState(false);

  const detailQuery = useQuery({
    queryKey: ["admin-user-detail", adminUserId],
    queryFn: () => getAdminUser(adminUserId),
  });

  const rolesQuery = useQuery({ queryKey: ["admin-user-roles"], queryFn: listAdminRoles });

  const form = useForm<ProfileFormValues>({
    resolver: zodResolver(profileSchema),
    defaultValues: { fullName: "", email: "" },
  });

  // Seeds the editable form from the fetched detail exactly once per load (and
  // again if the underlying id changes) - not on every refetch, so it does not
  // clobber in-progress edits when the other mutations on this page invalidate
  // and refresh the same query.
  const loadedId = detailQuery.data?.id;
  useEffect(() => {
    if (detailQuery.data) {
      form.reset({ fullName: detailQuery.data.fullName, email: detailQuery.data.email });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [adminUserId, loadedId]);

  const refreshAdminUser = () => {
    queryClient.invalidateQueries({ queryKey: ["admin-user-detail", adminUserId] });
    // The list shows name, email, role and status, all of which can change here.
    queryClient.invalidateQueries({ queryKey: ["admin-users", "search"] });
  };

  const updateMutation = useMutation({
    mutationFn: (values: ProfileFormValues) =>
      updateAdminUser(adminUserId, { fullName: values.fullName.trim(), email: values.email.trim() }),
    onSuccess: () => {
      refreshAdminUser();
      pushToast("success", "Profile saved.");
    },
  });

  const assignRoleMutation = useMutation({
    mutationFn: (roleId: string | null) => assignAdminRole(adminUserId, { roleId }),
    onSuccess: () => {
      refreshAdminUser();
      pushToast("success", "Role updated.");
    },
  });

  const activateMutation = useMutation({
    mutationFn: () => activateAdminUser(adminUserId),
    onSuccess: () => {
      refreshAdminUser();
      pushToast("success", "Account activated.");
    },
  });

  const deactivateMutation = useMutation({
    mutationFn: () => deactivateAdminUser(adminUserId),
    onSuccess: () => {
      setConfirmDeactivate(false);
      refreshAdminUser();
      pushToast("success", "Account deactivated.");
    },
  });

  const resetPasswordMutation = useMutation({
    mutationFn: () => resetAdminUserPassword(adminUserId),
    onSuccess: (response) => {
      setConfirmReset(false);
      setTemporaryPassword(response.temporaryPassword);
    },
  });

  const breadcrumbs = [
    { label: "Admin users", href: "/admin-users" },
    { label: detailQuery.data?.fullName ?? "Admin user" },
  ];

  if (detailQuery.isPending) {
    return <DetailSkeleton cards={3} />;
  }

  if (detailQuery.error || !detailQuery.data) {
    return (
      <DetailError
        title="Admin user"
        breadcrumbs={breadcrumbs}
        error={detailQuery.error}
        message={detailQuery.error ? undefined : "This account no longer exists."}
        onRetry={() => detailQuery.refetch()}
      />
    );
  }

  const adminUser = detailQuery.data;
  const isSelf = currentAdminUserId === adminUser.id;
  // "No role assigned" is a real option, not `Select`'s `placeholder` - that
  // renders a *disabled* first option, so a role could be assigned but never
  // removed, even though `assignAdminRole` accepts `null`.
  const roleOptions = [
    { value: "", label: "No role assigned" },
    ...(rolesQuery.data ?? []).map((role) => ({ value: role.id, label: role.name })),
  ];

  return (
    <div className="mx-auto flex w-full max-w-3xl flex-col gap-6">
      <PageHeading
        title={adminUser.fullName}
        subtitle={adminUser.email}
        breadcrumbs={<Breadcrumbs items={breadcrumbs} />}
        actions={<AdminUserStatusBadge status={adminUser.status} isLockedOut={adminUser.isLockedOut} />}
      />

      {temporaryPassword ? (
        <Alert
          tone="success"
          title="Temporary password generated"
          action={
            <IconButton label="Dismiss" onClick={() => setTemporaryPassword(null)}>
              <svg
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                className="h-4 w-4"
                aria-hidden
              >
                <path d="M18 6 6 18M6 6l12 12" />
              </svg>
            </IconButton>
          }
        >
          <span className="nums font-semibold">{temporaryPassword}</span> — relay this to the account
          owner securely. It will not be shown again.
        </Alert>
      ) : null}

      <Card
        title="Profile"
        description={canWrite ? undefined : "Read-only — you do not hold admin-user write access."}
      >
        <form
          onSubmit={form.handleSubmit((values) => updateMutation.mutate(values))}
          className="flex flex-col gap-5"
          noValidate
        >
          {updateMutation.isError ? <Alert>{describeError(updateMutation.error)}</Alert> : null}

          <FormGrid>
            <Field
              label="Full name"
              required
              disabled={!canWrite}
              error={form.formState.errors.fullName?.message}
              {...form.register("fullName")}
            />
            <Field
              label="Email"
              type="email"
              required
              disabled={!canWrite}
              error={form.formState.errors.email?.message}
              {...form.register("email")}
            />
          </FormGrid>

          {canWrite ? (
            <FormActions>
              <Button type="submit" loading={updateMutation.isPending}>
                Save profile
              </Button>
            </FormActions>
          ) : null}
        </form>

        <div className="mt-6 border-t border-line pt-6">
          <DescriptionList
            columns={3}
            items={[
              { label: "Created", value: <span className="nums">{formatDate(adminUser.createdAtUtc)}</span> },
              {
                label: "Failed sign-ins",
                value: <span className="nums">{adminUser.failedLoginAttempts}</span>,
              },
              {
                label: "Locked until",
                value: adminUser.lockedUntilUtc ? (
                  <span className="nums">{formatDateTime(adminUser.lockedUntilUtc)}</span>
                ) : (
                  <span className="text-fg-subtle">Not locked</span>
                ),
              },
            ]}
          />
        </div>
      </Card>

      <Card
        title="Role"
        description="Which permission set this account carries (SRS 12.2.1 'Assign role(s)')."
      >
        <div className="flex flex-col gap-4">
          {assignRoleMutation.isError ? <Alert>{describeError(assignRoleMutation.error)}</Alert> : null}
          {rolesQuery.error ? (
            <SectionError error={rolesQuery.error} onRetry={() => rolesQuery.refetch()}>
              Roles could not be loaded, so the role cannot be changed right now.{" "}
              {describeError(rolesQuery.error)}
            </SectionError>
          ) : null}

          <Select
            label="Role"
            options={roleOptions}
            disabled={!canWrite || rolesQuery.isPending || assignRoleMutation.isPending}
            hint={assignRoleMutation.isPending ? "Saving…" : undefined}
            // While the assignment is in flight the server copy still holds the
            // old role, so binding straight to it snapped the control back to
            // the previous value and made the change look like it had failed.
            value={
              assignRoleMutation.isPending
                ? assignRoleMutation.variables ?? ""
                : adminUser.roleId ?? ""
            }
            onChange={(event) => assignRoleMutation.mutate(event.target.value || null)}
          />
        </div>
      </Card>

      {canWrite ? (
        <Card
          title="Account actions"
          description="Activation state and admin-initiated password reset (SRS 12.2.1)."
        >
          <div className="flex flex-col gap-4">
            {activateMutation.isError ? <Alert>{describeError(activateMutation.error)}</Alert> : null}
            {resetPasswordMutation.isError && !confirmReset ? (
              <Alert>{describeError(resetPasswordMutation.error)}</Alert>
            ) : null}

            <FormActions align="start">
              {adminUser.status === AdminUserStatus.Active ? (
                <Button
                  type="button"
                  variant="danger"
                  disabled={isSelf}
                  title={isSelf ? "You cannot deactivate your own account." : undefined}
                  onClick={() => setConfirmDeactivate(true)}
                >
                  Deactivate account
                </Button>
              ) : (
                <Button
                  type="button"
                  variant="subtle"
                  loading={activateMutation.isPending}
                  onClick={() => activateMutation.mutate()}
                >
                  Activate account
                </Button>
              )}
              <Button type="button" variant="secondary" onClick={() => setConfirmReset(true)}>
                Reset password
              </Button>
            </FormActions>

            {isSelf ? (
              <p className="text-xs text-fg-subtle">
                This is your own account — deactivating it is blocked so you cannot lock yourself
                out.
              </p>
            ) : null}
          </div>
        </Card>
      ) : null}

      <ConfirmDialog
        open={confirmDeactivate}
        title="Deactivate this account?"
        description="The operator is signed out and can no longer sign in. You can activate the account again at any time."
        confirmLabel="Deactivate"
        cancelLabel="Keep active"
        loading={deactivateMutation.isPending}
        error={deactivateMutation.error ? describeError(deactivateMutation.error) : null}
        onCancel={() => setConfirmDeactivate(false)}
        onConfirm={() => deactivateMutation.mutate()}
      >
        <p className="text-sm text-fg-muted">
          Deactivating <span className="font-medium text-fg">{adminUser.fullName}</span> (
          {adminUser.email}).
        </p>
      </ConfirmDialog>

      <ConfirmDialog
        open={confirmReset}
        title="Reset this account's password?"
        description="The current password stops working immediately. The replacement is shown once, here, and cannot be retrieved again."
        confirmLabel="Reset password"
        cancelLabel="Cancel"
        loading={resetPasswordMutation.isPending}
        error={resetPasswordMutation.error ? describeError(resetPasswordMutation.error) : null}
        onCancel={() => setConfirmReset(false)}
        onConfirm={() => resetPasswordMutation.mutate()}
      >
        <p className="text-sm text-fg-muted">
          Resetting the password for{" "}
          <span className="font-medium text-fg">{adminUser.fullName}</span>
          {isSelf ? " — this is your own account, so you will need the new password to sign in again." : "."}
        </p>
      </ConfirmDialog>
    </div>
  );
}
