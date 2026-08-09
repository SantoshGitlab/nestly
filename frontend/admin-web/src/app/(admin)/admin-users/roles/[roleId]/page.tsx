"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useParams } from "next/navigation";
import { useEffect, useMemo, useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Breadcrumbs, ConfirmDialog, FormActions, FormGrid } from "@/components/data-table";
import { DetailError, DetailSkeleton, SectionError } from "@/components/screen-states";
import { Alert, Button, Card, Checkbox, Field, PageHeading, Textarea, useToast } from "@/components/ui";
import {
  getAdminPermissionCatalog,
  getAdminRole,
  setAdminRolePermissions,
  updateAdminRole,
} from "@/lib/admin-roles-api";
import { AdminPermissionAction } from "@/lib/admin-roles-types";
import type { AdminPermissionCatalogEntry } from "@/lib/admin-roles-types";
import { describeError } from "@/lib/api";
import { canWriteModule } from "@/lib/permissions";
import { useAdminClaims } from "@/lib/use-admin-claims";

const profileSchema = z.object({
  name: z.string().trim().min(1, "Name is required").max(100),
  description: z.string().trim().max(1000),
});
type ProfileFormValues = z.infer<typeof profileSchema>;

interface ModuleRow {
  module: string;
  readCode: string;
  writeCode: string;
}

function buildModuleRows(catalog: AdminPermissionCatalogEntry[]): ModuleRow[] {
  const byModule = new Map<string, ModuleRow>();
  for (const entry of catalog) {
    const row = byModule.get(entry.module) ?? { module: entry.module, readCode: "", writeCode: "" };
    if (entry.action === AdminPermissionAction.Read) row.readCode = entry.code;
    else row.writeCode = entry.code;
    byModule.set(entry.module, row);
  }
  return Array.from(byModule.values()).sort((a, b) => a.module.localeCompare(b.module));
}

/**
 * Role detail (SRS 12.2.2, 12.2.3, task 313): rename/describe the role, and
 * the permission-matrix editor - a checkbox grid of every module x
 * Read/Write, replacing the role's entire grant on save
 * (`setAdminRolePermissions` is a full-replace, not a delta - see the
 * backend contract's doc comment).
 *
 * The server enforces a self-escalation guard on every save: the response
 * is a 403 ("AdminRole.SelfEscalationBlocked") if this would grant the role
 * a permission the signed-in admin does not already hold themselves -
 * `describeError` surfaces that as plain text in the save error banner, the
 * same as any other rejected mutation on this page.
 *
 * Every save goes through `ConfirmDialog`: unlike a single admin account's
 * status, a role's grant can apply to many signed-in admins at once, and a
 * change - addition or removal - takes effect for all of them immediately.
 */
