"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Alert, Card } from "@/components/ui";
import { describeError } from "@/lib/api";
import { listServicePrices, updateServicePrice } from "@/lib/pricing-api";
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

  return (
    <Card title="Base Price" description="The price charged for each service before add-ons, city overrides, taxes or fees.">
      {pricesQuery.isLoading ? <p className="text-sm text-neutral-600 dark:text-neutral-400">Loading…</p> : null}
      {pricesQuery.error ? <Alert>{describeError(pricesQuery.error)}</Alert> : null}
      {updateMutation.isError ? <Alert>{describeError(updateMutation.error)}</Alert> : null}

      {pricesQuery.data && pricesQuery.data.length > 0 ? (
        <div className="overflow-x-auto">
          <table className="w-full min-w-full text-left text-sm">
            <thead>
              <tr className="border-b border-black/10 text-xs uppercase tracking-wide text-neutral-500 dark:border-white/15 dark:text-neutral-400">
                <th scope="col" className="px-3 py-2 font-medium">Service</th>
                <th scope="col" className="px-3 py-2 font-medium">Price</th>
              </tr>
            </thead>
            <tbody>
              {pricesQuery.data.map((service) => (
                <tr key={service.serviceId} className="border-b border-black/5 last:border-0 dark:border-white/10">
                  <td className="px-3 py-2">{service.serviceName}</td>
                  <td className="px-3 py-2">
                    <PriceEditCell
                      value={service.price}
                      canWrite={canWrite}
                      isSaving={
                        updateMutation.isPending && updateMutation.variables?.serviceId === service.serviceId
                      }
                      onSave={(price) => updateMutation.mutate({ serviceId: service.serviceId, price })}
                    />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : pricesQuery.data ? (
        <p className="text-sm text-neutral-600 dark:text-neutral-400">No services yet.</p>
      ) : null}
    </Card>
  );
}
