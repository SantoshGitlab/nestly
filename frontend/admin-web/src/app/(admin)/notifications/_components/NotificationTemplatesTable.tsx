"use client";

import { Fragment, useState } from "react";
import { Button } from "@/components/ui";
import { previewNotificationTemplate } from "@/lib/notification-template-api";
import {
  NOTIFICATION_CHANNEL_LABELS,
  NOTIFICATION_EVENT_TYPE_LABELS,
  type NotificationTemplateResponse,
} from "@/lib/notification-template-types";
import { NotificationTemplatePreviewPanel } from "./NotificationTemplatePreviewPanel";

function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString();
}

/**
 * Lists every notification template row (SRS 12.17.1-2, tasks 126a-d):
 * event/channel/key, subject, active status, and who last touched it. Each
 * row's "Preview" toggle expands the shared preview panel bound to that
 * saved template's own subject/body via the `{id}/preview` endpoint - the
 * template editor's own live-draft preview is a separate instance of the
 * same panel, wired in `page.tsx`.
 */
export function NotificationTemplatesTable({
  templates,
  isLoading,
  errorMessage,
  canWrite,
  onEdit,
  onToggleActive,
  togglingId,
}: {
  templates: NotificationTemplateResponse[] | undefined;
  isLoading: boolean;
  errorMessage: string | null;
  canWrite: boolean;
  onEdit: (template: NotificationTemplateResponse) => void;
  onToggleActive: (template: NotificationTemplateResponse) => void;
  togglingId?: string;
}) {
  const [previewingId, setPreviewingId] = useState<string | null>(null);

  if (isLoading) {
    return <p className="text-sm text-neutral-600 dark:text-neutral-400">Loading…</p>;
  }

  if (errorMessage) {
    return <p className="text-sm text-red-600 dark:text-red-400">{errorMessage}</p>;
  }

  if (!templates || templates.length === 0) {
    return <p className="text-sm text-neutral-600 dark:text-neutral-400">No templates match the current filters.</p>;
  }

  return (
    <div className="overflow-x-auto">
      <table className="w-full min-w-full text-left text-sm">
        <thead>
          <tr className="border-b border-black/10 text-xs uppercase tracking-wide text-neutral-500 dark:border-white/15 dark:text-neutral-400">
            <th scope="col" className="px-3 py-2 font-medium">Event</th>
            <th scope="col" className="px-3 py-2 font-medium">Channel</th>
            <th scope="col" className="px-3 py-2 font-medium">Template key</th>
            <th scope="col" className="px-3 py-2 font-medium">Subject</th>
            <th scope="col" className="px-3 py-2 font-medium">Status</th>
            <th scope="col" className="px-3 py-2 font-medium">Last updated</th>
            <th scope="col" className="px-3 py-2 font-medium">
              <span className="sr-only">Actions</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {templates.map((template) => (
            <Fragment key={template.id}>
              <tr className="border-b border-black/5 last:border-0 dark:border-white/10">
                <td className="px-3 py-2">{NOTIFICATION_EVENT_TYPE_LABELS[template.eventType]}</td>
                <td className="px-3 py-2">{NOTIFICATION_CHANNEL_LABELS[template.channel]}</td>
                <td className="px-3 py-2 font-medium">{template.templateKey}</td>
                <td className="px-3 py-2">{template.subject ?? <span className="text-neutral-400">-</span>}</td>
                <td className="px-3 py-2">
                  <span
                    className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${
                      template.isActive
                        ? "bg-green-100 text-green-800 dark:bg-green-950 dark:text-green-200"
                        : "bg-neutral-200 text-neutral-700 dark:bg-neutral-800 dark:text-neutral-300"
                    }`}
                  >
                    {template.isActive ? "Active" : "Inactive"}
                  </span>
                </td>
                <td className="px-3 py-2">{formatDateTime(template.updatedAtUtc)}</td>
                <td className="px-3 py-2">
                  <div className="flex flex-wrap gap-2">
                    <Button
                      type="button"
                      variant="secondary"
                      className="px-2 py-1 text-xs"
                      onClick={() => setPreviewingId((current) => (current === template.id ? null : template.id))}
                    >
                      {previewingId === template.id ? "Hide preview" : "Preview"}
                    </Button>
                    {canWrite ? (
                      <>
                        <Button type="button" variant="secondary" className="px-2 py-1 text-xs" onClick={() => onEdit(template)}>
                          Edit
                        </Button>
                        <Button
                          type="button"
                          variant={template.isActive ? "danger" : "secondary"}
                          className="px-2 py-1 text-xs"
                          disabled={togglingId === template.id}
                          onClick={() => onToggleActive(template)}
                        >
                          {togglingId === template.id ? "Saving…" : template.isActive ? "Deactivate" : "Activate"}
                        </Button>
                      </>
                    ) : null}
                  </div>
                </td>
              </tr>
              {previewingId === template.id ? (
                <tr className="border-b border-black/5 last:border-0 dark:border-white/10">
                  <td colSpan={7} className="bg-neutral-50 px-3 py-4 dark:bg-neutral-950/40">
                    <NotificationTemplatePreviewPanel
                      channel={template.channel}
                      subject={template.subject}
                      body={template.body}
                      render={(sampleVariables) => previewNotificationTemplate(template.id, { sampleVariables })}
                    />
                  </td>
                </tr>
              ) : null}
            </Fragment>
          ))}
        </tbody>
      </table>
    </div>
  );
}
