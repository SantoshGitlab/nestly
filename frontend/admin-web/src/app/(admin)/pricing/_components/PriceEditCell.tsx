"use client";

import { useState } from "react";
import { Button } from "@/components/ui";
import { formatCurrency } from "@/components/data-table";

/**
 * Inline "click to edit" price cell shared by the base and add-on pricing
 * tables (SRS 12.8.1) - both are "one decimal value, edited in place, saved
 * independently per row," so this is the one place that shape is rendered
 * rather than two near-identical copies.
 *
 * The input is a bare `<input>` rather than the kit's `Field` on purpose: a
 * table cell has no room for a stacked label, so the label is carried by
 * `aria-label` and the control borrows `Field`'s focus treatment through the
 * same tokens.
 */
export function PriceEditCell({
  value,
  canWrite,
  isSaving,
  onSave,
}: {
  value: number;
  canWrite: boolean;
  isSaving: boolean;
  onSave: (newValue: number) => void;
}) {
  const [isEditing, setIsEditing] = useState(false);
  const [draft, setDraft] = useState(String(value));

  if (!canWrite) {
    return <span className="nums">{formatCurrency(value)}</span>;
  }

  if (!isEditing) {
    return (
      <div className="flex items-center justify-end gap-2">
        <span className="nums text-fg">{formatCurrency(value)}</span>
        <Button
          type="button"
          size="sm"
          variant="ghost"
          onClick={() => {
            setDraft(String(value));
            setIsEditing(true);
          }}
        >
          Edit
        </Button>
      </div>
    );
  }

  const parsed = Number(draft);
  const isValid = draft.trim() !== "" && !Number.isNaN(parsed) && parsed > 0;

  return (
    <div className="flex items-center justify-end gap-2">
      <input
        type="number"
        step="0.01"
        min="0.01"
        value={draft}
        onChange={(event) => setDraft(event.target.value)}
        aria-label="Price"
        autoFocus
        className="nums w-24 rounded-lg border border-line bg-surface px-2 py-1 text-sm text-fg shadow-xs outline-none transition duration-fast ease-out hover:border-line-strong focus:border-brand-600 focus:ring-2 focus:ring-brand-600/25"
      />
      <Button
        type="button"
        size="sm"
        disabled={!isValid}
        loading={isSaving}
        onClick={() => {
          onSave(parsed);
          setIsEditing(false);
        }}
      >
        Save
      </Button>
      <Button type="button" size="sm" variant="ghost" onClick={() => setIsEditing(false)}>
        Cancel
      </Button>
    </div>
  );
}
