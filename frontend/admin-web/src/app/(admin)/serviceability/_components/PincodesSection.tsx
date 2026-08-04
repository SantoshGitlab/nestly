"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Button, Card, Field, Select } from "@/components/ui";
import { FormActions, FormGrid } from "@/components/data-table";
import { EntityTable } from "@/components/entity-table";
import { describeError } from "@/lib/api";
import { createPincode, listCities, listPincodes, setPincodeActive } from "@/lib/serviceability-api";
import type { PincodeAdminResponse } from "@/lib/serviceability-types";
import { toLookupOptions } from "./lookup-options";

const pincodeSchema = z.object({
  cityId: z.string().min(1, "Select a city"),
  // Matches the platform's own pincode rule (Application/Profile's
  // `^\d{6}$`), so a typo is caught here rather than by a 400 from the API.
  code: z.string().trim().min(1, "Pincode is required").regex(/^\d{6}$/, "A pincode is 6 digits"),
});
type PincodeFormValues = z.infer<typeof pincodeSchema>;

/**
 * Geography master: pincodes (SRS 12.9.1) - the level service-serviceability
 * is mapped against (SRS 12.9.2). No rename control: a pincode's code is a
 * postal identifier, not an editable label (see PincodeCreateRequest's doc
 * comment on the backend) - only activation state can change after creation.
 */
export function PincodesSection({ canWrite }: { canWrite: boolean }) {
  const queryClient = useQueryClient();
  const [cityFilter, setCityFilter] = useState("");

  const citiesQuery = useQuery({ queryKey: ["cities", ""], queryFn: () => listCities(undefined) });
  const pincodesQuery = useQuery({
    queryKey: ["pincodes", cityFilter],
    queryFn: () => listPincodes(cityFilter || undefined),
  });

  const form = useForm<PincodeFormValues>({
    resolver: zodResolver(pincodeSchema),
    defaultValues: { cityId: "", code: "" },
  });

  const createMutation = useMutation({
    mutationFn: (values: PincodeFormValues) => createPincode({ cityId: values.cityId, code: values.code.trim() }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["pincodes"] });
      form.reset({ cityId: form.getValues("cityId"), code: "" });
    },
  });

  const toggleMutation = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => setPincodeActive(id, isActive),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["pincodes"] }),
  });

  const cityOptions = toLookupOptions(citiesQuery.data, (city) => city.name);
  const onSubmit = form.handleSubmit((values) => createMutation.mutate(values));

  return (
    <div className="flex flex-col gap-4">
      <EntityTable<PincodeAdminResponse>
        title="Pincodes"
        description="Service serviceability is mapped at this level (SRS 12.9.1, 12.9.2)."
        actions={
          <div className="w-56">
            <Select
              label="Filter by city"
              value={cityFilter}
              onChange={(e) => setCityFilter(e.target.value)}
              options={[{ value: "", label: "All cities" }, ...cityOptions]}
            />
          </div>
        }
        items={pincodesQuery.data}
        isLoading={pincodesQuery.isPending}
        isFetching={pincodesQuery.isFetching}
        error={pincodesQuery.error}
        onRetry={() => pincodesQuery.refetch()}
        emptyMessage={cityFilter ? "No pincodes in this city" : "No pincodes yet"}
        emptyAction={
          cityFilter ? (
            <Button variant="secondary" onClick={() => setCityFilter("")}>
              Show all cities
            </Button>
          ) : undefined
        }
        canWrite={canWrite}
        entityLabel="pincode"
        labelOf={(pincode) => pincode.code}
        minWidth="640px"
        skeletonRows={5}
        togglingId={toggleMutation.isPending ? toggleMutation.variables?.id : undefined}
        toggleError={toggleMutation.error}
        onToggleActive={(pincode) => toggleMutation.mutate({ id: pincode.id, isActive: !pincode.isActive })}
        columns={[
          {
            header: "Code",
            sortValue: (pincode) => pincode.code,
            render: (pincode) => <span className="nums font-medium text-fg">{pincode.code}</span>,
          },
          { header: "City", sortValue: (pincode) => pincode.cityName, render: (pincode) => pincode.cityName },
        ]}
      />

      {canWrite ? (
        <Card
          title="Add a pincode"
          description="A pincode's code cannot be changed later — only its activation state."
        >
          <form onSubmit={onSubmit} className="flex flex-col gap-4" noValidate>
            {createMutation.isError ? <Alert>{describeError(createMutation.error)}</Alert> : null}
            <FormGrid>
              <Select
                label="City"
                required
                placeholder="Select a city…"
                error={form.formState.errors.cityId?.message}
                options={cityOptions}
                {...form.register("cityId")}
              />
              <Field
                label="Pincode"
                required
                inputMode="numeric"
                maxLength={6}
                error={form.formState.errors.code?.message}
                {...form.register("code")}
              />
            </FormGrid>
            <FormActions align="start">
              <Button type="submit" loading={createMutation.isPending}>
                Add pincode
              </Button>
            </FormActions>
          </form>
        </Card>
      ) : null}
    </div>
  );
}
