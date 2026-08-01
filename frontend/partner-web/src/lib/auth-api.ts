/**
 * Typed client for the Partner API's auth surface (`/api/v1/auth`): OTP-based
 * registration and login. None of these calls attach a bearer token - that
 * is the whole point of this surface.
 */
import { API_V1, apiFetch } from "./api";
import type {
  LogoutRequest,
  PartnerLoginResponse,
  PartnerProfile,
  RefreshSessionRequest,
  RegisterPartnerRequest,
  RequestOtpRequest,
  VerifyLoginOtpRequest,
} from "./types";

const AUTH_BASE = `${API_V1}/auth`;

export const requestRegistrationOtp = (request: RequestOtpRequest) =>
  apiFetch<void>(`${AUTH_BASE}/registration/otp`, {
    method: "POST",
    body: JSON.stringify(request),
  });

export const registerPartner = (request: RegisterPartnerRequest) =>
  apiFetch<PartnerProfile>(`${AUTH_BASE}/registration`, {
    method: "POST",
    body: JSON.stringify(request),
  });

export const requestLoginOtp = (request: RequestOtpRequest) =>
  apiFetch<void>(`${AUTH_BASE}/login/otp`, {
    method: "POST",
    body: JSON.stringify(request),
  });

export const verifyLoginOtp = (request: VerifyLoginOtpRequest) =>
  apiFetch<PartnerLoginResponse>(`${AUTH_BASE}/login/otp/verify`, {
    method: "POST",
    body: JSON.stringify(request),
  });

export const refreshSession = (request: RefreshSessionRequest) =>
  apiFetch<PartnerLoginResponse>(`${AUTH_BASE}/refresh`, {
    method: "POST",
    body: JSON.stringify(request),
  });

export const logoutPartner = (request: LogoutRequest) =>
  apiFetch<void>(`${AUTH_BASE}/logout`, {
    method: "POST",
    body: JSON.stringify(request),
  });
