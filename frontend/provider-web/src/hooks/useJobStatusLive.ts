"use client";

import * as signalR from "@microsoft/signalr";
import { useQueryClient } from "@tanstack/react-query";
import { useEffect } from "react";
import { API_BASE_URL } from "@/lib/api";
import { getAccessToken } from "@/lib/auth";

/**
 * Task 282's use of `@microsoft/signalr` on provider-web (added to this app
 * for the first time here - it was already present in customer-web and
 * admin-web). Joins the booking's group on `/hubs/tracking` (task 273's hub,
 * shared by all three apps) and invalidates the `["provider-job", jobId]`
 * cache on `BookingStatusChanged`, copying customer-web's
 * `useBookingTracking` connection lifecycle (accessTokenFactory,
 * withAutomaticReconnect, join/leave on mount/unmount, re-join on
 * onreconnected) rather than inventing a second client pattern for the same
 * hub.
 *
 * This is what makes {@link useLocationSharing}'s "stop cleanly on
 * complete/cancel" hold even when the transition happens server-side (an
 * admin cancellation) rather than through this provider's own Start/Complete
 * buttons: without it, `job.status` - and therefore whether the location
 * feed is `active` - would only ever change in response to this provider's
 * own mutations, and a feed left running past a cancellation is exactly the
 * kind of leak task 278's authorization/privacy pass exists to catch on the
 * server if a client ever fails to stop on its own.
 */
export function useJobStatusLive(jobId: string) {
  const queryClient = useQueryClient();

  useEffect(() => {
    if (!jobId) return;

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${API_BASE_URL}/hubs/tracking`, { accessTokenFactory: () => getAccessToken() ?? "" })
      .withAutomaticReconnect()
      .build();

    connection.on("BookingStatusChanged", (payload: { bookingId: string }) => {
      if (payload.bookingId !== jobId) return;
      queryClient.invalidateQueries({ queryKey: ["provider-job", jobId] });
    });

    connection.onreconnected(() => {
      connection.invoke("JoinBooking", jobId).catch(() => {});
    });

    connection
      .start()
      .then(() => connection.invoke("JoinBooking", jobId))
      .catch(() => {
        // Live updates degrade to whatever refetches this screen already
        // triggers on its own actions; nothing here is load-bearing.
      });

    return () => {
      connection.invoke("LeaveBooking", jobId).catch(() => {});
      connection.stop();
    };
  }, [jobId, queryClient]);
}
