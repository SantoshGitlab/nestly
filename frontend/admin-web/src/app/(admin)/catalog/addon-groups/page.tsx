"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Button, Card, Field, PageHeading, Select } from "@/components/ui";
import { ConfirmDialog, DataTable, FormActions, FormGrid } from "@/components/data-table";
import { describeError } from "@/lib/api";
import { createAddOnGroup, deleteAddOnGroup, listAddOnGroups, listServices, updateAddOnGroup } from "@/lib/catalog-api";
import type { ServiceAddOnGroupAdminResponse } from "@/lib/catalog-types";
import { canWriteModule } from "@/lib/permissions";
import { useAdminClaims } from "@/lib/use-admin-claims";
import { CatalogTabs } from "../_components/CatalogTabs";

const groupSchema = z
  .object({
    serviceId: z.string().min(1, "Select a service"),
    name: z.string().min(1, "Group name is required").max(200),
    selectionType: z.enum(["Single", "Multiple"]),
    minSelect: z.number().int().min(0),
    maxSelect: z.number().int().min(1).optional(),
    sortOrder: z.number().int().min(0),
  })
  .refine((v) => v.selectionType !== "Single" || v.maxSelect === undefined || v.maxSelect <= 1, {
    message: "A pick-one group's max selection cannot exceed 1.",
    path: ["maxSelect"],
  });
type GroupFormValues = z.infer<typeof groupSchema>;
const emptyGroupForm: GroupFormValues = {
  serviceId: "",
  name: "",
  selectionType: "Single",
  minSelect: 0,
  maxSelect: 1,
  sortOrder: 0,
};

/**
 * Admin add-on-group management screen (Phase 3 catalog redesign): create
 * named groups with a pick-one/pick-many selection rule, mapped to a
 * service. Mirrors `/catalog/addons`'s flat list+create shape, since groups
 * get their own top-level tab rather than living only under one service's
 * edit page.
 */
