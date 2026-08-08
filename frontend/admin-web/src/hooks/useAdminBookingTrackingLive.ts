"use client";

import * as signalR from "@microsoft/signalr";
import { useQueryClient } from "@tanstack/react-query";
import { useEffect } from "react";
import { API_BASE_URL } from "@/lib/api";
import { getAccessToken } from "@/lib/auth";
import type { AdminBookingTrackingResponse } from "@/lib/bookings-types";
import { BookingStatus } from "@/lib/types";

/**
 * Task 284's live half of the ops view: joins the booking's group on
 * `/hubs/tracking` with the admin JWT (the hub is already mapped by
 * admin-api - see BookingTrackingAuthorizer's AdminPermissionCode branch,
 * task 273) and keeps the `["admin-booking-tracking", bookingId]` cache
 * live, so ops watch a job without refreshing.
 *
 * Copies customer-web's `useLiveBookingTracking` split exactly: patch the
 * cache directly for `ProviderLocationUpdated`/`EtaUpdated` (both are
 * field-for-field a subset of `providerLocation`/`eta` - see
 * `TrackingBroadcastContracts.cs`), invalidate (don't patch) on
 * `BookingStatusChanged` since that payload can't rebuild the rest of the
 * response and can flip whether tracking exists at all.
 */
export function useAdminBookingTrackingLive(bookingId: string, enabled: boolean) {
  const queryClient = useQueryClient();

  useEffect(() => {
    if (!enabled) return;

    const connection = new signalR.HubConnectionBuilder()
      // withCredentials: false - see the chat hub connection in
      // app/(admin)/chat/[threadId]/page.tsx for why: admin-api's CORS
      // policy is credential-less by design (AddNestlyCors), so a
      // credentialed XHR preflight is blocked before it can connect.
      .withUrl(`${API_BASE_URL}/hubs/tracking`, { accessTokenFactory: () => getAccessToken() ?? "", withCredentials: false })
      .withAutomaticReconnect()
      .build();

    connection.on("BookingStatusChanged", (payload: { bookingId: string }) => {
      if (payload.bookingId !== bookingId) return;
      queryClient.invalidateQueries({ queryKey: ["admin-booking-tracking", bookingId] });
    });

    connection.on(
      "ProviderLocationUpdated",
      (payload: { bookingId: string; latitude: number; longitude: number; recordedAtUtc: string }) => {
        if (payload.bookingId !== bookingId) return;
        queryClient.setQueryData<AdminBookingTrackingResponse>(["admin-booking-tracking", bookingId], (current) =>
          current
            ? {
                ...current,
                providerLocation: {
                  latitude: payload.latitude,
                  longitude: payload.longitude,
                  recordedAtUtc: payload.recordedAtUtc,
                },
              }
            : current,
        );
      },
    );

    connection.on(
      "EtaUpdated",
      (payload: { bookingId: string; etaSeconds: number; etaComputedAtUtc: string }) => {
        if (payload.bookingId !== bookingId) return;
        queryClient.setQueryData<AdminBookingTrackingResponse>(["admin-booking-tracking", bookingId], (current) =>
          current
            ? { ...current, eta: { etaSeconds: payload.etaSeconds, etaComputedAtUtc: payload.etaComputedAtUtc } }
            : current,
        );
      },
    );

    connection.onreconnected(() => {
      connection.invoke("JoinBooking", bookingId).catch(() => {});
    });

    connection
      .start()
      .then(() => connection.invoke("JoinBooking", bookingId))
      .catch(() => {
        // No live updates; the ops view still shows whatever the last REST read returned.
      });

    return () => {
      connection.invoke("LeaveBooking", bookingId).catch(() => {});
      connection.stop();
    };
  }, [bookingId, enabled, queryClient]);
}

/** Mirrors Nestly.Domain.BookingLifecycle.TrackableStatuses - see its doc comment for why this exact set. Kept live-only: a completed/cancelled booking's tracking card renders the plain "no live data" state instead of joining a hub group or polling. */
const TRACKABLE_STATUSES: ReadonlySet<BookingStatus> = new Set([
  BookingStatus.Assigned,
  BookingStatus.ProviderEnRoute,
  BookingStatus.ProviderArrived,
  BookingStatus.InProgress,
]);

export function isBookingTrackable(status: BookingStatus): boolean {
  return TRACKABLE_STATUSES.has(status);
}
