"use client";

import { useQuery } from "@tanstack/react-query";
import { DataTable } from "@/components/data-table";
import type { DataTableColumn } from "@/components/data-table";
import { listMappedPincodesWithActiveProviderCoverage } from "@/lib/serviceability-api";
import type { MappedPincodeWithActiveProviderCoverageResponse } from "@/lib/serviceability-types";

/**
 * Coverage gap map, fourth quadrant: active service/pincode mappings that ARE
 * actually bookable right now, i.e. at least one active provider covers them.
 * The other three quadrants only ever surface gaps - this is the one place an
 * admin can see which services genuinely have live provider coverage, instead
 * of inferring it by elimination against the gap lists.
 */
export function MappedWithActiveProviderCoverageSection() {
  const coverageQuery = useQuery({
    queryKey: ["mapped-with-active-provider-coverage"],
    queryFn: listMappedPincodesWithActiveProviderCoverage,
  });

  const columns: DataTableColumn<MappedPincodeWithActiveProviderCoverageResponse>[] = [
    {
      key: "service",
      header: "Service",
      sortValue: (row) => row.serviceName,
      cell: (row) => <span className="font-medium text-fg">{row.serviceName}</span>,
    },
    {
      key: "pincode",
      header: "Pincode",
      sortValue: (row) => row.pincodeCode,
      cell: (row) => <span className="nums">{row.pincodeCode}</span>,
    },
    {
      key: "activeProviderCount",
      header: "Active providers",
      sortValue: (row) => row.activeProviderCount,
      cell: (row) => <span className="nums">{row.activeProviderCount}</span>,
    },
  ];

  return (
    <DataTable
      title="Mapped with active provider coverage"
      description="Active mappings that at least one active provider can currently fulfil - what's genuinely live and bookable."
      columns={columns}
      rows={coverageQuery.data}
      rowKey={(row) => row.mappingId}
      isLoading={coverageQuery.isPending}
      isFetching={coverageQuery.isFetching}
      error={coverageQuery.error}
      onRetry={() => coverageQuery.refetch()}
      caption="Active mappings with active provider coverage"
      emptyTitle="No coverage"
      emptyDescription="No active mapping currently has provider coverage."
      hideDensityToggle
      skeletonRows={3}
      minWidth="640px"
      maxHeight="360px"
    />
  );
}
