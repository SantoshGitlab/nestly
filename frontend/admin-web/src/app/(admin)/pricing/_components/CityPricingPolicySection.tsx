"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { Alert, Button, Card, Select } from "@/components/ui";
import { describeError } from "@/lib/api";
import { listCityPricingPolicies, upsertCityPricingPolicy } from "@/lib/pricing-api";
import type { CityPricingPolicyResponse } from "@/lib/pricing-types";
import { listCities } from "@/lib/serviceability-api";

/** A single city's tax/fee policy row, edited as one unit since the upsert request carries all three fields together. */
function CityPricingPolicyRow({
  policy,
  canWrite,
  onSave,
  isSaving,
}: {
  policy: CityPricingPolicyResponse;
  canWrite: boolean;
  onSave: (cityId: string, visitCharge: number, taxPercentage: number, platformFee: number) => void;
  isSaving: boolean;
}) {
  const [isEditing, setIsEditing] = useState(false);
  const [visitCharge, setVisitCharge] = useState(String(policy.visitCharge));
  const [taxPercentage, setTaxPercentage] = useState(String(policy.taxPercentage));
  const [platformFee, setPlatformFee] = useState(String(policy.platformFee));

  if (!isEditing) {
    return (
      <tr className="border-b border-black/5 last:border-0 dark:border-white/10">
        <td className="px-3 py-2">{policy.cityName}</td>
        <td className="px-3 py-2 tabular-nums">₹{policy.visitCharge.toFixed(2)}</td>
        <td className="px-3 py-2 tabular-nums">{policy.taxPercentage.toFixed(2)}%</td>
        <td className="px-3 py-2 tabular-nums">₹{policy.platformFee.toFixed(2)}</td>
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

  const parsedVisitCharge = Number(visitCharge);
  const parsedTaxPercentage = Number(taxPercentage);
  const parsedPlatformFee = Number(platformFee);
  const isValid =
    !Number.isNaN(parsedVisitCharge) &&
    parsedVisitCharge >= 0 &&
    !Number.isNaN(parsedTaxPercentage) &&
    parsedTaxPercentage >= 0 &&
    parsedTaxPercentage <= 100 &&
    !Number.isNaN(parsedPlatformFee) &&
    parsedPlatformFee >= 0;

  return (
    <tr className="border-b border-black/5 last:border-0 dark:border-white/10">
      <td className="px-3 py-2">{policy.cityName}</td>
      <td className="px-3 py-2">
        <input
          type="number"
          step="0.01"
          min="0"
          value={visitCharge}
          onChange={(event) => setVisitCharge(event.target.value)}
          aria-label="Visit charge"
          className="w-24 rounded-lg border border-black/15 bg-transparent px-2 py-1 text-sm outline-none focus:border-black focus:ring-1 focus:ring-black dark:border-white/20 dark:focus:border-white dark:focus:ring-white"
        />
      </td>
      <td className="px-3 py-2">
        <input
          type="number"
          step="0.01"
          min="0"
          max="100"
          value={taxPercentage}
          onChange={(event) => setTaxPercentage(event.target.value)}
          aria-label="Tax percentage"
          className="w-20 rounded-lg border border-black/15 bg-transparent px-2 py-1 text-sm outline-none focus:border-black focus:ring-1 focus:ring-black dark:border-white/20 dark:focus:border-white dark:focus:ring-white"
        />
      </td>
      <td className="px-3 py-2">
        <input
          type="number"
          step="0.01"
          min="0"
          value={platformFee}
          onChange={(event) => setPlatformFee(event.target.value)}
          aria-label="Platform fee"
          className="w-24 rounded-lg border border-black/15 bg-transparent px-2 py-1 text-sm outline-none focus:border-black focus:ring-1 focus:ring-black dark:border-white/20 dark:focus:border-white dark:focus:ring-white"
        />
      </td>
      <td className="px-3 py-2">
        <div className="flex gap-2">
          <Button
            type="button"
            className="px-2 py-1 text-xs"
            disabled={!isValid || isSaving}
            onClick={() => {
              onSave(policy.cityId, parsedVisitCharge, parsedTaxPercentage, parsedPlatformFee);
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

/** Per-city tax rate, visit charge and platform/convenience fee (SRS 12.8.1 "Tax configuration", "Visit charge", "Convenience fee"; tasks 109b/109c). */
export function CityPricingPolicySection({ canWrite }: { canWrite: boolean }) {
  const queryClient = useQueryClient();

  const citiesQuery = useQuery({ queryKey: ["cities"], queryFn: () => listCities() });
  const policiesQuery = useQuery({ queryKey: ["pricing", "policies"], queryFn: listCityPricingPolicies });
  const [newCityId, setNewCityId] = useState("");

  const upsertMutation = useMutation({
    mutationFn: ({
      cityId,
      visitCharge,
      taxPercentage,
      platformFee,
    }: {
      cityId: string;
      visitCharge: number;
      taxPercentage: number;
      platformFee: number;
    }) => upsertCityPricingPolicy(cityId, { visitCharge, taxPercentage, platformFee }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["pricing", "policies"] });
      setNewCityId("");
    },
  });

  const configuredCityIds = new Set((policiesQuery.data ?? []).map((policy) => policy.cityId));
  const unconfiguredCityOptions = (citiesQuery.data ?? [])
    .filter((city) => !configuredCityIds.has(city.id))
    .map((city) => ({ value: city.id, label: city.name }));

  return (
    <Card title="Tax & Fees" description="Per-city tax rate, visit charge and platform/convenience fee (SRS 12.8.1).">
      {policiesQuery.isLoading ? <p className="text-sm text-neutral-600 dark:text-neutral-400">Loading…</p> : null}
      {policiesQuery.error ? <Alert>{describeError(policiesQuery.error)}</Alert> : null}
      {upsertMutation.isError ? <Alert>{describeError(upsertMutation.error)}</Alert> : null}

      {policiesQuery.data && policiesQuery.data.length > 0 ? (
        <div className="overflow-x-auto">
          <table className="w-full min-w-full text-left text-sm">
            <thead>
              <tr className="border-b border-black/10 text-xs uppercase tracking-wide text-neutral-500 dark:border-white/15 dark:text-neutral-400">
                <th scope="col" className="px-3 py-2 font-medium">City</th>
                <th scope="col" className="px-3 py-2 font-medium">Visit charge</th>
                <th scope="col" className="px-3 py-2 font-medium">Tax %</th>
                <th scope="col" className="px-3 py-2 font-medium">Platform fee</th>
                {canWrite ? (
                  <th scope="col" className="px-3 py-2 font-medium">
                    <span className="sr-only">Actions</span>
                  </th>
                ) : null}
              </tr>
            </thead>
            <tbody>
              {policiesQuery.data.map((policy) => (
                <CityPricingPolicyRow
                  key={policy.id}
                  policy={policy}
                  canWrite={canWrite}
                  isSaving={upsertMutation.isPending && upsertMutation.variables?.cityId === policy.cityId}
                  onSave={(cityId, visitCharge, taxPercentage, platformFee) =>
                    upsertMutation.mutate({ cityId, visitCharge, taxPercentage, platformFee })
                  }
                />
              ))}
            </tbody>
          </table>
        </div>
      ) : policiesQuery.data ? (
        <p className="text-sm text-neutral-600 dark:text-neutral-400">No city pricing policies configured yet.</p>
      ) : null}

      {canWrite && unconfiguredCityOptions.length > 0 ? (
        <div className="mt-4 flex flex-wrap items-end gap-3">
          <div className="w-48">
            <Select
              label="Configure a new city"
              placeholder="Select a city…"
              value={newCityId}
              onChange={(event) => setNewCityId(event.target.value)}
              options={unconfiguredCityOptions}
            />
          </div>
          <Button
            type="button"
            disabled={!newCityId || upsertMutation.isPending}
            onClick={() => upsertMutation.mutate({ cityId: newCityId, visitCharge: 0, taxPercentage: 0, platformFee: 0 })}
          >
            Add city policy
          </Button>
        </div>
      ) : null}
    </Card>
  );
}
