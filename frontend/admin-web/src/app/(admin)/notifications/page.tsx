"use client";

import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { FilterBar, countActiveFilters } from "@/components/data-table";
import { Button, Card, PageHeading, Select, useToast } from "@/components/ui";
import { describeError } from "@/lib/api";
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
import { useAdminClaims } from "@/lib/use-admin-claims";
import {
  NotificationTemplateForm,
  type NotificationTemplateFormValues,
} from "./_components/NotificationTemplateForm";
import { NotificationTemplatePreviewPanel } from "./_components/NotificationTemplatePreviewPanel";
import { NotificationTemplatesTable } from "./_components/NotificationTemplatesTable";

const CHANNEL_FILTER_OPTIONS = [
  { value: "", label: "All channels" },
  ...Object.entries(NOTIFICATION_CHANNEL_LABELS).map(([value, label]) => ({ value, label })),
];

const EVENT_TYPE_FILTER_OPTIONS = [
  { value: "", label: "All events" },
  ...Object.entries(NOTIFICATION_EVENT_TYPE_LABELS).map(([value, label]) => ({ value, label })),
];

const STATUS_FILTER_OPTIONS = [
  { value: "", label: "All statuses" },
  { value: "true", label: "Active" },
  { value: "false", label: "Inactive" },
];

interface TemplateFilters {
  channel: string;
  eventType: string;
  status: string;
}

const EMPTY_FILTERS: TemplateFilters = { channel: "", eventType: "", status: "" };

/**
 * Notification template management (SRS 12.17, tasks 126a-d, 127): channel-
 * specific templates with `{{Variable}}` placeholders, preview/test, and
 * change history via the existing audit trail (visible on the Audit Log
 * screen, keyed by entity type "NotificationTemplate"). Gated behind the
 * "notifications" permission module the same way every other admin screen
 * gates itself - see CouponsPage's doc comment for the pattern this mirrors.
 *
 * The create/edit form and its live preview panel sit side by side while a
 * template is being authored, so an admin sees the rendered result update as
 * they type (task 127's "editor and preview screens") without having to save
 * first; `NotificationTemplateForm`'s `onValuesChange` feeds the panel that
 * draft. That pairing is why the editor is not a `Modal` like the other admin
 * create forms: a modal would push the preview below the fold, which is the
 * one thing this screen exists to avoid.
 */
