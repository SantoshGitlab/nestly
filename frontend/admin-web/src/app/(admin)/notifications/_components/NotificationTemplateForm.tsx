"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useEffect } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Button, Field, Select, Textarea } from "@/components/ui";
import {
  NOTIFICATION_CHANNEL_LABELS,
  NOTIFICATION_EVENT_TYPE_LABELS,
  NotificationChannel,
  NotificationEventType,
  type NotificationTemplateCreateRequest,
  type NotificationTemplateResponse,
  type NotificationTemplateUpdateRequest,
} from "@/lib/notification-template-types";

/**
 * Create/edit form for a notification template (SRS 12.17.1-2, tasks 126a-d,
 * 127). One component serves both modes - `template` present means "edit":
 * event type, channel and template key become read-only (see
 * `NotificationTemplate`'s doc comment for why that trio is immutable once
 * created) and only subject/body are resubmitted, matching
 * `NotificationTemplateUpdateRequest`'s shape.
 *
 * The subject field enforces the same channel rule the backend entity does
 * (SMS has no subject; email/push require one) so a validation error never
 * has to make a round trip to be caught.
 */

const EVENT_TYPE_OPTIONS = Object.entries(NOTIFICATION_EVENT_TYPE_LABELS).map(([value, label]) => ({
  value,
  label,
}));

const CHANNEL_OPTIONS = Object.entries(NOTIFICATION_CHANNEL_LABELS).map(([value, label]) => ({
  value,
  label,
}));

const templateFormSchema = z
  .object({
    eventType: z.number().int(),
    channel: z.number().int(),
    templateKey: z.string().min(1, "Template key is required").max(100, "Template key must be 100 characters or fewer"),
    subject: z.string().max(300, "Subject must be 300 characters or fewer"),
    body: z.string().min(1, "Body is required").max(4000, "Body must be 4000 characters or fewer"),
  })
  .refine((values) => values.channel !== NotificationChannel.Sms || values.subject.trim() === "", {
    path: ["subject"],
    message: "An SMS template cannot have a subject.",
  })
  .refine((values) => values.channel === NotificationChannel.Sms || values.subject.trim() !== "", {
    path: ["subject"],
    message: "Email and push templates require a subject.",
  });

export type NotificationTemplateFormValues = z.infer<typeof templateFormSchema>;

function defaultValuesFor(template: NotificationTemplateResponse | null): NotificationTemplateFormValues {
  if (!template) {
    return {
      eventType: NotificationEventType.Welcome,
      channel: NotificationChannel.Sms,
      templateKey: "",
      subject: "",
      body: "",
    };
  }

  return {
    eventType: template.eventType,
    channel: template.channel,
    templateKey: template.templateKey,
    subject: template.subject ?? "",
    body: template.body,
  };
}

export function NotificationTemplateForm({
  template,
  isSubmitting,
  submitError,
  onSubmit,
  onCancel,
  onValuesChange,
}: {
  /** Present in edit mode; null when creating a new template. */
  template: NotificationTemplateResponse | null;
  isSubmitting: boolean;
  submitError: string | null;
  onSubmit: (request: NotificationTemplateCreateRequest | NotificationTemplateUpdateRequest) => void;
  onCancel?: () => void;
  /** Fires on every keystroke so the preview panel can render the draft as-you-type. */
  onValuesChange?: (values: NotificationTemplateFormValues) => void;
}) {
  const form = useForm<NotificationTemplateFormValues>({
    resolver: zodResolver(templateFormSchema),
    defaultValues: defaultValuesFor(template),
  });

  // Re-seed the form whenever the template being edited changes (switching
  // from "create" to "edit", or between two different rows) - react-hook-form
  // only applies `defaultValues` once, at mount.
  useEffect(() => {
    form.reset(defaultValuesFor(template));
  }, [template, form]);

  const watched = form.watch();
  useEffect(() => {
    onValuesChange?.(watched);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [watched.eventType, watched.channel, watched.templateKey, watched.subject, watched.body]);

  const isEditing = template !== null;
  const channel = form.watch("channel");
  const isSms = Number(channel) === NotificationChannel.Sms;

  const submit = form.handleSubmit((values) => {
    const subject = values.subject.trim() === "" ? null : values.subject.trim();

    if (isEditing) {
      onSubmit({ subject, body: values.body } satisfies NotificationTemplateUpdateRequest);
    } else {
      onSubmit({
        eventType: values.eventType as NotificationEventType,
        channel: values.channel as NotificationChannel,
        templateKey: values.templateKey,
        subject,
        body: values.body,
      } satisfies NotificationTemplateCreateRequest);
    }
  });

  return (
    <form onSubmit={submit} className="flex flex-col gap-4" noValidate>
      {submitError ? <Alert>{submitError}</Alert> : null}

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
        <Select
          label="Trigger event"
          error={form.formState.errors.eventType?.message}
          options={EVENT_TYPE_OPTIONS}
          disabled={isEditing}
          title={isEditing ? "The trigger event cannot be changed after creation." : undefined}
          {...form.register("eventType", { valueAsNumber: true })}
        />
        <Select
          label="Channel"
          error={form.formState.errors.channel?.message}
          options={CHANNEL_OPTIONS}
          disabled={isEditing}
          title={isEditing ? "The channel cannot be changed after creation." : undefined}
          {...form.register("channel", { valueAsNumber: true })}
        />
        <Field
          label="Template key"
          error={form.formState.errors.templateKey?.message}
          readOnly={isEditing}
          title={isEditing ? "The template key cannot be changed after creation." : undefined}
          {...form.register("templateKey")}
        />
      </div>

      {!isSms ? (
        <Field
          label="Subject / push title"
          error={form.formState.errors.subject?.message}
          {...form.register("subject")}
        />
      ) : (
        <input type="hidden" {...form.register("subject")} />
      )}

      <Textarea
        label="Body"
        rows={6}
        error={form.formState.errors.body?.message}
        placeholder="Hi {{CustomerName}}, ..."
        {...form.register("body")}
      />

      <p className="text-xs text-neutral-600 dark:text-neutral-400">
        Use <code>{"{{VariableName}}"}</code> placeholders - they are substituted with real values at send time (SRS 12.17.2).
      </p>

      <div className="flex gap-3">
        <Button type="submit" disabled={isSubmitting}>
          {isSubmitting ? "Saving…" : isEditing ? "Save changes" : "Create template"}
        </Button>
        {onCancel ? (
          <Button type="button" variant="secondary" onClick={onCancel} disabled={isSubmitting}>
            Cancel
          </Button>
        ) : null}
      </div>
    </form>
  );
}
