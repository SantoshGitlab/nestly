"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Button, Card, Field, Select } from "@/components/ui";
import { describeError } from "@/lib/api";
import { createPromotionalPrice, listPromotionalPrices, setPromotionalPriceActive } from "@/lib/pricing-api";
import type { PromotionalPriceResponse } from "@/lib/pricing-types";
import { listCities, listServiceLookups } from "@/lib/serviceability-api";
import { EntityTable } from "../../serviceability/_components/EntityTable";

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
    <Card title="Promotional Price" description="A scheduled discounted price for a service, optionally scoped to one city (SRS 12.8.1).">
      <EntityTable<PromotionalPriceResponse>
        items={promotionsQuery.data}
        isLoading={promotionsQuery.isLoading}
        errorMessage={promotionsQuery.error ? describeError(promotionsQuery.error) : null}
        emptyMessage="No promotional prices yet."
        canWrite={canWrite}
        togglingId={toggleMutation.isPending ? toggleMutation.variables?.id : undefined}
        onToggleActive={(promotion) => toggleMutation.mutate({ id: promotion.id, isActive: !promotion.isActive })}
        columns={[
          { header: "Service", render: (promotion) => promotion.serviceName },
          { header: "City", render: (promotion) => promotion.cityName ?? "All cities" },
          { header: "Discounted price", render: (promotion) => `₹${promotion.discountedPrice.toFixed(2)}` },
          { header: "Starts", render: (promotion) => promotion.startDate },
          { header: "Ends", render: (promotion) => promotion.endDate },
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
              label="Service"
              placeholder="Select a service…"
              error={form.formState.errors.serviceId?.message}
              options={serviceOptions}
              {...form.register("serviceId")}
            />
          </div>
          <div className="w-48">
            <Select
              label="City (optional)"
              options={[{ value: "", label: "All cities" }, ...cityOptions]}
              {...form.register("cityId")}
            />
          </div>
          <div className="w-28">
            <Field
              label="Discounted price"
              type="number"
              step="0.01"
              min="0.01"
              error={form.formState.errors.discountedPrice?.message}
              {...form.register("discountedPrice")}
            />
          </div>
          <div className="w-40">
            <Field
              label="Start date"
              type="date"
              error={form.formState.errors.startDate?.message}
              {...form.register("startDate")}
            />
          </div>
          <div className="w-40">
            <Field
              label="End date"
              type="date"
              error={form.formState.errors.endDate?.message}
              {...form.register("endDate")}
            />
          </div>
          <Button type="submit" disabled={form.formState.isSubmitting || createMutation.isPending}>
            {createMutation.isPending ? "Adding…" : "Add promotion"}
          </Button>
        </form>
      ) : null}
    </Card>
  );
}
