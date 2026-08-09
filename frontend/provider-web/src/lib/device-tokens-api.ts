/**
 * Typed client for provider-api's DeviceTokensController (task 307),
 * mirroring the shape of the other *-api.ts files in this app. Scoped to
 * the caller's own provider id server-side - see IDeviceTokenService's doc
 * comment on why the owner is never a request-body field.
 */
import { API_V1, apiFetch } from "./api";

const DEVICE_TOKENS_BASE = `${API_V1}/device-tokens`;

// No JsonStringEnumConverter is registered anywhere in this solution (see
// BookingStatus.cs's own comment on this), so every enum crosses the wire as
// its ordinal - mirrors Nestly.Domain.DevicePlatform's declaration order
// exactly, same convention as admin-web's AdminUserStatus.
export enum DevicePlatform {
  Fcm = 0,
  Apns = 1,
}

export interface DeviceTokenResponse {
  id: string;
  platform: DevicePlatform;
  token: string;
  isActive: boolean;
  registeredAtUtc: string;
}

export const registerDeviceToken = (platform: DevicePlatform, token: string) =>
  apiFetch<DeviceTokenResponse>(DEVICE_TOKENS_BASE, {
    method: "POST",
    authenticated: true,
    body: JSON.stringify({ platform, token }),
  });

export const revokeDeviceToken = (deviceTokenId: string) =>
  apiFetch<void>(`${DEVICE_TOKENS_BASE}/${deviceTokenId}`, {
    method: "DELETE",
    authenticated: true,
  });

// Namespaced alongside auth.ts's "nestly.provider.*" session keys. Holds the
// row id (not the raw token) this session registered, so sign-out can revoke
// exactly the device it created - see ProviderHeader.signOut.
const DEVICE_TOKEN_ID_KEY = "nestly.provider.deviceTokenId";

export function storeDeviceTokenId(id: string): void {
  sessionStorage.setItem(DEVICE_TOKEN_ID_KEY, id);
}

export function takeDeviceTokenId(): string | null {
  const id = sessionStorage.getItem(DEVICE_TOKEN_ID_KEY);
  sessionStorage.removeItem(DEVICE_TOKEN_ID_KEY);
  return id;
}
