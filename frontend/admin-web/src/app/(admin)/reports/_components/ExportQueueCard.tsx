"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { Alert, Badge, Button, Card, Select, useToast } from "@/components/ui";
import type { BadgeTone } from "@/components/ui";
import { DataTable, FormActions, FormGrid, formatDateTime } from "@/components/data-table";
import type { DataTableColumn } from "@/components/data-table";
import { describeError } from "@/lib/api";
import { downloadBlob, downloadExportJob, listMyExportJobs, requestExportJob } from "@/lib/reports-api";
import { ExportJobStatus, ExportReportType } from "@/lib/reports-types";
import type { ExportJobStatusResponse } from "@/lib/reports-types";
import { REPORT_TYPE_OPTIONS, reportTypeLabel } from "../_lib/report-types";

const JOB_STATUS: Record<ExportJobStatus, { label: string; tone: BadgeTone }> = {
  [ExportJobStatus.Pending]: { label: "Pending", tone: "neutral" },
  [ExportJobStatus.Processing]: { label: "Processing", tone: "info" },
  [ExportJobStatus.Completed]: { label: "Completed", tone: "success" },
  [ExportJobStatus.Failed]: { label: "Failed", tone: "danger" },
};

function JobStatusBadge({ status }: { status: ExportJobStatus }) {
  const meta = JOB_STATUS[status] ?? { label: "Unknown", tone: "neutral" as BadgeTone };
  return <Badge tone={meta.tone}>{meta.label}</Badge>;
}

/**
 * The async export queue (SRS 12.18.2, task 128d): for a date range too large
 * to render inline, a background worker builds the file and the admin comes
 * back for it. Gated on "reports.write" by the caller, since requesting one
 * persists a job and occupies a worker slot.
 */
export function ExportQueueCard({
  fromUtc,
  toUtc,
  city,
}: {
  fromUtc: string;
  toUtc: string;
  /** Only meaningful for the Booking & Revenue report; ignored for the rest. */
  city: string;
}) {
  const queryClient = useQueryClient();
  const toast = useToast();

  const [reportType, setReportType] = useState(String(ExportReportType.BookingRevenue));
  const [downloadingJobId, setDownloadingJobId] = useState<string | null>(null);
  const [downloadError, setDownloadError] = useState<string | null>(null);

  const jobsQuery = useQuery({
    queryKey: ["reports", "my-exports"] as const,
    queryFn: listMyExportJobs,
    refetchInterval: (query) => {
      const jobs = query.state.data ?? [];
      const hasInFlight = jobs.some(
        (job) => job.status === ExportJobStatus.Pending || job.status === ExportJobStatus.Processing,
      );
      return hasInFlight ? 3000 : false;
    },
  });

  const requestMutation = useMutation({
    mutationFn: () =>
      requestExportJob({
        reportType: Number(reportType) as ExportReportType,
        fromUtc,
        toUtc,
        city: Number(reportType) === ExportReportType.BookingRevenue ? city || null : null,
      }),
    onSuccess: () => {
      toast("success", "Export queued. It appears below as soon as the worker picks it up.");
      // The new job must show immediately, and the poller only restarts once
      // the list actually contains an in-flight job.
      void queryClient.invalidateQueries({ queryKey: ["reports", "my-exports"] });
    },
  });

  async function download(job: ExportJobStatusResponse) {
    // Guard the whole card rather than one row: two concurrent downloads of
    // the same blob produce two files and one confusing error.
    if (downloadingJobId) return;
    setDownloadingJobId(job.id);
    setDownloadError(null);
    try {
      const blob = await downloadExportJob(job.id);
      const label = reportTypeLabel(job.reportType).toLowerCase().replace(/\s+/g, "-");
      downloadBlob(blob, `${label}-${job.id}.csv`);
    } catch (err) {
      setDownloadError(describeError(err));
    } finally {
      setDownloadingJobId(null);
    }
  }

  const columns: DataTableColumn<ExportJobStatusResponse>[] = [
    {
      key: "report",
      header: "Report",
      cell: (job) => <span className="font-medium text-fg">{reportTypeLabel(job.reportType)}</span>,
    },
    { key: "status", header: "Status", cell: (job) => <JobStatusBadge status={job.status} /> },
    {
      key: "requested",
      header: "Requested",
      className: "whitespace-nowrap",
      cell: (job) => <span className="nums">{formatDateTime(job.requestedAtUtc)}</span>,
    },
    {
      key: "completed",
      header: "Completed",
      className: "whitespace-nowrap",
      cell: (job) =>
        job.completedAtUtc ? (
          <span className="nums">{formatDateTime(job.completedAtUtc)}</span>
        ) : (
          <span className="text-fg-subtle">—</span>
        ),
    },
    {
      key: "detail",
      header: "Detail",
      cell: (job) =>
        job.errorMessage ? (
          <span className="text-danger">{job.errorMessage}</span>
        ) : (
          <span className="text-fg-subtle">—</span>
        ),
    },
  ];

  return (
    <Card
      title="Async export queue"
      description="For a large date range: a background job generates the file, and this list refreshes itself while anything is still running (SRS 12.18.2, task 128d)."
    >
      <div className="flex flex-col gap-6">
        <form
          className="flex flex-col gap-4"
          onSubmit={(event) => {
            event.preventDefault();
            requestMutation.mutate();
          }}
        >
          {requestMutation.isError ? <Alert>{describeError(requestMutation.error)}</Alert> : null}
          <FormGrid columns={2}>
            <Select
              label="Report"
              options={REPORT_TYPE_OPTIONS}
              value={reportType}
              onChange={(event) => setReportType(event.target.value)}
            />
          </FormGrid>
          <FormActions align="start">
            <Button type="submit" loading={requestMutation.isPending}>
              Request export
            </Button>
            <span className="text-xs text-fg-subtle">
              Uses the date range selected at the top of this page.
            </span>
          </FormActions>
        </form>

        {downloadError ? <Alert>{downloadError}</Alert> : null}

        <DataTable
          title="My exports"
          description="Only the jobs you requested. Completed files stay available for download."
          columns={columns}
          rows={jobsQuery.data}
          rowKey={(job) => job.id}
          isLoading={jobsQuery.isPending}
          isFetching={jobsQuery.isFetching}
          error={jobsQuery.error}
          onRetry={() => void jobsQuery.refetch()}
          caption="Export jobs you have requested"
          emptyTitle="No exports requested yet"
          emptyDescription="Pick a report above and request one — it will show up here."
          skeletonRows={3}
          minWidth="880px"
          hideDensityToggle
          rowActions={(job) =>
            job.status === ExportJobStatus.Completed && job.hasResult ? (
              <Button
                type="button"
                size="sm"
                variant="secondary"
                loading={downloadingJobId === job.id}
                disabled={downloadingJobId !== null && downloadingJobId !== job.id}
                onClick={() => void download(job)}
              >
                Download
              </Button>
            ) : null
          }
        />
      </div>
    </Card>
  );
}
