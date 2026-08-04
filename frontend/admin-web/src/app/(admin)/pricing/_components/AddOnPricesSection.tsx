"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { Alert, Button, Select } from "@/components/ui";
import { DataTable } from "@/components/data-table";
import type { DataTableColumn } from "@/components/data-table";
import { describeError } from "@/lib/api";
import { listAddOnPrices, updateAddOnPrice } from "@/lib/pricing-api";
import type { AddOnPriceResponse } from "@/lib/pricing-types";
import { listServiceLookups } from "@/lib/serviceability-api";
import { PriceEditCell } from "./PriceEditCell";

/** Add-on price (SRS 12.8.1 "Add-on price", task 109a). */
export function AddOnPricesSection({ canWrite }: { canWrite: boolean }) {
  const queryClient = useQueryClient();
  const [serviceFilter, setServiceFilter] = useState("");

  const servicesQuery = useQuery({ queryKey: ["serviceability", "service-lookups"], queryFn: listServiceLookups });
  const addOnsQuery = useQuery({
    queryKey: ["pricing", "addons", serviceFilter],
    queryFn: () => listAddOnPrices(serviceFilter || undefined),
  });

  const updateMutation = useMutation({
    mutationFn: ({ addOnId, price }: { addOnId: string; price: number }) => updateAddOnPrice(addOnId, { price }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["pricing", "addons"] }),
  });

  const serviceOptions = (servicesQuery.data ?? []).map((service) => ({ value: service.id, label: service.name }));

  const columns: DataTableColumn<AddOnPriceResponse>[] = [
    {
      key: "addon",
      header: "Add-on",
      sortValue: (addOn) => addOn.addOnName,
      cell: (addOn) => <span className="font-medium text-fg">{addOn.addOnName}</span>,
    },
    {
      key: "service",
      header: "Service",
      sortValue: (addOn) => addOn.serviceName,
      cell: (addOn) => addOn.serviceName,
    },
    {
      key: "price",
      header: "Price",
      numeric: true,
      sortValue: (addOn) => addOn.price,
      cell: (addOn) => (
        <PriceEditCell
          value={addOn.price}
          canWrite={canWrite}
          isSaving={updateMutation.isPending && updateMutation.variables?.addOnId === addOn.addOnId}
          onSave={(price) => updateMutation.mutate({ addOnId: addOn.addOnId, price })}
        />
      ),
    },
  ];

  return (
    <div className="flex flex-col gap-3">
      {updateMutation.isError ? <Alert>{describeError(updateMutation.error)}</Alert> : null}
      <DataTable
        title="Add-on price"
        description="Prices for optional add-ons attached to a service."
        actions={
          <div className="w-56">
            <Select
              label="Filter by service"
              value={serviceFilter}
              onChange={(event) => setServiceFilter(event.target.value)}
              options={[{ value: "", label: "All services" }, ...serviceOptions]}
            />
          </div>
        }
        columns={columns}
        rows={addOnsQuery.data}
        rowKey={(addOn) => addOn.addOnId}
        isLoading={addOnsQuery.isPending}
        isFetching={addOnsQuery.isFetching}
        error={addOnsQuery.error}
        onRetry={() => addOnsQuery.refetch()}
        caption="Add-on prices"
        emptyTitle={serviceFilter ? "No add-ons for this service" : "No add-ons yet"}
        emptyDescription="Add-ons are created in the catalog; their price is set here."
        emptyAction={
          serviceFilter ? (
            <Button variant="secondary" onClick={() => setServiceFilter("")}>
              Show all services
            </Button>
          ) : undefined
        }
        hideDensityToggle
        skeletonRows={4}
        minWidth="640px"
      />
    </div>
  );
}
