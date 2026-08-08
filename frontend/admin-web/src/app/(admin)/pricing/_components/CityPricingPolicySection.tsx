"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { Alert, Button, Field, Modal, Select } from "@/components/ui";
import { DataTable, FormGrid, formatCurrency } from "@/components/data-table";
import type { DataTableColumn } from "@/components/data-table";
import { describeError } from "@/lib/api";
import { listCityPricingPolicies, upsertCityPricingPolicy } from "@/lib/pricing-api";
import type { CityPricingPolicyResponse } from "@/lib/pricing-types";
import { listCities } from "@/lib/serviceability-api";

/**
 * Edit dialog for one city's tax/fee policy, edited as one unit since the
 * upsert request carries all three fields together. It replaced a row that
 * swapped into four unlabelled bare `<input>`s in place.
 */
function CityPricingPolicyEditor({
  policy,
  saving,
  error,
  onSave,
  onClose,
}: {
  policy: CityPricingPolicyResponse;
  saving: boolean;
  error: unknown;
  onSave: (visitCharge: number, taxPercentage: number, platformFee: number) => void;
  onClose: () => void;
}) {
  const [visitCharge, setVisitCharge] = useState(String(policy.visitCharge));
  const [taxPercentage, setTaxPercentage] = useState(String(policy.taxPercentage));
  const [platformFee, setPlatformFee] = useState(String(policy.platformFee));

  const parsedVisitCharge = Number(visitCharge);
  const parsedTaxPercentage = Number(taxPercentage);
  const parsedPlatformFee = Number(platformFee);
  const taxInRange = !Number.isNaN(parsedTaxPercentage) && parsedTaxPercentage >= 0 && parsedTaxPercentage <= 100;
  const isValid =
    !Number.isNaN(parsedVisitCharge) &&
    parsedVisitCharge >= 0 &&
    taxInRange &&
    !Number.isNaN(parsedPlatformFee) &&
    parsedPlatformFee >= 0;

  return (
    <Modal
      open
      onClose={onClose}
      title="Edit tax & fees"
      description={policy.cityName}
      footer={
        <>
          <Button variant="secondary" onClick={onClose} disabled={saving}>
            Cancel
          </Button>
          <Button
            disabled={!isValid}
            loading={saving}
            onClick={() => onSave(parsedVisitCharge, parsedTaxPercentage, parsedPlatformFee)}
          >
            Save policy
          </Button>
        </>
      }
    >
      <div className="flex flex-col gap-4">
        {error ? <Alert>{describeError(error)}</Alert> : null}
        <FormGrid>
          <Field
            label="Visit charge"
            type="number"
            step="0.01"
            min="0"
            required
            leading="₹"
            value={visitCharge}
            onChange={(event) => setVisitCharge(event.target.value)}
          />
          <Field
            label="Platform fee"
            type="number"
            step="0.01"
            min="0"
            required
            leading="₹"
            value={platformFee}
            onChange={(event) => setPlatformFee(event.target.value)}
          />
        </FormGrid>
        <Field
          label="Tax percentage"
          type="number"
          step="0.01"
          min="0"
          max="100"
          required
          hint="Applied to the taxable portion of the booking total."
          error={taxInRange ? undefined : "Enter a percentage between 0 and 100."}
          value={taxPercentage}
          onChange={(event) => setTaxPercentage(event.target.value)}
        />
      </div>
    </Modal>
  );
}

/** Per-city tax rate, visit charge and platform/convenience fee (SRS 12.8.1 "Tax configuration", "Visit charge", "Convenience fee"; tasks 109b/109c). */
export function CityPricingPolicySection({ canWrite }: { canWrite: boolean }) {
  const queryClient = useQueryClient();

  const citiesQuery = useQuery({ queryKey: ["cities"], queryFn: () => listCities() });
  const policiesQuery = useQuery({ queryKey: ["pricing", "policies"], queryFn: listCityPricingPolicies });
  const [newCityId, setNewCityId] = useState("");
  const [editing, setEditing] = useState<CityPricingPolicyResponse | null>(null);

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
      setEditing(null);
    },
  });

  const configuredCityIds = new Set((policiesQuery.data ?? []).map((policy) => policy.cityId));
  const unconfiguredCityOptions = (citiesQuery.data ?? [])
    .filter((city) => !configuredCityIds.has(city.id))
    .map((city) => ({ value: city.id, label: city.name }));

  const columns: DataTableColumn<CityPricingPolicyResponse>[] = [
    {
      key: "city",
      header: "City",
      sortValue: (policy) => policy.cityName,
      cell: (policy) => <span className="font-medium text-fg">{policy.cityName}</span>,
    },
    {
      key: "visit",
      header: "Visit charge",
      numeric: true,
      sortValue: (policy) => policy.visitCharge,
      cell: (policy) => formatCurrency(policy.visitCharge),
    },
    {
      key: "tax",
      header: "Tax %",
      numeric: true,
      sortValue: (policy) => policy.taxPercentage,
      cell: (policy) => `${policy.taxPercentage.toFixed(2)}%`,
    },
    {
      key: "platform",
      header: "Platform fee",
      numeric: true,
      sortValue: (policy) => policy.platformFee,
      cell: (policy) => formatCurrency(policy.platformFee),
    },
  ];

  return (
    <div className="flex flex-col gap-3">
      {upsertMutation.isError && !editing ? <Alert>{describeError(upsertMutation.error)}</Alert> : null}

      <DataTable
        title="Tax & fees"
        description="Per-city tax rate, visit charge and platform/convenience fee (SRS 12.8.1)."
        actions={
          canWrite && unconfiguredCityOptions.length > 0 ? (
            <div className="flex items-end gap-2">
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
                disabled={!newCityId}
                loading={upsertMutation.isPending && upsertMutation.variables?.cityId === newCityId}
                onClick={() => upsertMutation.mutate({ cityId: newCityId, visitCharge: 0, taxPercentage: 0, platformFee: 0 })}
              >
                Add
              </Button>
            </div>
          ) : undefined
        }
        columns={columns}
        rows={policiesQuery.data}
        rowKey={(policy) => policy.id}
        isLoading={policiesQuery.isPending}
        isFetching={policiesQuery.isFetching}
        error={policiesQuery.error}
        onRetry={() => policiesQuery.refetch()}
        caption="Per-city tax and fee policies"
        emptyTitle="No city pricing policies configured yet"
        emptyDescription="A city with no policy charges no tax, no visit charge and no platform fee."
        hideDensityToggle
        skeletonRows={4}
        minWidth="720px"
        maxHeight="420px"
        rowActions={
          canWrite
            ? (policy) => (
                <Button size="sm" variant="ghost" onClick={() => setEditing(policy)}>
                  Edit
                </Button>
              )
            : undefined
        }
      />

      {editing ? (
        <CityPricingPolicyEditor
          key={editing.id}
          policy={editing}
          saving={upsertMutation.isPending}
          error={upsertMutation.isError ? upsertMutation.error : null}
          onClose={() => setEditing(null)}
          onSave={(visitCharge, taxPercentage, platformFee) =>
            upsertMutation.mutate({ cityId: editing.cityId, visitCharge, taxPercentage, platformFee })
          }
        />
      ) : null}
    </div>
  );
}