export default function AdminRoleDetailPage() {
  const { roleId } = useParams<{ roleId: string }>();
  const claims = useAdminClaims();
  const canWrite = canWriteModule(claims, "settings");
  const queryClient = useQueryClient();
  const pushToast = useToast();

  const [selectedCodes, setSelectedCodes] = useState<Set<string>>(new Set());
  const [confirmSavePermissions, setConfirmSavePermissions] = useState(false);

  const detailQuery = useQuery({ queryKey: ["admin-role-detail", roleId], queryFn: () => getAdminRole(roleId) });
  const catalogQuery = useQuery({ queryKey: ["admin-permission-catalog"], queryFn: getAdminPermissionCatalog });

  const moduleRows = useMemo(() => buildModuleRows(catalogQuery.data ?? []), [catalogQuery.data]);

  const form = useForm<ProfileFormValues>({
    resolver: zodResolver(profileSchema),
    defaultValues: { name: "", description: "" },
  });

  const loadedId = detailQuery.data?.id;
  useEffect(() => {
    if (detailQuery.data) {
      form.reset({ name: detailQuery.data.name, description: detailQuery.data.description });
      setSelectedCodes(new Set(detailQuery.data.permissionCodes));
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [roleId, loadedId]);

  const refreshRole = () => {
    queryClient.invalidateQueries({ queryKey: ["admin-role-detail", roleId] });
    queryClient.invalidateQueries({ queryKey: ["admin-roles"] });
  };

  const updateMutation = useMutation({
    mutationFn: (values: ProfileFormValues) =>
      updateAdminRole(roleId, { name: values.name.trim(), description: values.description.trim() }),
    onSuccess: () => {
      refreshRole();
      pushToast("success", "Role saved.");
    },
  });

  const permissionsMutation = useMutation({
    mutationFn: () => setAdminRolePermissions(roleId, { permissionCodes: Array.from(selectedCodes) }),
    onSuccess: () => {
      setConfirmSavePermissions(false);
      refreshRole();
      pushToast("success", "Permissions updated.");
    },
  });

  const toggleRead = (row: ModuleRow, checked: boolean) => {
    setSelectedCodes((current) => {
      const next = new Set(current);
      if (checked) {
        next.add(row.readCode);
      } else if (!next.has(row.writeCode)) {
        // Write implies Read (AdminPermissionCatalog's own grant-building
        // rule) - Read cannot be unchecked while Write is still granted.
        next.delete(row.readCode);
      }
      return next;
    });
  };

  const toggleWrite = (row: ModuleRow, checked: boolean) => {
    setSelectedCodes((current) => {
      const next = new Set(current);
      if (checked) {
        next.add(row.writeCode);
        next.add(row.readCode);
      } else {
        next.delete(row.writeCode);
      }
      return next;
    });
  };

  const breadcrumbs = [
    { label: "Admin users", href: "/admin-users" },
    { label: "Roles", href: "/admin-users/roles" },
    { label: detailQuery.data?.name ?? "Role" },
  ];

  if (detailQuery.isPending) {
    return <DetailSkeleton cards={2} />;
  }

  if (detailQuery.error || !detailQuery.data) {
    return (
      <DetailError
        title="Role"
        breadcrumbs={breadcrumbs}
        error={detailQuery.error}
        message={detailQuery.error ? undefined : "This role no longer exists."}
        onRetry={() => detailQuery.refetch()}
      />
    );
  }

  const role = detailQuery.data;
  const originalCodes = new Set(role.permissionCodes);
  const hasChanges =
    selectedCodes.size !== originalCodes.size || Array.from(selectedCodes).some((code) => !originalCodes.has(code));
  const removedCodes = role.permissionCodes.filter((code) => !selectedCodes.has(code));

  return (
    <div className="mx-auto flex w-full max-w-3xl flex-col gap-6">
      <PageHeading
        title={role.name}
        subtitle="Role details and the exact permission-matrix row it grants (SRS 12.2.2, 12.2.3)."
        breadcrumbs={<Breadcrumbs items={breadcrumbs} />}
      />

      <Card title="Role" description={canWrite ? undefined : "Read-only — you do not hold settings write access."}>
        <form
          onSubmit={form.handleSubmit((values) => updateMutation.mutate(values))}
          className="flex flex-col gap-5"
          noValidate
        >
          {updateMutation.isError ? <Alert>{describeError(updateMutation.error)}</Alert> : null}

          <FormGrid columns={1}>
            <Field
              label="Name"
              required
              disabled={!canWrite}
              error={form.formState.errors.name?.message}
              {...form.register("name")}
            />
            <Textarea
              label="Description"
              disabled={!canWrite}
              error={form.formState.errors.description?.message}
              {...form.register("description")}
            />
          </FormGrid>

          {canWrite ? (
            <FormActions>
              <Button type="submit" loading={updateMutation.isPending}>
                Save role
              </Button>
            </FormActions>
          ) : null}
        </form>
      </Card>

      <Card
        title="Permission matrix"
        description="Which modules this role can view (Read) or change (Write). Write always implies Read."
      >
        <div className="flex flex-col gap-4">
          {permissionsMutation.isError && !confirmSavePermissions ? (
            <Alert>{describeError(permissionsMutation.error)}</Alert>
          ) : null}
          {catalogQuery.error ? (
            <SectionError error={catalogQuery.error} onRetry={() => catalogQuery.refetch()}>
              The permission catalog could not be loaded, so the matrix cannot be edited right now.{" "}
              {describeError(catalogQuery.error)}
            </SectionError>
          ) : null}

          <div className="overflow-x-auto rounded-xl border border-line">
            <table className="w-full min-w-[480px] border-collapse text-sm">
              <thead>
                <tr className="border-b border-line bg-surface-2 text-left">
                  <th className="px-4 py-2.5 font-medium text-fg-muted">Module</th>
                  <th className="px-4 py-2.5 font-medium text-fg-muted">Read</th>
                  <th className="px-4 py-2.5 font-medium text-fg-muted">Write</th>
                </tr>
              </thead>
              <tbody>
                {moduleRows.map((row) => (
                  <tr key={row.module} className="border-b border-line last:border-0">
                    <td className="px-4 py-2.5 font-medium text-fg capitalize">{row.module}</td>
                    <td className="px-4 py-2.5">
                      <Checkbox
                        label="Read"
                        checked={selectedCodes.has(row.readCode)}
                        disabled={!canWrite || selectedCodes.has(row.writeCode)}
                        onChange={(event) => toggleRead(row, event.target.checked)}
                      />
                    </td>
                    <td className="px-4 py-2.5">
                      <Checkbox
                        label="Write"
                        checked={selectedCodes.has(row.writeCode)}
                        disabled={!canWrite}
                        onChange={(event) => toggleWrite(row, event.target.checked)}
                      />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {canWrite ? (
            <FormActions>
              <Button
                type="button"
                disabled={!hasChanges}
                onClick={() => setConfirmSavePermissions(true)}
              >
                Save permissions
              </Button>
            </FormActions>
          ) : null}
        </div>
      </Card>

      <ConfirmDialog
        open={confirmSavePermissions}
        title="Update this role's permissions?"
        description="Every admin currently assigned this role is affected immediately, including any signed-in session."
        confirmLabel="Save permissions"
        cancelLabel="Cancel"
        loading={permissionsMutation.isPending}
        error={permissionsMutation.error ? describeError(permissionsMutation.error) : null}
        onCancel={() => setConfirmSavePermissions(false)}
        onConfirm={() => permissionsMutation.mutate()}
      >
        <p className="text-sm text-fg-muted">
          {removedCodes.length > 0
            ? `This removes ${removedCodes.length} permission(s) currently granted.`
            : "This only adds permissions - nothing currently granted is removed."}
        </p>
      </ConfirmDialog>
    </div>
  );
}
