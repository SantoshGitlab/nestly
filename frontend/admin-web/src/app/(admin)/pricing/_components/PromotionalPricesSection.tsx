"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Button, Card, Field, Select } from "@/components/ui";
import { FormActions, FormGrid, formatCurrency } from "@/components/data-table";
import { EntityTable } from "@/components/entity-table";
import { describeError } from "@/lib/api";
import { createPromotionalPrice, listPromotionalPrices, setPromotionalPriceActive } from "@/lib/pricing-api";
import type { PromotionalPriceResponse } from "@/lib/pricing-types";
import { listCities, listServiceLookups } from "@/lib/serviceability-api";

const promotionSchema = z
  .object({
    serviceId: z.string().min(1, "Select a service"),
    cityId: z.string().optional(),
    discountedPrice: z
      .string()
      .min(1, "Price is required")
      .refine((value) => !Number.isNaN(Number(value)) && Number(value) > 0, "Price must be greater than zero"),
    startDate: z.string().min(1, "Start date is required"),
    endDate: z.string().min(1, "End date is required"),
  })
  .refine((values) => values.endDate >= values.startDate, {
    message: "The end date must not be before the start date.",
    path: ["endDate"],
  });
type PromotionFormValues = z.infer<typeof promotionSchema>;

/** Promotional price (SRS 12.8.1 "Promotional price", task 109a) - a scheduled discount, optionally scoped to one city. */
export function PromotionalPricesSection({ canWrite }: { canWrite: boolean }) {
  const queryClient = useQueryClient();

  const servicesQuery = useQuery({ queryKey: ["serviceability", "service-lookups"], queryFn: listServiceLookups });
  const citiesQuery = useQuery({ queryKey: ["cities"], queryFn: () => listCities() });
  const promotionsQuery = useQuery({ queryKey: ["pricing", "promotions"], queryFn: () => listPromotionalPrices() });

  const form = useForm<PromotionFormValues>({
    resolver: zodResolver(promotionSchema),
    defaultValues: { serviceId: "", cityId: "", discountedPrice: "", startDate: "", endDate: "" },
  });

  const createMutation = useMutation({
    mutationFn: (values: PromotionFormValues) =>
      createPromotionalPrice({
        serviceId: values.serviceId,
        cityId: values.cityId || null,
        discountedPrice: Number(values.discountedPrice),
        startDate: values.startDate,
        endDate: values.endDate,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["pricing", "promotions"] });
      form.reset({ serviceId: "", cityId: "", discountedPrice: "", startDate: "", endDate: "" });
    },
  });

  const toggleMutation = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => setPromotionalPriceActive(id, isActive),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["pricing", "promotions"] }),
  });

  const serviceOptions = (servicesQuery.data ?? []).map((service) => ({ value: service.id, label: service.name }));
  const cityOptions = (citiesQuery.data ?? []).map((city) => ({ value: city.id, label: city.name }));
  const onSubmit = form.handleSubmit((values) => createMutation.mutate(values));

  return (
    <div className="flex flex-col gap-4">
      <EntityTable<PromotionalPriceResponse>
        title="Promotional price"
        description="A scheduled discounted price for a service, optionally scoped to one city (SRS 12.8.1)."
        items={promotionsQuery.data}
        isLoading={promotionsQuery.isPending}
        isFetching={promotionsQuery.isFetching}
        error={promotionsQuery.error}
        onRetry={() => promotionsQuery.refetch()}
        emptyMessage="No promotional prices yet"
        canWrite={canWrite}
        entityLabel="promotion"
        labelOf={(promotion) => `${promotion.serviceName} · ${promotion.cityName ?? "All cities"}`}
        hideDensityToggle
        skeletonRows={4}
        minWidth="880px"
        maxHeight="420px"
        togglingId={toggleMutation.isPending ? toggleMutation.variables?.id : undefined}
        toggleError={toggleMutation.error}
        onToggleActive={(promotion) => toggleMutation.mutate({ id: promotion.id, isActive: !promotion.isActive })}
        columns={[
          {
            header: "Service",
            sortValue: (promotion) => promotion.serviceName,
            render: (promotion) => <span className="font-medium text-fg">{promotion.serviceName}</span>,
          },
          {
            header: "City",
            sortValue: (promotion) => promotion.cityName,
            render: (promotion) => promotion.cityName ?? <span className="text-fg-subtle">All cities</span>,
          },
          {
            header: "Discounted price",
            numeric: true,
            sortValue: (promotion) => promotion.discountedPrice,
            render: (promotion) => formatCurrency(promotion.discountedPrice),
          },
          {
            header: "Starts",
            sortValue: (promotion) => promotion.startDate,
            render: (promotion) => <span className="nums">{promotion.startDate}</span>,
          },
          {
            header: "Ends",
            sortValue: (promotion) => promotion.endDate,
            render: (promotion) => <span className="nums">{promotion.endDate}</span>,
          },
        ]}
      />

      {canWrite ? (
        <Card title="Add a promotion" description="Leave the city empty to run the discount everywhere.">
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
                hint="Optional — applies everywhere if left as All cities."
                options={[{ value: "", label: "All cities" }, ...cityOptions]}
                {...form.register("cityId")}
              />
              <Field
                label="Discounted price"
                type="number"
                step="0.01"
                min="0.01"
                required
                leading="₹"
                error={form.formState.errors.discountedPrice?.message}
                {...form.register("discountedPrice")}
              />
              <Field
                label="Start date"
                type="date"
                required
                error={form.formState.errors.startDate?.message}
                {...form.register("startDate")}
              />
              <Field
                label="End date"
                type="date"
                required
                error={form.formState.errors.endDate?.message}
                {...form.register("endDate")}
              />
            </FormGrid>
            <FormActions>
              <Button type="submit" loading={form.formState.isSubmitting || createMutation.isPending}>
                Add promotion
              </Button>
            </FormActions>
          </form>
        </Card>
      ) : null}
    </div>
  );
}
