"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Button, Card, Field, PageHeading, Select } from "@/components/ui";
import { ActiveBadge, ConfirmDialog, DataTable, FormActions, FormGrid } from "@/components/data-table";
import { describeError } from "@/lib/api";
import {
  createServiceGroup,
  deleteServiceGroup,
  listCategories,
  listServiceGroups,
  setServiceGroupActive,
  updateServiceGroup,
} from "@/lib/catalog-api";
import type { ServiceGroupAdminResponse } from "@/lib/catalog-types";
import { canWriteModule } from "@/lib/permissions";
import { useAdminClaims } from "@/lib/use-admin-claims";
import { CatalogTabs } from "../_components/CatalogTabs";

const groupSchema = z.object({
  categoryId: z.string().min(1, "Select a category"),
  name: z.string().min(1, "Group name is required").max(200),
  sortOrder: z.number().int().min(0),
});
type GroupFormValues = z.infer<typeof groupSchema>;
const emptyGroupForm: GroupFormValues = { categoryId: "", name: "", sortOrder: 0 };

/**
 * Admin service-group management screen (Appliance/Service Group catalog
 * redesign): create named section headers (e.g. "Repair & gas refill") for a
 * subset of a category's services, mapped to a category. Mirrors
 * `/catalog/addon-groups`'s flat list+create shape, since service groups get
 * their own top-level tab rather than living only under one category's edit
 * page - distinct from an add-on group, which groups one *service's*
 * add-ons rather than a *category's* services.
 */
export default function CatalogServiceGroupsPage() {
  const claims = useAdminClaims();
  const [categoryFilter, setCategoryFilter] = useState("");
  const [editingGroupId, setEditingGroupId] = useState<string | null>(null);
  const [pendingDelete, setPendingDelete] = useState<ServiceGroupAdminResponse | null>(null);
  const queryClient = useQueryClient();

  const canWrite = canWriteModule(claims, "catalog");

  const categoriesQuery = useQuery({ queryKey: ["categories"], queryFn: () => listCategories() });
  const groupsQuery = useQuery({
    queryKey: ["service-groups", categoryFilter],
    queryFn: () => listServiceGroups(categoryFilter || undefined),
  });

  const categoryOptions = (categoriesQuery.data ?? []).map((c) => ({ value: c.id, label: c.name }));

  const form = useForm<GroupFormValues>({ resolver: zodResolver(groupSchema), defaultValues: emptyGroupForm });

  const createMutation = useMutation({
    mutationFn: createServiceGroup,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["service-groups"] });
      form.reset(emptyGroupForm);
    },
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, values }: { id: string; values: GroupFormValues }) => updateServiceGroup(id, values),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["service-groups"] });
      setEditingGroupId(null);
      form.reset(emptyGroupForm);
    },
  });

  const toggleActiveMutation = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => setServiceGroupActive(id, isActive),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["service-groups"] }),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => deleteServiceGroup(id),
    onSuccess: () => {
      setPendingDelete(null);
      queryClient.invalidateQueries({ queryKey: ["service-groups"] });
    },
  });

  const startEditing = (group: ServiceGroupAdminResponse) => {
    setEditingGroupId(group.id);
    form.reset({ categoryId: group.categoryId, name: group.name, sortOrder: group.sortOrder });
  };

  const cancelEditing = () => {
    setEditingGroupId(null);
    form.reset(emptyGroupForm);
  };

  const onSubmit = form.handleSubmit((values) => {
    if (editingGroupId) {
      updateMutation.mutate({ id: editingGroupId, values });
    } else {
      createMutation.mutate(values);
    }
  });

  return (
    <div className="mx-auto flex w-full max-w-5xl flex-col gap-6">
      <div>
        <PageHeading title="Catalog" subtitle="Categories, services and add-ons (SRS 12.5-12.7)." />
        <CatalogTabs />
      </div>

      <DataTable<ServiceGroupAdminResponse>
        title="Service groups"
        description='Optional section headers for a subset of a category&rsquo;s services (e.g. "Repair & gas refill" under "AC"). Leave a service ungrouped to show it directly with no header.'
        actions={
          <div className="w-56">
            <Select
              label="Filter by category"
              value={categoryFilter}
              onChange={(e) => setCategoryFilter(e.target.value)}
              options={[{ value: "", label: "All categories" }, ...categoryOptions]}
            />
          </div>
        }
        rows={groupsQuery.data}
        rowKey={(group) => group.id}
        isLoading={groupsQuery.isPending}
        isFetching={groupsQuery.isFetching}
        error={groupsQuery.error}
        onRetry={() => groupsQuery.refetch()}
        emptyTitle={categoryFilter ? "No service groups for this category" : "No service groups yet"}
        minWidth="760px"
        rowActions={
          canWrite
            ? (group) => (
                <div className="flex gap-2">
                  <Button type="button" size="sm" variant="secondary" onClick={() => startEditing(group)}>
                    Edit
                  </Button>
                  <Button
                    type="button"
                    size="sm"
                    variant={group.isActive ? "secondary" : "subtle"}
                    loading={toggleActiveMutation.isPending && toggleActiveMutation.variables?.id === group.id}
                    onClick={() => toggleActiveMutation.mutate({ id: group.id, isActive: !group.isActive })}
                  >
                    {group.isActive ? "Deactivate" : "Activate"}
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
          { key: "category", header: "Category", cell: (g) => g.categoryName, sortValue: (g) => g.categoryName },
          { key: "sortOrder", header: "Sort order", numeric: true, cell: (g) => g.sortOrder, sortValue: (g) => g.sortOrder },
          {
            key: "status",
            header: "Status",
            cell: (g) => <ActiveBadge active={g.isActive} inactiveLabel="Suspended" />,
            sortValue: (g) => g.isActive,
          },
        ]}
      />

      {canWrite ? (
        <Card
          title={editingGroupId ? "Edit service group" : "Add service group"}
          description="Creates the group immediately. Assign services to it from the Services tab."
        >
          <form onSubmit={onSubmit} className="flex flex-col gap-4" noValidate>
            {(editingGroupId ? updateMutation.isError : createMutation.isError) ? (
              <Alert>{describeError(editingGroupId ? updateMutation.error : createMutation.error)}</Alert>
            ) : null}

            <FormGrid>
              <Select
                label="Category"
                required
                placeholder="Select a category…"
                error={form.formState.errors.categoryId?.message}
                options={categoryOptions}
                {...form.register("categoryId")}
              />
              <Field label="Name" required error={form.formState.errors.name?.message} {...form.register("name")} />
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
        title="Delete this service group?"
        description="Fails if any service still points at it — ungroup or reassign them first."
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
