"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Button, Card, Select } from "@/components/ui";
import { describeError } from "@/lib/api";
import {
  createCategoryCityMapping,
  listCategoryCityMappings,
  listCategoryLookups,
  listCities,
  setCategoryCityMappingActive,
} from "@/lib/serviceability-api";
import type { CategoryCityMappingResponse } from "@/lib/serviceability-types";
import { EntityTable } from "./EntityTable";

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

  const categoryOptions = (categoriesQuery.data ?? []).map((c) => ({ value: c.id, label: c.name }));
  const cityOptions = (citiesQuery.data ?? []).map((c) => ({ value: c.id, label: c.name }));
  const onSubmit = form.handleSubmit((values) => createMutation.mutate(values));

  return (
    <Card
      title="Category serviceability by city"
      description="Which categories are active in which city (SRS 12.9.2)."
    >
      <div className="mb-4 flex flex-wrap gap-3">
        <div className="w-56">
          <Select
            label="Filter by category"
            value={categoryFilter}
            onChange={(e) => setCategoryFilter(e.target.value)}
            options={[{ value: "", label: "All categories" }, ...categoryOptions]}
          />
        </div>
        <div className="w-56">
          <Select
            label="Filter by city"
            value={cityFilter}
            onChange={(e) => setCityFilter(e.target.value)}
            options={[{ value: "", label: "All cities" }, ...cityOptions]}
          />
        </div>
      </div>

      <EntityTable<CategoryCityMappingResponse>
        items={mappingsQuery.data}
        isLoading={mappingsQuery.isLoading}
        errorMessage={mappingsQuery.error ? describeError(mappingsQuery.error) : null}
        emptyMessage="No category/city mappings yet."
        canWrite={canWrite}
        togglingId={toggleMutation.isPending ? toggleMutation.variables?.id : undefined}
        onToggleActive={(mapping) => toggleMutation.mutate({ id: mapping.id, isActive: !mapping.isActive })}
        columns={[
          { header: "Category", render: (mapping) => mapping.categoryName },
          { header: "City", render: (mapping) => mapping.cityName },
        ]}
      />

      {canWrite ? (
        <form onSubmit={onSubmit} className="mt-4 flex flex-wrap items-end gap-3" noValidate>
          {createMutation.isError ? (
            <div className="w-full">
              <Alert>{describeError(createMutation.error)}</Alert>
            </div>
          ) : null}
          <div className="w-56">
            <Select
              label="Category"
              placeholder="Select a category…"
              error={form.formState.errors.categoryId?.message}
              options={categoryOptions}
              {...form.register("categoryId")}
            />
          </div>
          <div className="w-56">
            <Select
              label="City"
              placeholder="Select a city…"
              error={form.formState.errors.cityId?.message}
              options={cityOptions}
              {...form.register("cityId")}
            />
          </div>
          <Button type="submit" disabled={form.formState.isSubmitting || createMutation.isPending}>
            {createMutation.isPending ? "Saving…" : "Enable serviceability"}
          </Button>
        </form>
      ) : null}
    </Card>
  );
}
