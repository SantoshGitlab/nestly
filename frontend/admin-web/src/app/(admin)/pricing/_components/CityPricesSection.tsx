"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Button, Card, Field, Select } from "@/components/ui";
import { describeError } from "@/lib/api";
import { createCityPrice, listCityPrices, updateCityPrice } from "@/lib/pricing-api";
import type { CityPriceResponse } from "@/lib/pricing-types";
import { listCities, listServiceLookups } from "@/lib/serviceability-api";

const cityPriceSchema = z
  .object({
    serviceId: z.string().min(1, "Select a service"),
    cityId: z.string().min(1, "Select a city"),
    price: z
      .string()
      .min(1, "Price is required")
      .refine((value) => !Number.isNaN(Number(value)) && Number(value) > 0, "Price must be greater than zero"),
    effectiveStartDate: z.string().optional(),
    effectiveEndDate: z.string().optional(),
  })
  .refine(
    (values) =>
      !values.effectiveStartDate || !values.effectiveEndDate || values.effectiveEndDate >= values.effectiveStartDate,
    { message: "The end date must not be before the start date.", path: ["effectiveEndDate"] },
  );
type CityPriceFormValues = z.infer<typeof cityPriceSchema>;

/** A single city price row's own edit state - price and effective dates are edited together since the update request requires all three (SRS 12.8.2 effective dating, task 109d). */
function CityPriceRow({
  cityPrice,
  canWrite,
  onSave,
  isSaving,
}: {
  cityPrice: CityPriceResponse;
  canWrite: boolean;
  onSave: (id: string, price: number, effectiveStartDate: string, effectiveEndDate: string | null) => void;
  isSaving: boolean;
}) {
  const [isEditing, setIsEditing] = useState(false);
  const [price, setPrice] = useState(String(cityPrice.price));
  const [startDate, setStartDate] = useState(cityPrice.effectiveStartDate);
  const [endDate, setEndDate] = useState(cityPrice.effectiveEndDate ?? "");

  if (!isEditing) {
    return (
      <tr className="border-b border-black/5 last:border-0 dark:border-white/10">
        <td className="px-3 py-2">{cityPrice.serviceName}</td>
        <td className="px-3 py-2">{cityPrice.cityName}</td>
        <td className="px-3 py-2 tabular-nums">₹{cityPrice.price.toFixed(2)}</td>
        <td className="px-3 py-2">{cityPrice.effectiveStartDate}</td>
        <td className="px-3 py-2">{cityPrice.effectiveEndDate ?? "No expiry"}</td>
        {canWrite ? (
          <td className="px-3 py-2">
            <Button type="button" variant="secondary" className="px-2 py-1 text-xs" onClick={() => setIsEditing(true)}>
              Edit
            </Button>
          </td>
        ) : null}
      </tr>
    );
  }

  const parsedPrice = Number(price);
  const isValid = price.trim() !== "" && !Number.isNaN(parsedPrice) && parsedPrice > 0 && startDate.trim() !== "";

  return (
    <tr className="border-b border-black/5 last:border-0 dark:border-white/10">
      <td className="px-3 py-2">{cityPrice.serviceName}</td>
      <td className="px-3 py-2">{cityPrice.cityName}</td>
      <td className="px-3 py-2">
        <input
          type="number"
          step="0.01"
          min="0.01"
          value={price}
          onChange={(event) => setPrice(event.target.value)}
          aria-label="Price"
          className="w-24 rounded-lg border border-black/15 bg-transparent px-2 py-1 text-sm outline-none focus:border-black focus:ring-1 focus:ring-black dark:border-white/20 dark:focus:border-white dark:focus:ring-white"
        />
      </td>
      <td className="px-3 py-2">
        <input
          type="date"
          value={startDate}
          onChange={(event) => setStartDate(event.target.value)}
          aria-label="Effective start date"
          className="rounded-lg border border-black/15 bg-transparent px-2 py-1 text-sm outline-none focus:border-black focus:ring-1 focus:ring-black dark:border-white/20 dark:focus:border-white dark:focus:ring-white"
        />
      </td>
      <td className="px-3 py-2">
        <input
          type="date"
          value={endDate}
          onChange={(event) => setEndDate(event.target.value)}
          aria-label="Effective end date (optional)"
          className="rounded-lg border border-black/15 bg-transparent px-2 py-1 text-sm outline-none focus:border-black focus:ring-1 focus:ring-black dark:border-white/20 dark:focus:border-white dark:focus:ring-white"
        />
      </td>
      <td className="px-3 py-2">
        <div className="flex gap-2">
          <Button
            type="button"
            className="px-2 py-1 text-xs"
            disabled={!isValid || isSaving}
            onClick={() => {
              onSave(cityPrice.id, parsedPrice, startDate, endDate.trim() === "" ? null : endDate);
              setIsEditing(false);
            }}
          >
            {isSaving ? "Saving…" : "Save"}
          </Button>
          <Button type="button" variant="secondary" className="px-2 py-1 text-xs" onClick={() => setIsEditing(false)}>
            Cancel
          </Button>
        </div>
      </td>
    </tr>
  );
}

