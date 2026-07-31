"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Button, Card, Field, Select } from "@/components/ui";
import { describeError } from "@/lib/api";
import { createPincode, listCities, listPincodes, setPincodeActive } from "@/lib/serviceability-api";
import type { PincodeAdminResponse } from "@/lib/serviceability-types";
import { EntityTable } from "./EntityTable";

const pincodeSchema = z.object({
  cityId: z.string().min(1, "Select a city"),
  code: z.string().min(1, "Pincode is required").max(10),
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
    mutationFn: createPincode,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["pincodes"] });
      form.reset({ cityId: form.getValues("cityId"), code: "" });
    },
  });

  const toggleMutation = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => setPincodeActive(id, isActive),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["pincodes"] }),
  });

  const cityOptions = (citiesQuery.data ?? []).map((city) => ({ value: city.id, label: city.name }));
  const onSubmit = form.handleSubmit((values) => createMutation.mutate(values));

  return (
    <Card title="Pincodes" description="Service serviceability is mapped at this level (SRS 12.9.1, 12.9.2).">
      <div className="mb-4 w-64">
        <Select
          label="Filter by city"
          value={cityFilter}
          onChange={(e) => setCityFilter(e.target.value)}
          options={[{ value: "", label: "All cities" }, ...cityOptions]}
        />
      </div>

      <EntityTable<PincodeAdminResponse>
        items={pincodesQuery.data}
        isLoading={pincodesQuery.isLoading}
        errorMessage={pincodesQuery.error ? describeError(pincodesQuery.error) : null}
        emptyMessage="No pincodes yet."
        canWrite={canWrite}
        togglingId={toggleMutation.isPending ? toggleMutation.variables?.id : undefined}
        onToggleActive={(pincode) => toggleMutation.mutate({ id: pincode.id, isActive: !pincode.isActive })}
        columns={[
          { header: "Code", render: (pincode) => pincode.code },
          { header: "City", render: (pincode) => pincode.cityName },
        ]}
      />

      {canWrite ? (
        <form onSubmit={onSubmit} className="mt-4 flex flex-wrap items-end gap-3" noValidate>
          {createMutation.isError ? (
            <div className="w-full">
              <Alert>{describeError(createMutation.error)}</Alert>
            </div>
          ) : null}
          <div className="w-48">
            <Select
              label="City"
              placeholder="Select a city…"
              error={form.formState.errors.cityId?.message}
              options={cityOptions}
              {...form.register("cityId")}
            />
          </div>
          <div className="w-32">
            <Field label="Pincode" error={form.formState.errors.code?.message} {...form.register("code")} />
          </div>
          <Button type="submit" disabled={form.formState.isSubmitting || createMutation.isPending}>
            {createMutation.isPending ? "Adding…" : "Add pincode"}
          </Button>
        </form>
      ) : null}
    </Card>
  );
}
