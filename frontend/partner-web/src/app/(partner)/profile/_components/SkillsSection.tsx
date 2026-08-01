"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { Alert, Button, Card, Field } from "@/components/ui";
import { describeError } from "@/lib/api";
import { getSkills, updateSkills } from "@/lib/profile-api";
import type { PartnerSkillInput } from "@/lib/profile-types";

const EMPTY_ROW: PartnerSkillInput = { categoryId: "", serviceId: "" };

/**
 * Skills editor (docs/PARTNER.md's Capability & Coverage domain,
 * `partner_skill_mapping`): the categories/services a partner is qualified
 * to fulfil. Same full-replace PUT shape and same "id typed by hand, no
 * catalog lookup endpoint in this contract" caveat as ServiceAreasSection.
 */
export function SkillsSection() {
  const queryClient = useQueryClient();
  const [rows, setRows] = useState<PartnerSkillInput[]>([]);
  const [isDirty, setIsDirty] = useState(false);

  const query = useQuery({ queryKey: ["partner-skills"], queryFn: getSkills });

  useEffect(() => {
    if (query.data && !isDirty) {
      setRows(
        query.data.map((skill) => ({
          categoryId: skill.categoryId,
          serviceId: skill.serviceId ?? "",
        })),
      );
    }
  }, [query.data, isDirty]);

  const mutation = useMutation({
    mutationFn: (skills: PartnerSkillInput[]) => updateSkills({ skills }),
    onSuccess: (skills) => {
      queryClient.setQueryData(["partner-skills"], skills);
      setIsDirty(false);
    },
  });

  function updateRow(index: number, patch: Partial<PartnerSkillInput>) {
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
    const skills = rows
      .filter((row) => row.categoryId.trim() !== "")
      .map((row) => ({
        categoryId: row.categoryId.trim(),
        serviceId: row.serviceId?.trim() || undefined,
      }));
    mutation.mutate(skills);
  }

  if (query.isPending) {
    return (
      <Card title="Skills">
        <p className="text-sm text-neutral-500">Loading skills…</p>
      </Card>
    );
  }

  if (query.isError) {
    return (
      <Card title="Skills">
        <Alert>{describeError(query.error)}</Alert>
      </Card>
    );
  }

  return (
    <Card title="Skills" description="Service categories and services you're qualified to fulfil.">
      {mutation.isError ? (
        <div className="mb-3">
          <Alert>{describeError(mutation.error)}</Alert>
        </div>
      ) : null}

      {rows.length === 0 ? (
        <p className="mb-3 text-sm text-neutral-600 dark:text-neutral-400">No skills added yet.</p>
      ) : (
        <div className="flex flex-col gap-3">
          {rows.map((row, index) => (
            <div key={index} className="flex flex-wrap items-end gap-3">
              <div className="w-56">
                <Field
                  label="Category ID"
                  value={row.categoryId}
                  onChange={(e) => updateRow(index, { categoryId: e.target.value })}
                />
              </div>
              <div className="w-56">
                <Field
                  label="Service ID (optional)"
                  value={row.serviceId ?? ""}
                  onChange={(e) => updateRow(index, { serviceId: e.target.value })}
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
          Add skill
        </Button>
        <Button type="button" disabled={mutation.isPending || !isDirty} onClick={save}>
          {mutation.isPending ? "Saving…" : "Save changes"}
        </Button>
      </div>
    </Card>
  );
}
