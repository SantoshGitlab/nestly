"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Alert } from "@/components/ui";
import { DataTable } from "@/components/data-table";
import type { DataTableColumn } from "@/components/data-table";
import { describeError } from "@/lib/api";
import { listServicePrices, updateServicePrice } from "@/lib/pricing-api";
import type { ServicePriceResponse } from "@/lib/pricing-types";
import { PriceEditCell } from "./PriceEditCell";

/** Base service price (SRS 12.8.1 "Base service price", task 109a). */
export function ServicePricesSection({ canWrite }: { canWrite: boolean }) {
  const queryClient = useQueryClient();
  const pricesQuery = useQuery({ queryKey: ["pricing", "services"], queryFn: listServicePrices });

  const updateMutation = useMutation({
    mutationFn: ({ serviceId, price }: { serviceId: string; price: number }) =>
      updateServicePrice(serviceId, { price }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["pricing", "services"] }),
  });

  // The whole list is held client-side, so sorting here is honest — it is not
  // reordering one page of a server-paged result.
  const columns: DataTableColumn<ServicePriceResponse>[] = [
    {
      key: "service",
      header: "Service",
      sortValue: (service) => service.serviceName,
      cell: (service) => <span className="font-medium text-fg">{service.serviceName}</span>,
    },
    {
      key: "price",
      header: "Price",
      numeric: true,
      sortValue: (service) => service.price,
      cell: (service) => (
        <PriceEditCell
          value={service.price}
          canWrite={canWrite}
          isSaving={updateMutation.isPending && updateMutation.variables?.serviceId === service.serviceId}
          onSave={(price) => updateMutation.mutate({ serviceId: service.serviceId, price })}
        />
      ),
    },
  ];

  return (
    <div className="flex flex-col gap-3">
      {updateMutation.isError ? <Alert>{describeError(updateMutation.error)}</Alert> : null}
      <DataTable
        title="Base price"
        description="The price charged for each service before add-ons, city overrides, taxes or fees."
        columns={columns}
        rows={pricesQuery.data}
        rowKey={(service) => service.serviceId}
        isLoading={pricesQuery.isPending}
        isFetching={pricesQuery.isFetching}
        error={pricesQuery.error}
        onRetry={() => pricesQuery.refetch()}
        caption="Base service prices"
        emptyTitle="No services yet"
        emptyDescription="Create a service in the catalog first — a price has nothing to attach to until then."
        hideDensityToggle
        skeletonRows={4}
      />
    </div>
  );
}