export default function CatalogAddOnGroupsPage() {
  const claims = useAdminClaims();
  const [serviceFilter, setServiceFilter] = useState("");
  const [editingGroupId, setEditingGroupId] = useState<string | null>(null);
  const [pendingDelete, setPendingDelete] = useState<ServiceAddOnGroupAdminResponse | null>(null);
  const queryClient = useQueryClient();

  const canWrite = canWriteModule(claims, "catalog");

  const servicesQuery = useQuery({ queryKey: ["services"], queryFn: () => listServices() });
  const groupsQuery = useQuery({
    queryKey: ["addon-groups", serviceFilter],
    queryFn: () => listAddOnGroups(serviceFilter || undefined),
  });

  const serviceOptions = (servicesQuery.data ?? []).map((s) => ({ value: s.id, label: s.name }));

  const form = useForm<GroupFormValues>({ resolver: zodResolver(groupSchema), defaultValues: emptyGroupForm });
  const selectionType = form.watch("selectionType");

  const createMutation = useMutation({
    mutationFn: createAddOnGroup,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["addon-groups"] });
      form.reset(emptyGroupForm);
    },
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, values }: { id: string; values: GroupFormValues }) =>
      updateAddOnGroup(id, { ...values, maxSelect: values.maxSelect ?? null }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["addon-groups"] });
      setEditingGroupId(null);
      form.reset(emptyGroupForm);
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => deleteAddOnGroup(id),
    onSuccess: () => {
      setPendingDelete(null);
      queryClient.invalidateQueries({ queryKey: ["addon-groups"] });
    },
  });

  const startEditing = (group: ServiceAddOnGroupAdminResponse) => {
    setEditingGroupId(group.id);
    form.reset({
      serviceId: group.serviceId,
      name: group.name,
      selectionType: group.selectionType,
      minSelect: group.minSelect,
      maxSelect: group.maxSelect ?? undefined,
      sortOrder: group.sortOrder,
    });
  };

  const cancelEditing = () => {
    setEditingGroupId(null);
    form.reset(emptyGroupForm);
  };

  const onSubmit = form.handleSubmit((values) => {
    const request = { ...values, maxSelect: values.maxSelect ?? null };
    if (editingGroupId) {
      updateMutation.mutate({ id: editingGroupId, values });
    } else {
      createMutation.mutate(request);
    }
  });

  return (
    <div className="mx-auto flex w-full max-w-5xl flex-col gap-6">
      <div>
        <PageHeading title="Catalog" subtitle="Categories, services and add-ons (SRS 12.5-12.7)." />
        <CatalogTabs />
      </div>

      <DataTable<ServiceAddOnGroupAdminResponse>
        title="Add-on groups"
        description="Named groups of add-ons with a pick-one/pick-many selection rule (Phase 3 catalog redesign)."
        actions={
          <div className="w-56">
            <Select
              label="Filter by service"
              value={serviceFilter}
              onChange={(e) => setServiceFilter(e.target.value)}
              options={[{ value: "", label: "All services" }, ...serviceOptions]}
            />
          </div>
        }
        rows={groupsQuery.data}
        rowKey={(group) => group.id}
        isLoading={groupsQuery.isPending}
        isFetching={groupsQuery.isFetching}
        error={groupsQuery.error}
        onRetry={() => groupsQuery.refetch()}
        emptyTitle={serviceFilter ? "No add-on groups for this service" : "No add-on groups yet"}
        minWidth="820px"
        rowActions={
          canWrite
            ? (group) => (
                <div className="flex gap-2">
                  <Button type="button" size="sm" variant="secondary" onClick={() => startEditing(group)}>
                    Edit
                  </Button>
                  <Button type="button" size="sm" variant="secondary" onClick={() => setPendingDelete(group)}>
                    Delete
                  </Button>
                </div>
              )
            : undefined
        }
        columns={[
          { key: "name", header: "Name", cell: (g) => g.name, sortValue: (g) => g.name },
          { key: "service", header: "Service", cell: (g) => g.serviceName, sortValue: (g) => g.serviceName },
          {
            key: "selection",
            header: "Selection rule",
            cell: (g) => (g.selectionType === "Single" ? "Pick one" : "Pick many"),
            sortValue: (g) => g.selectionType,
          },
          { key: "min", header: "Min", numeric: true, cell: (g) => g.minSelect, sortValue: (g) => g.minSelect },
          { key: "max", header: "Max", numeric: true, cell: (g) => g.maxSelect ?? "—", sortValue: (g) => g.maxSelect ?? -1 },
        ]}
      />

      {canWrite ? (
        <Card
          title={editingGroupId ? "Edit add-on group" : "Add add-on group"}
          description="Creates the group immediately. Assign add-ons to it from the Add-ons tab."
        >
          <form onSubmit={onSubmit} className="flex flex-col gap-4" noValidate>
            {(editingGroupId ? updateMutation.isError : createMutation.isError) ? (
              <Alert>{describeError(editingGroupId ? updateMutation.error : createMutation.error)}</Alert>
            ) : null}

            <FormGrid>
              <Select
                label="Service"
                required
                placeholder="Select a service…"
                error={form.formState.errors.serviceId?.message}
                options={serviceOptions}
                {...form.register("serviceId")}
              />
              <Field label="Name" required error={form.formState.errors.name?.message} {...form.register("name")} />
            </FormGrid>

            <FormGrid columns={3}>
              <Select
                label="Selection rule"
                options={[
                  { value: "Single", label: "Pick one" },
                  { value: "Multiple", label: "Pick many" },
                ]}
                {...form.register("selectionType")}
              />
              <Field
                label="Min select"
                type="number"
                error={form.formState.errors.minSelect?.message}
                {...form.register("minSelect", { valueAsNumber: true })}
              />
              <Field
                label="Max select"
                type="number"
                hint={selectionType === "Single" ? "At most 1 for a pick-one group." : "Leave blank for unbounded."}
                error={form.formState.errors.maxSelect?.message}
                {...form.register("maxSelect", { valueAsNumber: true })}
              />
            </FormGrid>

            <Field
              label="Sort order"
              type="number"
              error={form.formState.errors.sortOrder?.message}
              {...form.register("sortOrder", { valueAsNumber: true })}
            />

            <FormActions>
              {editingGroupId ? (
                <Button type="button" variant="secondary" onClick={cancelEditing}>
                  Cancel
                </Button>
              ) : null}
              <Button type="submit" loading={form.formState.isSubmitting || createMutation.isPending || updateMutation.isPending}>
                {editingGroupId ? "Save group" : "Add group"}
              </Button>
            </FormActions>
          </form>
        </Card>
      ) : null}

      <ConfirmDialog
        open={pendingDelete !== null}
        title="Delete this add-on group?"
        description="Fails if any add-on still points at it — ungroup or reassign them first."
        confirmLabel="Delete group"
        cancelLabel="Keep group"
        loading={deleteMutation.isPending}
        error={deleteMutation.isError ? describeError(deleteMutation.error) : null}
        onCancel={() => setPendingDelete(null)}
        onConfirm={() => {
          if (pendingDelete) deleteMutation.mutate(pendingDelete.id);
        }}
      >
        {pendingDelete ? (
          <p className="text-sm text-fg-muted">
            Deleting <span className="font-medium text-fg">{pendingDelete.name}</span>.
          </p>
        ) : null}
      </ConfirmDialog>
    </div>
  );
}
