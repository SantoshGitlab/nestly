"use client";

import { useState } from "react";
import { Button } from "@/components/ui";

/**
 * Inline "click to edit" price cell shared by the base and add-on pricing
 * tables (SRS 12.8.1) - both are "one decimal value, edited in place, saved
 * independently per row," so this is the one place that shape is rendered
 * rather than two near-identical copies.
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
    return <span className="tabular-nums">₹{value.toFixed(2)}</span>;
  }

  if (!isEditing) {
    return (
      <div className="flex items-center gap-2">
        <span className="tabular-nums">₹{value.toFixed(2)}</span>
        <Button
          type="button"
          variant="secondary"
          className="px-2 py-1 text-xs"
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
    <div className="flex items-center gap-2">
      <input
        type="number"
        step="0.01"
        min="0.01"
        value={draft}
        onChange={(event) => setDraft(event.target.value)}
        aria-label="Price"
        className="w-24 rounded-lg border border-black/15 bg-transparent px-2 py-1 text-sm outline-none focus:border-black focus:ring-1 focus:ring-black dark:border-white/20 dark:focus:border-white dark:focus:ring-white"
      />
      <Button
        type="button"
        className="px-2 py-1 text-xs"
        disabled={!isValid || isSaving}
        onClick={() => {
          onSave(parsed);
          setIsEditing(false);
        }}
      >
        {isSaving ? "Saving…" : "Save"}
      </Button>
      <Button type="button" variant="secondary" className="px-2 py-1 text-xs" onClick={() => setIsEditing(false)}>
        Cancel
      </Button>
    </div>
  );
}