/** City-wise price override (SRS 12.8.1 "City-wise price", task 109a) with effective dating (task 109d). */
export function CityPricesSection({ canWrite }: { canWrite: boolean }) {
  const queryClient = useQueryClient();

  const servicesQuery = useQuery({ queryKey: ["serviceability", "service-lookups"], queryFn: listServiceLookups });
  const citiesQuery = useQuery({ queryKey: ["cities"], queryFn: () => listCities() });
  const cityPricesQuery = useQuery({ queryKey: ["pricing", "city-prices"], queryFn: () => listCityPrices() });

  const form = useForm<CityPriceFormValues>({
    resolver: zodResolver(cityPriceSchema),
    defaultValues: { serviceId: "", cityId: "", price: "", effectiveStartDate: "", effectiveEndDate: "" },
  });

  const createMutation = useMutation({
    mutationFn: (values: CityPriceFormValues) =>
      createCityPrice({
        serviceId: values.serviceId,
        cityId: values.cityId,
        price: Number(values.price),
        effectiveStartDate: values.effectiveStartDate || null,
        effectiveEndDate: values.effectiveEndDate || null,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["pricing", "city-prices"] });
      form.reset({ serviceId: "", cityId: "", price: "", effectiveStartDate: "", effectiveEndDate: "" });
    },
  });

  const updateMutation = useMutation({
    mutationFn: ({
      id,
      price,
      effectiveStartDate,
      effectiveEndDate,
    }: {
      id: string;
      price: number;
      effectiveStartDate: string;
      effectiveEndDate: string | null;
    }) => updateCityPrice(id, { price, effectiveStartDate, effectiveEndDate }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["pricing", "city-prices"] }),
  });

  const serviceOptions = (servicesQuery.data ?? []).map((service) => ({ value: service.id, label: service.name }));
  const cityOptions = (citiesQuery.data ?? []).map((city) => ({ value: city.id, label: city.name }));
  const onSubmit = form.handleSubmit((values) => createMutation.mutate(values));

  return (
    <Card title="City-wise Price" description="Overrides the base price for a specific service in a specific city (SRS 12.8.1, 12.8.2).">
      {cityPricesQuery.isLoading ? <p className="text-sm text-neutral-600 dark:text-neutral-400">Loading…</p> : null}
      {cityPricesQuery.error ? <Alert>{describeError(cityPricesQuery.error)}</Alert> : null}
      {updateMutation.isError ? <Alert>{describeError(updateMutation.error)}</Alert> : null}

      {cityPricesQuery.data && cityPricesQuery.data.length > 0 ? (
        <div className="overflow-x-auto">
          <table className="w-full min-w-full text-left text-sm">
            <thead>
              <tr className="border-b border-black/10 text-xs uppercase tracking-wide text-neutral-500 dark:border-white/15 dark:text-neutral-400">
                <th scope="col" className="px-3 py-2 font-medium">Service</th>
                <th scope="col" className="px-3 py-2 font-medium">City</th>
                <th scope="col" className="px-3 py-2 font-medium">Price</th>
                <th scope="col" className="px-3 py-2 font-medium">Effective from</th>
                <th scope="col" className="px-3 py-2 font-medium">Effective until</th>
                {canWrite ? (
                  <th scope="col" className="px-3 py-2 font-medium">
                    <span className="sr-only">Actions</span>
                  </th>
                ) : null}
              </tr>
            </thead>
            <tbody>
              {cityPricesQuery.data.map((cityPrice) => (
                <CityPriceRow
                  key={cityPrice.id}
                  cityPrice={cityPrice}
                  canWrite={canWrite}
                  isSaving={updateMutation.isPending && updateMutation.variables?.id === cityPrice.id}
                  onSave={(id, price, effectiveStartDate, effectiveEndDate) =>
                    updateMutation.mutate({ id, price, effectiveStartDate, effectiveEndDate })
                  }
                />
              ))}
            </tbody>
          </table>
        </div>
      ) : cityPricesQuery.data ? (
        <p className="text-sm text-neutral-600 dark:text-neutral-400">No city price overrides yet.</p>
      ) : null}

      {canWrite ? (
        <form onSubmit={onSubmit} className="mt-4 flex flex-wrap items-end gap-3" noValidate>
          {createMutation.isError ? (
            <div className="w-full">
              <Alert>{describeError(createMutation.error)}</Alert>
            </div>
          ) : null}
          <div className="w-48">
            <Select
              label="Service"
              placeholder="Select a service…"
              error={form.formState.errors.serviceId?.message}
              options={serviceOptions}
              {...form.register("serviceId")}
            />
          </div>
          <div className="w-48">
            <Select
              label="City"
              placeholder="Select a city…"
              error={form.formState.errors.cityId?.message}
              options={cityOptions}
              {...form.register("cityId")}
            />
          </div>
          <div className="w-28">
            <Field
              label="Price"
              type="number"
              step="0.01"
              min="0.01"
              error={form.formState.errors.price?.message}
              {...form.register("price")}
            />
          </div>
          <div className="w-40">
            <Field
              label="Effective from"
              type="date"
              error={form.formState.errors.effectiveStartDate?.message}
              {...form.register("effectiveStartDate")}
            />
          </div>
          <div className="w-40">
            <Field
              label="Effective until (optional)"
              type="date"
              error={form.formState.errors.effectiveEndDate?.message}
              {...form.register("effectiveEndDate")}
            />
          </div>
          <Button type="submit" disabled={form.formState.isSubmitting || createMutation.isPending}>
            {createMutation.isPending ? "Adding…" : "Add city price"}
          </Button>
        </form>
      ) : null}
    </Card>
  );
}
