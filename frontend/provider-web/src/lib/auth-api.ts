/**
 * Typed client for the Provider API's auth surface (`/api/v1/auth`): OTP-based
 * registration and login. None of these calls attach a bearer token - that
 * is the whole point of this surface.
 */
import { API_V1, apiFetch } from "./api";
import type {
  ForgotPasswordRequest,
  LoginWithPasswordRequest,
  LogoutRequest,
  ProviderLoginResponse,
  ProviderProfile,
  RefreshSessionRequest,
  RegisterProviderRequest,
  RegisterProviderWithEmailRequest,
  RequestOtpRequest,
  RequestRegistrationEmailOtpRequest,
  ResetPasswordRequest,
  VerifyLoginOtpRequest,
} from "./types";

const AUTH_BASE = `${API_V1}/auth`;

export const requestRegistrationOtp = (request: RequestOtpRequest) =>
  apiFetch<void>(`${AUTH_BASE}/registration/otp`, {
    method: "POST",
    body: JSON.stringify(request),
  });

export const registerProvider = (request: RegisterProviderRequest) =>
  apiFetch<ProviderProfile>(`${AUTH_BASE}/registration`, {
    method: "POST",
    body: JSON.stringify(request),
  });

/** Email-first registration step 1: send an OTP to an email address instead of a mobile number. */
export const requestRegistrationEmailOtp = (request: RequestRegistrationEmailOtpRequest) =>
  apiFetch<void>(`${AUTH_BASE}/registration/email-otp`, {
    method: "POST",
    body: JSON.stringify(request),
  });

/** Email-first registration step 2: the OTP proves email ownership instead of mobile ownership. */
export const registerProviderWithEmail = (request: RegisterProviderWithEmailRequest) =>
  apiFetch<ProviderProfile>(`${AUTH_BASE}/registration/email`, {
    method: "POST",
    body: JSON.stringify(request),
  });

export const requestLoginOtp = (request: RequestOtpRequest) =>
  apiFetch<void>(`${AUTH_BASE}/login/otp`, {
    method: "POST",
    body: JSON.stringify(request),
  });

export const verifyLoginOtp = (request: VerifyLoginOtpRequest) =>
  apiFetch<ProviderLoginResponse>(`${AUTH_BASE}/login/otp/verify`, {
    method: "POST",
    body: JSON.stringify(request),
  });

/** Task 372: email+password login, when password auth is enabled. */
export const loginWithPassword = (request: LoginWithPasswordRequest) =>
  apiFetch<ProviderLoginResponse>(`${AUTH_BASE}/login/password`, {
    method: "POST",
    body: JSON.stringify(request),
  });

/** Task 372, step 1: request a reset code (sent to the mobile number on file). */
export const requestPasswordReset = (request: ForgotPasswordRequest) =>
  apiFetch<void>(`${AUTH_BASE}/password/forgot`, {
    method: "POST",
    body: JSON.stringify(request),
  });

/** Task 372, step 2: set the new password once the code verifies. */
export const resetPassword = (request: ResetPasswordRequest) =>
  apiFetch<void>(`${AUTH_BASE}/password/reset`, {
    method: "POST",
    body: JSON.stringify(request),
  });

export const refreshSession = (request: RefreshSessionRequest) =>
  apiFetch<ProviderLoginResponse>(`${AUTH_BASE}/refresh`, {
    method: "POST",
    body: JSON.stringify(request),
  });

export const logoutProvider = (request: LogoutRequest) =>
  apiFetch<void>(`${AUTH_BASE}/logout`, {
    method: "POST",
    body: JSON.stringify(request),
  });
