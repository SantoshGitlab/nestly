"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Button, Card, Field, Modal, Select } from "@/components/ui";
import { DataTable, FormActions, FormGrid, formatCurrency } from "@/components/data-table";
import type { DataTableColumn } from "@/components/data-table";
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

/**
 * Edit dialog for one city price. Price and effective dates are edited
 * together because the update request requires all three (SRS 12.8.2
 * effective dating, task 109d).
 *
 * This replaced a row that swapped itself into a set of bare `<input>`s in
 * place: a five-column row of unlabelled inputs is unreadable on a laptop, and
 * the row form had no way to report a failed save.
 */
function CityPriceEditor({
  cityPrice,
  saving,
  error,
  onSave,
  onClose,
}: {
  cityPrice: CityPriceResponse;
  saving: boolean;
  error: unknown;
  onSave: (price: number, effectiveStartDate: string, effectiveEndDate: string | null) => void;
  onClose: () => void;
}) {
  const [price, setPrice] = useState(String(cityPrice.price));
  const [startDate, setStartDate] = useState(cityPrice.effectiveStartDate);
  const [endDate, setEndDate] = useState(cityPrice.effectiveEndDate ?? "");

  const parsedPrice = Number(price);
  const datesOrdered = endDate.trim() === "" || endDate >= startDate;
  const isValid =
    price.trim() !== "" && !Number.isNaN(parsedPrice) && parsedPrice > 0 && startDate.trim() !== "" && datesOrdered;

  return (
    <Modal
      open
      onClose={onClose}
      title="Edit city price"
      description={`${cityPrice.serviceName} in ${cityPrice.cityName}`}
      footer={
        <>
          <Button variant="secondary" onClick={onClose} disabled={saving}>
            Cancel
          </Button>
          <Button
            disabled={!isValid}
            loading={saving}
            onClick={() => onSave(parsedPrice, startDate, endDate.trim() === "" ? null : endDate)}
          >
            Save price
          </Button>
        </>
      }
    >
      <div className="flex flex-col gap-4">
        {error ? <Alert>{describeError(error)}</Alert> : null}
        <Field
          label="Price"
          type="number"
          step="0.01"
          min="0.01"
          required
          leading="₹"
          value={price}
          onChange={(event) => setPrice(event.target.value)}
        />
        <FormGrid>
          <Field
            label="Effective from"
            type="date"
            required
            value={startDate}
            onChange={(event) => setStartDate(event.target.value)}
          />
          <Field
            label="Effective until"
            type="date"
            hint="Leave empty for no expiry."
            error={datesOrdered ? undefined : "The end date must not be before the start date."}
            value={endDate}
            onChange={(event) => setEndDate(event.target.value)}
          />
        </FormGrid>
      </div>
    </Modal>
  );
}

/** City-wise price override (SRS 12.8.1 "City-wise price", task 109a) with effective dating (task 109d). */
export function CityPricesSection({ canWrite }: { canWrite: boolean }) {
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState<CityPriceResponse | null>(null);

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
    onSuccess: () => {
      setEditing(null);
      queryClient.invalidateQueries({ queryKey: ["pricing", "city-prices"] });
    },
  });

  const serviceOptions = (servicesQuery.data ?? []).map((service) => ({ value: service.id, label: service.name }));
  const cityOptions = (citiesQuery.data ?? []).map((city) => ({ value: city.id, label: city.name }));
  const onSubmit = form.handleSubmit((values) => createMutation.mutate(values));

  const columns: DataTableColumn<CityPriceResponse>[] = [
    {
      key: "service",
      header: "Service",
      sortValue: (cityPrice) => cityPrice.serviceName,
      cell: (cityPrice) => <span className="font-medium text-fg">{cityPrice.serviceName}</span>,
    },
    { key: "city", header: "City", sortValue: (cityPrice) => cityPrice.cityName, cell: (cityPrice) => cityPrice.cityName },
    {
      key: "price",
      header: "Price",
      numeric: true,
      sortValue: (cityPrice) => cityPrice.price,
      cell: (cityPrice) => formatCurrency(cityPrice.price),
    },
    {
      key: "from",
      header: "Effective from",
      sortValue: (cityPrice) => cityPrice.effectiveStartDate,
      cell: (cityPrice) => <span className="nums">{cityPrice.effectiveStartDate}</span>,
    },
    {
      key: "until",
      header: "Effective until",
      sortValue: (cityPrice) => cityPrice.effectiveEndDate,
      cell: (cityPrice) =>
        cityPrice.effectiveEndDate ? (
          <span className="nums">{cityPrice.effectiveEndDate}</span>
        ) : (
          <span className="text-fg-subtle">No expiry</span>
        ),
    },
  ];

  return (
    <div className="flex flex-col gap-4">
      <DataTable
        title="City-wise price"
        description="Overrides the base price for a specific service in a specific city (SRS 12.8.1, 12.8.2)."
        columns={columns}
        rows={cityPricesQuery.data}
        rowKey={(cityPrice) => cityPrice.id}
        isLoading={cityPricesQuery.isPending}
        isFetching={cityPricesQuery.isFetching}
        error={cityPricesQuery.error}
        onRetry={() => cityPricesQuery.refetch()}
        caption="City price overrides"
        emptyTitle="No city price overrides yet"
        emptyDescription="Without an override, every city charges the service's base price."
        hideDensityToggle
        skeletonRows={4}
        minWidth="820px"
        maxHeight="420px"
        rowActions={
          canWrite
            ? (cityPrice) => (
                <Button size="sm" variant="ghost" onClick={() => setEditing(cityPrice)}>
                  Edit
                </Button>
              )
            : undefined
        }
      />

      {canWrite ? (
        <Card title="Add a city price" description="Effective dates are optional — leave both empty to apply immediately and indefinitely.">
          <form onSubmit={onSubmit} className="flex flex-col gap-4" noValidate>
            {createMutation.isError ? <Alert>{describeError(createMutation.error)}</Alert> : null}
            <FormGrid columns={3}>
              <Select
                label="Service"
                required
                placeholder="Select a service…"
                error={form.formState.errors.serviceId?.message}
                options={serviceOptions}
                {...form.register("serviceId")}
              />
              <Select
                label="City"
                required
                placeholder="Select a city…"
                error={form.formState.errors.cityId?.message}
                options={cityOptions}
                {...form.register("cityId")}
              />
              <Field
                label="Price"
                type="number"
                step="0.01"
                min="0.01"
                required
                leading="₹"
                error={form.formState.errors.price?.message}
                {...form.register("price")}
              />
              <Field
                label="Effective from"
                type="date"
                error={form.formState.errors.effectiveStartDate?.message}
                {...form.register("effectiveStartDate")}
              />
              <Field
                label="Effective until"
                type="date"
                hint="Leave empty for no expiry."
                error={form.formState.errors.effectiveEndDate?.message}
                {...form.register("effectiveEndDate")}
              />
            </FormGrid>
            <FormActions>
              <Button type="submit" loading={form.formState.isSubmitting || createMutation.isPending}>
                Add city price
              </Button>
            </FormActions>
          </form>
        </Card>
      ) : null}

      {editing ? (
        <CityPriceEditor
          key={editing.id}
          cityPrice={editing}
          saving={updateMutation.isPending}
          error={updateMutation.isError ? updateMutation.error : null}
          onClose={() => setEditing(null)}
          onSave={(price, effectiveStartDate, effectiveEndDate) =>
            updateMutation.mutate({ id: editing.id, price, effectiveStartDate, effectiveEndDate })
          }
        />
      ) : null}
    </div>
  );
}
