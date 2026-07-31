"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { Alert, Card, Select } from "@/components/ui";
import { describeError } from "@/lib/api";
import { listAddOnPrices, updateAddOnPrice } from "@/lib/pricing-api";
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

  return (
    <Card title="Add-on Price" description="Prices for optional add-ons attached to a service.">
      <div className="mb-4 w-64">
        <Select
          label="Filter by service"
          value={serviceFilter}
          onChange={(event) => setServiceFilter(event.target.value)}
          options={[{ value: "", label: "All services" }, ...serviceOptions]}
        />
      </div>

      {addOnsQuery.isLoading ? <p className="text-sm text-neutral-600 dark:text-neutral-400">Loading…</p> : null}
      {addOnsQuery.error ? <Alert>{describeError(addOnsQuery.error)}</Alert> : null}
      {updateMutation.isError ? <Alert>{describeError(updateMutation.error)}</Alert> : null}

      {addOnsQuery.data && addOnsQuery.data.length > 0 ? (
        <div className="overflow-x-auto">
          <table className="w-full min-w-full text-left text-sm">
            <thead>
              <tr className="border-b border-black/10 text-xs uppercase tracking-wide text-neutral-500 dark:border-white/15 dark:text-neutral-400">
                <th scope="col" className="px-3 py-2 font-medium">Add-on</th>
                <th scope="col" className="px-3 py-2 font-medium">Service</th>
                <th scope="col" className="px-3 py-2 font-medium">Price</th>
              </tr>
            </thead>
            <tbody>
              {addOnsQuery.data.map((addOn) => (
                <tr key={addOn.addOnId} className="border-b border-black/5 last:border-0 dark:border-white/10">
                  <td className="px-3 py-2">{addOn.addOnName}</td>
                  <td className="px-3 py-2">{addOn.serviceName}</td>
                  <td className="px-3 py-2">
                    <PriceEditCell
                      value={addOn.price}
                      canWrite={canWrite}
                      isSaving={updateMutation.isPending && updateMutation.variables?.addOnId === addOn.addOnId}
                      onSave={(price) => updateMutation.mutate({ addOnId: addOn.addOnId, price })}
                    />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : addOnsQuery.data ? (
        <p className="text-sm text-neutral-600 dark:text-neutral-400">No add-ons yet.</p>
      ) : null}
    </Card>
  );
}
