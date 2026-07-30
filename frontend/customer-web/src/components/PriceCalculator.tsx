"use client";

import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { Alert } from "@/components/ui";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import type { PriceBreakdown, PriceCalculationRequest, ServiceAddOnSummary } from "@/lib/types";

/**
 * Live, server-calculated price breakdown (SRS 11.6.1, 11.9 - "final price
 * must be calculated server-side", task 48). Quantity and add-on selections
 * are local UI state; every change re-requests the authoritative total
 * rather than computing it client-side.
 */
export function PriceCalculator({
  serviceId,
  addOns,
  cityId,
}: {
  serviceId: string;
  addOns: ServiceAddOnSummary[];
  cityId: string | null;
}) {
  const [quantity, setQuantity] = useState(1);
  const [selectedAddOnIds, setSelectedAddOnIds] = useState<Set<string>>(new Set());

  const toggleAddOn = (id: string) => {
    setSelectedAddOnIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  };

  const request: PriceCalculationRequest | null = cityId
    ? {
        serviceId,
        cityId,
        quantity,
        addOns: Array.from(selectedAddOnIds, (addOnId) => ({ addOnId, quantity: 1 })),
      }
    : null;

  const query = useQuery({
    queryKey: ["price-calculation", request],
    queryFn: () =>
      apiFetch<PriceBreakdown>(`${API_V1}/pricing/calculate`, {
        method: "POST",
        body: JSON.stringify(request),
      }),
    enabled: request !== null,
  });

  return (
    <div className="flex flex-col gap-4 rounded-xl border border-black/10 bg-white p-5 dark:border-white/15 dark:bg-neutral-900">
      <div className="flex items-center justify-between">
        <label htmlFor="quantity" className="text-sm font-medium">
          Quantity
        </label>
        <div className="flex items-center gap-3">
          <button
            type="button"
            aria-label="Decrease quantity"
            onClick={() => setQuantity((q) => Math.max(1, q - 1))}
            className="h-8 w-8 rounded-lg border border-black/15 text-sm dark:border-white/20"
          >
            −
          </button>
          <span id="quantity" className="w-6 text-center text-sm">
            {quantity}
          </span>
          <button
            type="button"
            aria-label="Increase quantity"
            onClick={() => setQuantity((q) => q + 1)}
            className="h-8 w-8 rounded-lg border border-black/15 text-sm dark:border-white/20"
          >
            +
          </button>
        </div>
      </div>

      {addOns.length > 0 ? (
        <fieldset className="flex flex-col gap-2">
          <legend className="text-sm font-medium">Add-ons</legend>
          {addOns.map((addOn) => (
            <label key={addOn.id} className="flex items-center justify-between gap-3 text-sm">
              <span className="flex items-center gap-2">
                <input
                  type="checkbox"
                  checked={selectedAddOnIds.has(addOn.id)}
                  onChange={() => toggleAddOn(addOn.id)}
                  className="h-4 w-4 rounded border-black/25 dark:border-white/30"
                />
                {addOn.name}
              </span>
              <span className="text-neutral-500">+₹{addOn.price}</span>
            </label>
          ))}
        </fieldset>
      ) : null}

      <div className="border-t border-black/10 pt-4 dark:border-white/15">
        {cityId === null ? (
          <p className="text-sm text-neutral-500">Select your city to see the total price.</p>
        ) : query.isPending ? (
          <p className="text-sm text-neutral-500">Calculating price…</p>
        ) : query.isError ? (
          <Alert>{describeError(query.error)}</Alert>
        ) : (
          <PriceSummary breakdown={query.data} />
        )}
      </div>
    </div>
  );
}

function PriceSummary({ breakdown }: { breakdown: PriceBreakdown }) {
  return (
    <dl className="flex flex-col gap-1.5 text-sm">
      <Row label={`Base price × ${breakdown.quantity}`} value={breakdown.baseTotal} />
      {breakdown.addOnLineItems.map((item) => (
        <Row key={item.addOnId} label={`${item.name} × ${item.quantity}`} value={item.lineTotal} />
      ))}
      {breakdown.visitCharge > 0 ? <Row label="Visit charge" value={breakdown.visitCharge} /> : null}
      <Row label={`Tax (${breakdown.taxPercentage}%)`} value={breakdown.taxAmount} />
      {breakdown.platformFee > 0 ? <Row label="Platform fee" value={breakdown.platformFee} /> : null}
      <div className="mt-1 flex items-center justify-between border-t border-black/10 pt-2 font-semibold dark:border-white/15">
        <dt>Total payable</dt>
        <dd>₹{breakdown.totalPayable.toFixed(2)}</dd>
      </div>
    </dl>
  );
}

function Row({ label, value }: { label: string; value: number }) {
  return (
    <div className="flex items-center justify-between text-neutral-600 dark:text-neutral-400">
      <dt>{label}</dt>
      <dd>₹{value.toFixed(2)}</dd>
    </div>
  );
}
