"use client";

import { useMutation } from "@tanstack/react-query";
import { useEffect, useMemo, useState } from "react";
import { Alert, Card, Field } from "@/components/ui";
import { describeError } from "@/lib/api";
import type { NotificationTemplatePreviewResponse } from "@/lib/notification-template-types";

const PLACEHOLDER_PATTERN = /\{\{(\w+)\}\}/g;

/** Extracts the distinct `{{Variable}}` names referenced across the given text(s), in first-seen order. */
function extractVariableNames(...texts: Array<string | null | undefined>): string[] {
  const seen = new Set<string>();
  for (const text of texts) {
    if (!text) continue;
    for (const match of Array.from(text.matchAll(PLACEHOLDER_PATTERN))) {
      seen.add(match[1]);
    }
  }
  return Array.from(seen);
}

/**
 * Live "preview/test" panel shared by the template editor's draft preview and
 * the templates table's saved-template preview (SRS 12.17.2, task 126b). The
 * two callers differ only in which endpoint renders the text - ad-hoc for an
 * unsaved draft, saved-id for a persisted row - so that's the one thing left
 * as a prop; everything else (placeholder discovery, sample-value inputs,
 * debounced re-render) is identical between them and lives here once.
 */
export function NotificationTemplatePreviewPanel({
  channel,
  subject,
  body,
  render,
}: {
  /** Included in the effect's dependency list so switching channel (which flips subject-required-ness) re-triggers a render. */
  channel: number;
  subject: string | null;
  body: string;
  render: (sampleVariables: Record<string, string>) => Promise<NotificationTemplatePreviewResponse>;
}) {
  const variableNames = useMemo(() => extractVariableNames(subject, body), [subject, body]);
  const [sampleValues, setSampleValues] = useState<Record<string, string>>({});

  // Seed a friendly default for every placeholder currently referenced, and
  // drop any that no longer appear - without clobbering an admin's own edits
  // to the ones that remain.
  useEffect(() => {
    setSampleValues((previous) => {
      const next: Record<string, string> = {};
      for (const name of variableNames) {
        next[name] = previous[name] ?? `Sample ${name}`;
      }
      return next;
    });
  }, [variableNames]);

  const previewMutation = useMutation({
    mutationFn: (sampleVariables: Record<string, string>) => render(sampleVariables),
  });

  // Debounced (300ms) so a fast typist doesn't fire a request per keystroke;
  // an empty body has nothing worth rendering (and often fails Update's
  // required-body validation before the admin has finished typing).
  useEffect(() => {
    if (body.trim() === "") return;
    const timeout = setTimeout(() => previewMutation.mutate(sampleValues), 300);
    return () => clearTimeout(timeout);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [channel, subject, body, sampleValues]);

  return (
    <Card title="Preview" description="Renders against sample values - nothing is sent or persisted (SRS 12.17.2).">
      <div className="flex flex-col gap-4">
        {variableNames.length > 0 ? (
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            {variableNames.map((name) => (
              <Field
                key={name}
                label={`{{${name}}}`}
                value={sampleValues[name] ?? ""}
                onChange={(event) =>
                  setSampleValues((previous) => ({ ...previous, [name]: event.target.value }))
                }
              />
            ))}
          </div>
        ) : (
          <p className="text-sm text-neutral-600 dark:text-neutral-400">
            No <code>{"{{Variable}}"}</code> placeholders in the current text.
          </p>
        )}

        {previewMutation.isError ? <Alert>{describeError(previewMutation.error)}</Alert> : null}

        {previewMutation.data ? (
          <div className="flex flex-col gap-3 rounded-lg border border-black/10 bg-neutral-50 p-4 text-sm dark:border-white/15 dark:bg-neutral-950">
            {previewMutation.data.subject ? (
              <div>
                <p className="text-xs font-medium uppercase tracking-wide text-neutral-500 dark:text-neutral-400">
                  Subject
                </p>
                <p className="font-medium">{previewMutation.data.subject}</p>
              </div>
            ) : null}
            <div>
              <p className="text-xs font-medium uppercase tracking-wide text-neutral-500 dark:text-neutral-400">
                Body
              </p>
              <p className="whitespace-pre-wrap">{previewMutation.data.body}</p>
            </div>
          </div>
        ) : body.trim() === "" ? (
          <p className="text-sm text-neutral-600 dark:text-neutral-400">Nothing to preview yet.</p>
        ) : null}
      </div>
    </Card>
  );
}
