/**
 * Typed client for the Partner API's profile/onboarding surface
 * (`/api/v1/profile`). Every call is authenticated.
 */
import { API_V1, apiFetch } from "./api";
import type {
  KycStatusResponse,
  ServiceArea,
  SubmitKycDocumentRequest,
  UpdateProfileRequest,
  UpdateServiceAreasRequest,
  UpdateSkillsRequest,
  PartnerSkill,
} from "./profile-types";
import type { PartnerProfile } from "./types";

const PROFILE_BASE = `${API_V1}/profile`;

export const getProfile = () =>
  apiFetch<PartnerProfile>(PROFILE_BASE, { authenticated: true });

export const updateProfile = (request: UpdateProfileRequest) =>
  apiFetch<PartnerProfile>(PROFILE_BASE, {
    method: "PUT",
    authenticated: true,
    body: JSON.stringify(request),
  });

export const getKycStatus = () =>
  apiFetch<KycStatusResponse>(`${PROFILE_BASE}/kyc`, { authenticated: true });

export const submitKycDocument = (request: SubmitKycDocumentRequest) =>
  apiFetch<KycStatusResponse["documents"][number]>(`${PROFILE_BASE}/kyc/documents`, {
    method: "POST",
    authenticated: true,
    body: JSON.stringify(request),
  });

export const getServiceAreas = () =>
  apiFetch<ServiceArea[]>(`${PROFILE_BASE}/service-areas`, { authenticated: true });

export const updateServiceAreas = (request: UpdateServiceAreasRequest) =>
  apiFetch<ServiceArea[]>(`${PROFILE_BASE}/service-areas`, {
    method: "PUT",
    authenticated: true,
    body: JSON.stringify(request),
  });

export const getSkills = () =>
  apiFetch<PartnerSkill[]>(`${PROFILE_BASE}/skills`, { authenticated: true });

export const updateSkills = (request: UpdateSkillsRequest) =>
  apiFetch<PartnerSkill[]>(`${PROFILE_BASE}/skills`, {
    method: "PUT",
    authenticated: true,
    body: JSON.stringify(request),
  });
