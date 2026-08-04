"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Button, Card, Select } from "@/components/ui";
import { FormActions, FormGrid } from "@/components/data-table";
import { EntityTable } from "@/components/entity-table";
import { describeError } from "@/lib/api";
import {
  createCategoryCityMapping,
  listCategoryCityMappings,
  listCategoryLookups,
  listCities,
  setCategoryCityMappingActive,
} from "@/lib/serviceability-api";
import type { CategoryCityMappingResponse } from "@/lib/serviceability-types";
import { toLookupOptions } from "./lookup-options";

const mappingSchema = z.object({
  categoryId: z.string().min(1, "Select a category"),
  cityId: z.string().min(1, "Select a city"),
});
type MappingFormValues = z.infer<typeof mappingSchema>;

/**
 * Which categories are active in which city (SRS 12.9.2). Deactivating a row
 * is the reversible "service blackout in selected areas" suspension SRS
 * 12.9.2 describes - the mapping record stays, only IsActive flips.
 */
export function CategoryCityMappingSection({ canWrite }: { canWrite: boolean }) {
  const queryClient = useQueryClient();
  const [categoryFilter, setCategoryFilter] = useState("");
  const [cityFilter, setCityFilter] = useState("");

  const categoriesQuery = useQuery({ queryKey: ["category-lookups"], queryFn: listCategoryLookups });
  const citiesQuery = useQuery({ queryKey: ["cities", ""], queryFn: () => listCities(undefined) });
  const mappingsQuery = useQuery({
    queryKey: ["category-city-mappings", categoryFilter, cityFilter],
    queryFn: () => listCategoryCityMappings(categoryFilter || undefined, cityFilter || undefined),
  });

  const form = useForm<MappingFormValues>({
    resolver: zodResolver(mappingSchema),
    defaultValues: { categoryId: "", cityId: "" },
  });

  const createMutation = useMutation({
    mutationFn: createCategoryCityMapping,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["category-city-mappings"] });
      form.reset({ categoryId: "", cityId: "" });
    },
  });

  const toggleMutation = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => setCategoryCityMappingActive(id, isActive),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["category-city-mappings"] }),
  });

  // The category lookup carries no activation flag, so it cannot be annotated
  // the way the city list is.
  const categoryOptions = (categoriesQuery.data ?? []).map((c) => ({ value: c.id, label: c.name }));
  const cityOptions = toLookupOptions(citiesQuery.data, (c) => c.name);

  const selected = form.watch();
  /**
   * The API rejects a duplicate pair, but the error comes back as an opaque
   * 409 well after the click. Naming the clash up-front — and pointing at the
   * row's own Activate control — is the difference between "it is broken" and
   * "it already exists but is suspended". Only what the current filters
   * returned is searched, so this catches the common case rather than every
   * case; the server stays the authority.
   */
  const duplicate = (mappingsQuery.data ?? []).find(
    (mapping) =>
      selected.categoryId !== "" &&
      mapping.categoryId === selected.categoryId &&
      mapping.cityId === selected.cityId,
  );

  const onSubmit = form.handleSubmit((values) => createMutation.mutate(values));
  const hasFilters = Boolean(categoryFilter || cityFilter);

  return (
    <div className="flex flex-col gap-4">
      <EntityTable<CategoryCityMappingResponse>
        title="Category serviceability by city"
        description="Which categories are active in which city (SRS 12.9.2)."
        actions={
          <div className="flex flex-wrap items-end gap-3">
            <div className="w-48">
              <Select
                label="Filter by category"
                value={categoryFilter}
                onChange={(e) => setCategoryFilter(e.target.value)}
                options={[{ value: "", label: "All categories" }, ...categoryOptions]}
              />
            </div>
            <div className="w-48">
              <Select
                label="Filter by city"
                value={cityFilter}
                onChange={(e) => setCityFilter(e.target.value)}
                options={[{ value: "", label: "All cities" }, ...cityOptions]}
              />
            </div>
          </div>
        }
        items={mappingsQuery.data}
        isLoading={mappingsQuery.isPending}
        isFetching={mappingsQuery.isFetching}
        error={mappingsQuery.error}
        onRetry={() => mappingsQuery.refetch()}
        emptyMessage={hasFilters ? "No mappings match these filters" : "No category/city mappings yet"}
        emptyAction={
          hasFilters ? (
            <Button
              variant="secondary"
              onClick={() => {
                setCategoryFilter("");
                setCityFilter("");
              }}
            >
              Clear filters
            </Button>
          ) : undefined
        }
        canWrite={canWrite}
        entityLabel="category/city mapping"
        labelOf={(mapping) => `${mapping.categoryName} in ${mapping.cityName}`}
        minWidth="680px"
        skeletonRows={5}
        togglingId={toggleMutation.isPending ? toggleMutation.variables?.id : undefined}
        toggleError={toggleMutation.error}
        onToggleActive={(mapping) => toggleMutation.mutate({ id: mapping.id, isActive: !mapping.isActive })}
        columns={[
          {
            header: "Category",
            sortValue: (mapping) => mapping.categoryName,
            render: (mapping) => <span className="font-medium text-fg">{mapping.categoryName}</span>,
          },
          { header: "City", sortValue: (mapping) => mapping.cityName, render: (mapping) => mapping.cityName },
        ]}
      />

      {canWrite ? (
        <Card
          title="Enable a category in a city"
          description="Suspending a mapping later hides the category from that city without deleting the record."
        >
          <form onSubmit={onSubmit} className="flex flex-col gap-4" noValidate>
            {createMutation.isError ? <Alert>{describeError(createMutation.error)}</Alert> : null}
            {duplicate ? (
              <Alert tone="info">
                {duplicate.categoryName} is already mapped to {duplicate.cityName}
                {duplicate.isActive ? " and is active." : " but is suspended — activate it in the list above."}
              </Alert>
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
              <Select
                label="City"
                required
                placeholder="Select a city…"
                error={form.formState.errors.cityId?.message}
                options={cityOptions}
                {...form.register("cityId")}
              />
            </FormGrid>
            <FormActions align="start">
              <Button type="submit" loading={createMutation.isPending} disabled={duplicate !== undefined}>
                Enable serviceability
              </Button>
            </FormActions>
          </form>
        </Card>
      ) : null}
    </div>
  );
}