export default function NotificationTemplatesPage() {
  const claims = useAdminClaims();
  const canWrite = canWriteModule(claims, "notifications");
  const queryClient = useQueryClient();
  const pushToast = useToast();

  const [filters, setFilters] = useState<TemplateFilters>(EMPTY_FILTERS);
  const [appliedFilters, setAppliedFilters] = useState<TemplateFilters>(EMPTY_FILTERS);
  const [editingTemplate, setEditingTemplate] = useState<NotificationTemplateResponse | null>(null);
  const [isCreating, setIsCreating] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const [draftValues, setDraftValues] = useState<NotificationTemplateFormValues | null>(null);

  const templatesQuery = useQuery({
    queryKey: ["notification-templates", "list", appliedFilters] as const,
    queryFn: () =>
      listNotificationTemplates({
        channel:
          appliedFilters.channel === "" ? undefined : (Number(appliedFilters.channel) as NotificationChannel),
        eventType:
          appliedFilters.eventType === ""
            ? undefined
            : (Number(appliedFilters.eventType) as NotificationEventType),
        isActive: appliedFilters.status === "" ? undefined : appliedFilters.status === "true",
      }),
    // Applying a filter dims the current rows rather than replacing the whole
    // table with a skeleton.
    placeholderData: keepPreviousData,
  });

  const invalidateTemplates = () =>
    queryClient.invalidateQueries({ queryKey: ["notification-templates", "list"] });

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
      pushToast("success", "Template created.");
    },
    // The form keeps everything that was typed — only the error banner changes.
    onError: (error) => setFormError(describeError(error)),
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, request }: { id: string; request: NotificationTemplateUpdateRequest }) =>
      updateNotificationTemplate(id, request),
    onSuccess: () => {
      invalidateTemplates();
      closeEditor();
      pushToast("success", "Template saved.");
    },
    onError: (error) => setFormError(describeError(error)),
  });

  const toggleMutation = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) =>
      setNotificationTemplateActive(id, isActive),
    onSuccess: invalidateTemplates,
  });

  const handleSubmit = (request: NotificationTemplateCreateRequest | NotificationTemplateUpdateRequest) => {
    setFormError(null);
    if (editingTemplate) {
      updateMutation.mutate({ id: editingTemplate.id, request: request as NotificationTemplateUpdateRequest });
    } else {
      createMutation.mutate(request as NotificationTemplateCreateRequest);
    }
  };

  const applyFilters = () => setAppliedFilters(filters);

  const clearFilters = () => {
    setFilters(EMPTY_FILTERS);
    setAppliedFilters(EMPTY_FILTERS);
  };

  const isEditorOpen = canWrite && (isCreating || editingTemplate !== null);
  const activeFilterCount = countActiveFilters(appliedFilters);

  return (
    <div className="w-full max-w-7xl">
      <PageHeading
        title="Notification templates"
        subtitle="Channel-specific templates with variable placeholders, preview/test rendering, and full change history (SRS 12.17)."
        actions={
          canWrite && !isEditorOpen ? (
            <Button
              type="button"
              onClick={() => {
                setIsCreating(true);
                setFormError(null);
              }}
            >
              New template
            </Button>
          ) : undefined
        }
      />

      <div className="flex flex-col gap-6">
        {isEditorOpen ? (
          <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
            <Card
              title={editingTemplate ? `Edit template: ${editingTemplate.templateKey}` : "Create a template"}
              description={
                editingTemplate
                  ? "The trigger event, channel and template key cannot be changed once created."
                  : "Pick an (event, channel) combination not already covered — each pair may have at most one template."
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

        <FilterBar
          columns={3}
          onSubmit={applyFilters}
          onClear={clearFilters}
          activeCount={activeFilterCount}
          busy={templatesQuery.isFetching}
        >
          <Select
            label="Channel"
            options={CHANNEL_FILTER_OPTIONS}
            value={filters.channel}
            onChange={(event) => setFilters((current) => ({ ...current, channel: event.target.value }))}
          />
          <Select
            label="Trigger event"
            options={EVENT_TYPE_FILTER_OPTIONS}
            value={filters.eventType}
            onChange={(event) => setFilters((current) => ({ ...current, eventType: event.target.value }))}
          />
          <Select
            label="Status"
            options={STATUS_FILTER_OPTIONS}
            value={filters.status}
            onChange={(event) => setFilters((current) => ({ ...current, status: event.target.value }))}
          />
        </FilterBar>

        <NotificationTemplatesTable
          templates={templatesQuery.data}
          isLoading={templatesQuery.isPending}
          isFetching={templatesQuery.isFetching}
          error={templatesQuery.error}
          onRetry={() => templatesQuery.refetch()}
          canWrite={canWrite}
          onEdit={(template) => {
            setIsCreating(false);
            setEditingTemplate(template);
            setFormError(null);
          }}
          onToggleActive={(template) =>
            toggleMutation.mutate({ id: template.id, isActive: !template.isActive })
          }
          togglingId={toggleMutation.isPending ? toggleMutation.variables?.id : undefined}
          // Previously dropped on the floor: a failed activate/deactivate left
          // the row unchanged with no message anywhere on the screen.
          toggleError={toggleMutation.error}
          emptyAction={
            activeFilterCount > 0 ? (
              <Button variant="secondary" onClick={clearFilters}>
                Clear filters
              </Button>
            ) : canWrite ? (
              <Button
                onClick={() => {
                  setIsCreating(true);
                  setFormError(null);
                }}
              >
                New template
              </Button>
            ) : undefined
          }
        />
      </div>
    </div>
  );
}
