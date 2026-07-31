/**
 * Typed client for the Partner API's jobs surface (`/api/v1/jobs`). Every
 * call is authenticated. See jobs-types.ts's doc comment: the backend behind
 * this surface currently answers 501 (sibling task #147 not yet merged) -
 * api.ts's `isNotImplemented` helper is how callers detect that and render
 * an empty state instead of a hard error.
 */
import { API_V1, apiFetch } from "./api";
import type { JobDetail, JobListItem, JobListParams, SubmitCompletionProofRequest } from "./jobs-types";

const JOBS_BASE = `${API_V1}/jobs`;

function query(params: JobListParams): string {
  const entries = Object.entries(params).filter(([, value]) => value !== undefined && value !== "");
  if (entries.length === 0) return "";
  return `?${new URLSearchParams(entries as [string, string][]).toString()}`;
}

export const listJobs = (params: JobListParams) =>
  apiFetch<JobListItem[]>(`${JOBS_BASE}${query(params)}`, { authenticated: true });

export const getJobDetail = (jobId: string) =>
  apiFetch<JobDetail>(`${JOBS_BASE}/${jobId}`, { authenticated: true });

export const acceptJob = (jobId: string) =>
  apiFetch<JobDetail>(`${JOBS_BASE}/${jobId}/accept`, { method: "POST", authenticated: true });

export const rejectJob = (jobId: string) =>
  apiFetch<JobDetail>(`${JOBS_BASE}/${jobId}/reject`, { method: "POST", authenticated: true });

export const startJob = (jobId: string) =>
  apiFetch<JobDetail>(`${JOBS_BASE}/${jobId}/start`, { method: "POST", authenticated: true });

export const completeJob = (jobId: string) =>
  apiFetch<JobDetail>(`${JOBS_BASE}/${jobId}/complete`, { method: "POST", authenticated: true });

export const submitCompletionProof = (jobId: string, request: SubmitCompletionProofRequest) =>
  apiFetch<JobDetail>(`${JOBS_BASE}/${jobId}/completion-proof`, {
    method: "POST",
    authenticated: true,
    body: JSON.stringify(request),
  });
