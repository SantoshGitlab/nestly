"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { Button, Card, PageHeading, Select } from "@/components/ui";
import { describeError } from "@/lib/api";
import { getSessionClaims, subscribeToAuthChanges } from "@/lib/auth";
import {
  createNotificationTemplate,
  listNotificationTemplates,
  previewNotificationTemplateAdHoc,
  setNotificationTemplateActive,
  updateNotificationTemplate,
} from "@/lib/notification-template-api";
import {
  NOTIFICATION_CHANNEL_LABELS,
  NOTIFICATION_EVENT_TYPE_LABELS,
  NotificationChannel,
  NotificationEventType,
  type NotificationTemplateCreateRequest,
  type NotificationTemplateResponse,
  type NotificationTemplateUpdateRequest,
} from "@/lib/notification-template-types";
import { canWriteModule } from "@/lib/permissions";
import type { AdminSessionClaims } from "@/lib/types";
import { NotificationTemplateForm, type NotificationTemplateFormValues } from "./_components/NotificationTemplateForm";
import { NotificationTemplatePreviewPanel } from "./_components/NotificationTemplatePreviewPanel";
import { NotificationTemplatesTable } from "./_components/NotificationTemplatesTable";

const CHANNEL_FILTER_OPTIONS = [
  { value: "", label: "All channels" },
  ...Object.entries(NOTIFICATION_CHANNEL_LABELS).map(([value, label]) => ({ value, label })),
] as const;

const EVENT_TYPE_FILTER_OPTIONS = [
  { value: "", label: "All events" },
  ...Object.entries(NOTIFICATION_EVENT_TYPE_LABELS).map(([value, label]) => ({ value, label })),
] as const;

const STATUS_FILTER_OPTIONS = [
  { value: "", label: "All statuses" },
  { value: "true", label: "Active" },
  { value: "false", label: "Inactive" },
] as const;

/**
 * Notification template management (SRS 12.17, tasks 126a-d, 127): channel-
 * specific templates with `{{Variable}}` placeholders, preview/test, and
 * change history via the existing audit trail (visible on the Audit Log
 * screen, keyed by entity type "NotificationTemplate"). Gated behind the
 * "notifications" permission module the same way every other admin screen
 * gates itself - see CouponsPage's doc comment for the pattern this mirrors.
 *
 * The create/edit form and its live preview panel are laid out side by side
 * while a template is being authored, so an admin sees the rendered result
 * update as they type (task 127's "editor and preview screens") without
 * having to save first; `NotificationTemplateForm`'s `onValuesChange` is
 * what feeds the panel that draft.
 */
