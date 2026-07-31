/**
 * Typed client for the Admin API's notification template surface (SRS 12.17,
 * tasks 126a-d): `NotificationTemplatesController` - template CRUD plus
 * preview/test rendering. Every call is authenticated - these are admin-only
 * endpoints gated behind the "notifications" permission module server-side.
 */
import { API_V1, apiFetch } from "./api";
import type {
  NotificationTemplateAdHocPreviewRequest,
  NotificationTemplateCreateRequest,
  NotificationTemplateListParams,
  NotificationTemplatePreviewRequest,
  NotificationTemplatePreviewResponse,
  NotificationTemplateResponse,
  NotificationTemplateUpdateRequest,
} from "./notification-template-types";

const TEMPLATES_BASE = `${API_V1}/notification-templates`;

function query(params: object): string {
  const entries = Object.entries(params as Record<string, string | number | boolean | undefined>)
    .filter(([, value]) => value !== undefined);
  if (entries.length === 0) return "";
  return `?${new URLSearchParams(entries.map(([key, value]) => [key, String(value)])).toString()}`;
}

export const listNotificationTemplates = (params: NotificationTemplateListParams = {}) =>
  apiFetch<NotificationTemplateResponse[]>(`${TEMPLATES_BASE}${query(params)}`, { authenticated: true });

export const getNotificationTemplate = (id: string) =>
  apiFetch<NotificationTemplateResponse>(`${TEMPLATES_BASE}/${id}`, { authenticated: true });

export const createNotificationTemplate = (request: NotificationTemplateCreateRequest) =>
  apiFetch<NotificationTemplateResponse>(TEMPLATES_BASE, {
    method: "POST",
    authenticated: true,
    body: JSON.stringify(request),
  });

export const updateNotificationTemplate = (id: string, request: NotificationTemplateUpdateRequest) =>
  apiFetch<NotificationTemplateResponse>(`${TEMPLATES_BASE}/${id}`, {
    method: "PUT",
    authenticated: true,
    body: JSON.stringify(request),
  });

export const setNotificationTemplateActive = (id: string, isActive: boolean) =>
  apiFetch<void>(`${TEMPLATES_BASE}/${id}/${isActive ? "activate" : "deactivate"}`, {
    method: "POST",
    authenticated: true,
  });

export const previewNotificationTemplate = (id: string, request: NotificationTemplatePreviewRequest) =>
  apiFetch<NotificationTemplatePreviewResponse>(`${TEMPLATES_BASE}/${id}/preview`, {
    method: "POST",
    authenticated: true,
    body: JSON.stringify(request),
  });

export const previewNotificationTemplateAdHoc = (request: NotificationTemplateAdHocPreviewRequest) =>
  apiFetch<NotificationTemplatePreviewResponse>(`${TEMPLATES_BASE}/preview`, {
    method: "POST",
    authenticated: true,
    body: JSON.stringify(request),
  });
