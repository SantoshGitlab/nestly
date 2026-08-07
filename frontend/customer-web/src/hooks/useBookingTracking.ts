"use client";

import * as signalR from "@microsoft/signalr";
import { useQueryClient } from "@tanstack/react-query";
import { useEffect } from "react";
import { API_BASE_URL } from "@/lib/api";
import { getAccessToken } from "@/lib/auth";
import { BookingStatus } from "@/lib/types";

/**
 * Task 279. Live half of the booking detail page: joins the booking's group
 * on `/hubs/tracking` (task 273's hub, shared by all three apps) and
 * invalidates the `["booking", id]` REST cache on `BookingStatusChanged` -
 * copying ChatWidget's connection lifecycle (accessTokenFactory,
 * withAutomaticReconnect, join/leave on mount/unmount, re-join on
 * onreconnected) rather than inventing a second client pattern.
 *
 * Invalidate, not patch: `BookingStatusChangedBroadcast` carries only the
 * booking id and the two status values (see its doc comment on the wire's PII
 * rule) - not the new status label, timeline entry, provider assignment or
 * any of the other fields the page renders off the same transition. Patching
 * the cache from a payload that thin would mean rendering a stale timeline
 * next to an up-to-date badge; a refetch is the only way to get a consistent
 * booking back.
 *
 * REST is still the source of truth: this hook never blocks rendering and
 * never introduces a "waiting for the socket" state. If the connection never
 * opens, the booking simply stops updating until the caller's own
 * `refetchInterval` polling ticks over instead - see the `useQuery` call on
 * the booking detail page.
 */
export function useBookingTracking(bookingId: string | undefined) {
  const queryClient = useQueryClient();

  useEffect(() => {
    if (!bookingId) return;

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${API_BASE_URL}/hubs/tracking`, { accessTokenFactory: () => getAccessToken() ?? "" })
      .withAutomaticReconnect()
      .build();

    connection.on("BookingStatusChanged", (payload: { bookingId: string }) => {
      if (payload.bookingId !== bookingId) return;
      queryClient.invalidateQueries({ queryKey: ["booking", bookingId] });
    });

    connection.onreconnected(() => {
      connection.invoke("JoinBooking", bookingId).catch(() => {});
    });

    connection
      .start()
      .then(() => connection.invoke("JoinBooking", bookingId))
      .catch(() => {
        // Live delivery degrades to REST-only polling; nothing here is load-bearing.
      });

    return () => {
      connection.invoke("LeaveBooking", bookingId).catch(() => {});
      connection.stop();
    };
  }, [bookingId, queryClient]);
}

/** Mirrors Nestly.Domain.BookingLifecycle.TrackableStatuses - see its doc comment for why this exact set. */
const TRACKABLE_STATUSES: ReadonlySet<BookingStatus> = new Set([
  BookingStatus.Assigned,
  BookingStatus.ProviderEnRoute,
  BookingStatus.ProviderArrived,
  BookingStatus.InProgress,
]);

export function isBookingTrackable(status: BookingStatus): boolean {
  return TRACKABLE_STATUSES.has(status);
}
