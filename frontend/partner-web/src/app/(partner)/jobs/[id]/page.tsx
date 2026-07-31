"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useParams, useRouter } from "next/navigation";
import { useState } from "react";
import { Alert, Button, Card, Field, PageHeading } from "@/components/ui";
import { describeError, isNotImplemented } from "@/lib/api";
import { acceptJob, completeJob, getJobDetail, rejectJob, startJob, submitCompletionProof } from "@/lib/jobs-api";

/**
 * Job detail (docs/PARTNER.md's `booking_partner_assignment` bridge table):
 * accept/reject/start/complete actions plus completion proof submission.
 * Same 501-stub caveat as the list page - the backend for this surface is
 * pending sibling task #147, so this renders an explicit "not yet available"
 * state instead of a hard error.
 */
export default function JobDetailPage() {
  const params = useParams<{ id: string }>();
  const jobId = params.id;
  const router = useRouter();
  const queryClient = useQueryClient();
  const [proofRef, setProofRef] = useState("");

  const query = useQuery({ queryKey: ["partner-job", jobId], queryFn: () => getJobDetail(jobId) });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ["partner-job", jobId] });

  const acceptMutation = useMutation({ mutationFn: () => acceptJob(jobId), onSuccess: invalidate });
  const rejectMutation = useMutation({ mutationFn: () => rejectJob(jobId), onSuccess: invalidate });
  const startMutation = useMutation({ mutationFn: () => startJob(jobId), onSuccess: invalidate });
  const completeMutation = useMutation({ mutationFn: () => completeJob(jobId), onSuccess: invalidate });
  const proofMutation = useMutation({
    mutationFn: () => submitCompletionProof(jobId, { fileRef: proofRef }),
    onSuccess: () => {
      setProofRef("");
      invalidate();
    },
  });

  const anyActionPending =
    acceptMutation.isPending || rejectMutation.isPending || startMutation.isPending || completeMutation.isPending;

  if (query.isPending) {
    return <p className="p-2 text-sm text-neutral-500">Loading job…</p>;
  }

  if (query.isError && isNotImplemented(query.error)) {
    return (
      <div className="mx-auto w-full max-w-2xl">
        <PageHeading title="Job detail" />
        <Card title="This job isn't available yet">
          <p className="text-sm text-neutral-600 dark:text-neutral-400">
            Job assignment is still being built on the platform side. Check back once your account can receive
            bookings.
          </p>
          <div className="mt-4">
            <Button type="button" variant="secondary" onClick={() => router.push("/jobs")}>
              Back to jobs
            </Button>
          </div>
        </Card>
      </div>
    );
  }

  if (query.isError) {
    return (
      <div className="mx-auto w-full max-w-2xl">
        <PageHeading title="Job detail" />
        <Alert>{describeError(query.error)}</Alert>
      </div>
    );
  }

  const job = query.data;
  const actionError =
    acceptMutation.error ?? rejectMutation.error ?? startMutation.error ?? completeMutation.error ?? proofMutation.error;

  return (
    <div className="mx-auto w-full max-w-2xl">
      <PageHeading title={`Job ${job.bookingId}`} subtitle={`Status: ${job.status}`} />

      {actionError ? (
        <div className="mb-4">
          <Alert>{describeError(actionError)}</Alert>
        </div>
      ) : null}

      <Card title="Details">
        <dl className="grid grid-cols-1 gap-3 text-sm sm:grid-cols-2">
          <div>
            <dt className="font-medium">Booking ID</dt>
            <dd>{job.bookingId}</dd>
          </div>
          <div>
            <dt className="font-medium">Status</dt>
            <dd>{job.status}</dd>
          </div>
          <div>
            <dt className="font-medium">Assigned</dt>
            <dd>{new Date(job.assignedAt).toLocaleString()}</dd>
          </div>
          <div>
            <dt className="font-medium">Response deadline</dt>
            <dd>{job.responseDeadline ? new Date(job.responseDeadline).toLocaleString() : "—"}</dd>
          </div>
          {job.customerName ? (
            <div>
              <dt className="font-medium">Customer</dt>
              <dd>{job.customerName}</dd>
            </div>
          ) : null}
          {job.serviceName ? (
            <div>
              <dt className="font-medium">Service</dt>
              <dd>{job.serviceName}</dd>
            </div>
          ) : null}
          {job.addressLine ? (
            <div className="sm:col-span-2">
              <dt className="font-medium">Address</dt>
              <dd>{job.addressLine}</dd>
            </div>
          ) : null}
        </dl>
      </Card>

      <div className="mt-6">
        <Card title="Actions">
          <div className="flex flex-wrap gap-2">
            {job.status === "Assigned" ? (
              <>
                <Button type="button" disabled={anyActionPending} onClick={() => acceptMutation.mutate()}>
                  {acceptMutation.isPending ? "Accepting…" : "Accept"}
                </Button>
                <Button
                  type="button"
                  variant="danger"
                  disabled={anyActionPending}
                  onClick={() => rejectMutation.mutate()}
                >
                  {rejectMutation.isPending ? "Rejecting…" : "Reject"}
                </Button>
              </>
            ) : null}
            {job.status === "Accepted" ? (
              <Button type="button" disabled={anyActionPending} onClick={() => startMutation.mutate()}>
                {startMutation.isPending ? "Starting…" : "Start job"}
              </Button>
            ) : null}
            {job.status === "InProgress" ? (
              <Button type="button" disabled={anyActionPending} onClick={() => completeMutation.mutate()}>
                {completeMutation.isPending ? "Completing…" : "Mark complete"}
              </Button>
            ) : null}
            {job.status !== "Assigned" && job.status !== "Accepted" && job.status !== "InProgress" ? (
              <p className="text-sm text-neutral-600 dark:text-neutral-400">No actions available for this status.</p>
            ) : null}
          </div>

          {job.status === "InProgress" || job.status === "Completed" ? (
            <form
              onSubmit={(e) => {
                e.preventDefault();
                proofMutation.mutate();
              }}
              className="mt-5 flex flex-wrap items-end gap-3 border-t border-black/10 pt-4 dark:border-white/15"
            >
              {/* Same "no upload backend yet" caveat as the KYC document form -
                  this is a text reference, not a real file upload. */}
              <div className="w-72">
                <Field
                  label="Completion proof (file reference or URL)"
                  value={proofRef}
                  onChange={(e) => setProofRef(e.target.value)}
                  placeholder="https://…"
                />
              </div>
              <Button type="submit" disabled={proofMutation.isPending || proofRef.trim() === ""}>
                {proofMutation.isPending ? "Submitting…" : "Submit completion proof"}
              </Button>
            </form>
          ) : null}
        </Card>
      </div>
    </div>
  );
}
