/**
 * Typed client for consumer-api's DeviceTokensController (task 307),
 * mirroring the shape of the other *-api.ts files in this app - and of
 * provider-web's identically-named file, since both apps talk to the same
 * push-registration contract. Scoped to the caller's own customer id
 * server-side - see IDeviceTokenService's doc comment on why the owner is
 * never a request-body field.
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

// Alongside this app's plain "nestly.*" session keys (lib/auth.ts). Holds the
// row id (not the raw token) this session registered, so sign-out can revoke
// exactly the device it created - see SiteHeader.signOut.
const DEVICE_TOKEN_ID_KEY = "nestly.deviceTokenId";

export function storeDeviceTokenId(id: string): void {
  sessionStorage.setItem(DEVICE_TOKEN_ID_KEY, id);
}

export function takeDeviceTokenId(): string | null {
  const id = sessionStorage.getItem(DEVICE_TOKEN_ID_KEY);
  sessionStorage.removeItem(DEVICE_TOKEN_ID_KEY);
  return id;
}