export default function NotificationTemplatesPage() {
  const [claims, setClaims] = useState<AdminSessionClaims | null>(null);
  const [channelFilter, setChannelFilter] = useState("");
  const [eventTypeFilter, setEventTypeFilter] = useState("");
  const [statusFilter, setStatusFilter] = useState("");
  const [editingTemplate, setEditingTemplate] = useState<NotificationTemplateResponse | null>(null);
  const [isCreating, setIsCreating] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const [draftValues, setDraftValues] = useState<NotificationTemplateFormValues | null>(null);

  useEffect(() => {
    const sync = () => setClaims(getSessionClaims());
    sync();
    return subscribeToAuthChanges(sync);
  }, []);

  const canWrite = canWriteModule(claims, "notifications");
  const queryClient = useQueryClient();

  const channel = channelFilter === "" ? undefined : (Number(channelFilter) as NotificationChannel);
  const eventType = eventTypeFilter === "" ? undefined : (Number(eventTypeFilter) as NotificationEventType);
  const isActive = statusFilter === "" ? undefined : statusFilter === "true";

  const templatesQuery = useQuery({
    queryKey: ["notification-templates", "list", channelFilter, eventTypeFilter, statusFilter] as const,
    queryFn: () => listNotificationTemplates({ channel, eventType, isActive }),
  });

  const invalidateTemplates = () => queryClient.invalidateQueries({ queryKey: ["notification-templates", "list"] });

  const closeEditor = () => {
    setEditingTemplate(null);
    setIsCreating(false);
    setFormError(null);
    setDraftValues(null);
  };

  const createMutation = useMutation({
    mutationFn: (request: NotificationTemplateCreateRequest) => createNotificationTemplate(request),
    onSuccess: () => {
      invalidateTemplates();
      closeEditor();
    },
    onError: (error) => setFormError(describeError(error)),
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, request }: { id: string; request: NotificationTemplateUpdateRequest }) =>
      updateNotificationTemplate(id, request),
    onSuccess: () => {
      invalidateTemplates();
      closeEditor();
    },
    onError: (error) => setFormError(describeError(error)),
  });

  const toggleMutation = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => setNotificationTemplateActive(id, isActive),
    onSuccess: invalidateTemplates,
  });

  const handleSubmit = (request: NotificationTemplateCreateRequest | NotificationTemplateUpdateRequest) => {
    if (editingTemplate) {
      updateMutation.mutate({ id: editingTemplate.id, request: request as NotificationTemplateUpdateRequest });
    } else {
      createMutation.mutate(request as NotificationTemplateCreateRequest);
    }
  };

  const isEditorOpen = canWrite && (isCreating || editingTemplate !== null);

  return (
    <div className="mx-auto w-full max-w-6xl">
      <PageHeading
        title="Notification Templates"
        subtitle="Channel-specific templates with variable placeholders, preview/test rendering, and full change history (SRS 12.17)."
      />

      <div className="flex flex-col gap-6">
        {canWrite ? (
          <div className="flex justify-end">
            {isEditorOpen ? null : (
              <Button
                type="button"
                onClick={() => {
                  setIsCreating(true);
                  setFormError(null);
                }}
              >
                New template
              </Button>
            )}
          </div>
        ) : null}

        {isEditorOpen ? (
          <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
            <Card
              title={editingTemplate ? `Edit template: ${editingTemplate.templateKey}` : "Create a template"}
              description={
                editingTemplate
                  ? "The trigger event, channel and template key cannot be changed once created."
                  : "Pick an (event, channel) combination not already covered - each pair may have at most one template."
              }
            >
              <NotificationTemplateForm
                template={editingTemplate}
                isSubmitting={createMutation.isPending || updateMutation.isPending}
                submitError={formError}
                onSubmit={handleSubmit}
                onCancel={closeEditor}
                onValuesChange={setDraftValues}
              />
            </Card>

            {draftValues ? (
              <NotificationTemplatePreviewPanel
                channel={draftValues.channel}
                subject={draftValues.subject.trim() === "" ? null : draftValues.subject}
                body={draftValues.body}
                render={(sampleVariables) =>
                  previewNotificationTemplateAdHoc({
                    channel: draftValues.channel as NotificationChannel,
                    subject: draftValues.subject.trim() === "" ? null : draftValues.subject,
                    body: draftValues.body,
                    sampleVariables,
                  })
                }
              />
            ) : null}
          </div>
        ) : null}

        <Card title="Templates" description="Every (event, channel) template currently defined (SRS 12.17.1).">
          <div className="mb-4 flex flex-wrap items-end gap-3">
            <div className="w-48">
              <Select
                label="Channel"
                options={CHANNEL_FILTER_OPTIONS}
                value={channelFilter}
                onChange={(event) => setChannelFilter(event.target.value)}
              />
            </div>
            <div className="w-56">
              <Select
                label="Trigger event"
                options={EVENT_TYPE_FILTER_OPTIONS}
                value={eventTypeFilter}
                onChange={(event) => setEventTypeFilter(event.target.value)}
              />
            </div>
            <div className="w-40">
              <Select
                label="Status"
                options={STATUS_FILTER_OPTIONS}
                value={statusFilter}
                onChange={(event) => setStatusFilter(event.target.value)}
              />
            </div>
          </div>

          <NotificationTemplatesTable
            templates={templatesQuery.data}
            isLoading={templatesQuery.isLoading}
            errorMessage={templatesQuery.error ? describeError(templatesQuery.error) : null}
            canWrite={canWrite}
            onEdit={(template) => {
              setIsCreating(false);
              setEditingTemplate(template);
              setFormError(null);
            }}
            onToggleActive={(template) => toggleMutation.mutate({ id: template.id, isActive: !template.isActive })}
            togglingId={toggleMutation.isPending ? toggleMutation.variables?.id : undefined}
          />
        </Card>
      </div>
    </div>
  );
}
