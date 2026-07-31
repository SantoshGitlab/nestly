"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { Alert, Button, Card, Field } from "@/components/ui";
import { describeError } from "@/lib/api";
import { getServiceAreas, updateServiceAreas } from "@/lib/profile-api";
import type { ServiceAreaInput } from "@/lib/profile-types";

const EMPTY_ROW: ServiceAreaInput = { cityId: "", zoneId: "", pincodeId: "" };

/**
 * Service areas editor (docs/PARTNER.md's Capability & Coverage domain,
 * `partner_service_area`): the cities/zones/pincodes a partner is willing to
 * work in. The API is a full-replace PUT, so this section edits a local
 * draft list and only sends it on "Save changes".
 *
 * City/zone/pincode are identified by id here rather than through a
 * lookup/typeahead picker - the task this screen was built against did not
 * include a serviceability lookup endpoint on partner-api, so ids are
 * entered directly. Swapping these plain inputs for a proper picker once
 * such a lookup exists only touches this file.
 */
export function ServiceAreasSection() {
  const queryClient = useQueryClient();
  const [rows, setRows] = useState<ServiceAreaInput[]>([]);
  const [isDirty, setIsDirty] = useState(false);

  const query = useQuery({ queryKey: ["partner-service-areas"], queryFn: getServiceAreas });

  useEffect(() => {
    if (query.data && !isDirty) {
      setRows(
        query.data.map((area) => ({
          cityId: area.cityId,
          zoneId: area.zoneId ?? "",
          pincodeId: area.pincodeId ?? "",
        })),
      );
    }
  }, [query.data, isDirty]);

  const mutation = useMutation({
    mutationFn: (areas: ServiceAreaInput[]) => updateServiceAreas({ areas }),
    onSuccess: (areas) => {
      queryClient.setQueryData(["partner-service-areas"], areas);
      setIsDirty(false);
    },
  });

  function updateRow(index: number, patch: Partial<ServiceAreaInput>) {
    setIsDirty(true);
    setRows((current) => current.map((row, i) => (i === index ? { ...row, ...patch } : row)));
  }

  function removeRow(index: number) {
    setIsDirty(true);
    setRows((current) => current.filter((_, i) => i !== index));
  }

  function addRow() {
    setIsDirty(true);
    setRows((current) => [...current, { ...EMPTY_ROW }]);
  }

  function save() {
    const areas = rows
      .filter((row) => row.cityId.trim() !== "")
      .map((row) => ({
        cityId: row.cityId.trim(),
        zoneId: row.zoneId?.trim() || undefined,
        pincodeId: row.pincodeId?.trim() || undefined,
      }));
    mutation.mutate(areas);
  }

  if (query.isPending) {
    return (
      <Card title="Service areas">
        <p className="text-sm text-neutral-500">Loading service areas…</p>
      </Card>
    );
  }

  if (query.isError) {
    return (
      <Card title="Service areas">
        <Alert>{describeError(query.error)}</Alert>
      </Card>
    );
  }

  return (
    <Card title="Service areas" description="Cities, zones and pincodes you're willing to work in.">
      {mutation.isError ? (
        <div className="mb-3">
          <Alert>{describeError(mutation.error)}</Alert>
        </div>
      ) : null}

      {rows.length === 0 ? (
        <p className="mb-3 text-sm text-neutral-600 dark:text-neutral-400">No service areas added yet.</p>
      ) : (
        <div className="flex flex-col gap-3">
          {rows.map((row, index) => (
            <div key={index} className="flex flex-wrap items-end gap-3">
              <div className="w-48">
                <Field
                  label="City ID"
                  value={row.cityId}
                  onChange={(e) => updateRow(index, { cityId: e.target.value })}
                />
              </div>
              <div className="w-48">
                <Field
                  label="Zone ID (optional)"
                  value={row.zoneId ?? ""}
                  onChange={(e) => updateRow(index, { zoneId: e.target.value })}
                />
              </div>
              <div className="w-48">
                <Field
                  label="Pincode ID (optional)"
                  value={row.pincodeId ?? ""}
                  onChange={(e) => updateRow(index, { pincodeId: e.target.value })}
                />
              </div>
              <Button type="button" variant="danger" onClick={() => removeRow(index)}>
                Remove
              </Button>
            </div>
          ))}
        </div>
      )}

      <div className="mt-4 flex gap-2">
        <Button type="button" variant="secondary" onClick={addRow}>
          Add area
        </Button>
        <Button type="button" disabled={mutation.isPending || !isDirty} onClick={save}>
          {mutation.isPending ? "Saving…" : "Save changes"}
        </Button>
      </div>
    </Card>
  );
}
