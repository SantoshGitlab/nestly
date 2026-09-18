"use client";

import Link from "next/link";
import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Badge, EmptyState, Modal, Spinner } from "@/components/ui";
import { DataTable } from "@/components/data-table";
import type { DataTableColumn } from "@/components/data-table";
import { listActiveProvidersForMapping, listMappedPincodesWithActiveProviderCoverage } from "@/lib/serviceability-api";
import type { MappedPincodeWithActiveProviderCoverageResponse } from "@/lib/serviceability-types";

/**
 * Coverage gap map, 4th quadrant: active service/pincode mappings that ARE
 * genuinely live right now (the other three quadrants only ever list gaps -
 * see this page's own doc comment). Row click drills into exactly which
 * active providers cover that pair, fetched on demand rather than joined
 * into the list query - the list can have hundreds of rows, the provider
 * detail is only ever needed for one at a time.
 */
export function ActiveCoverageSection() {
  const [selected, setSelected] = useState<MappedPincodeWithActiveProviderCoverageResponse | null>(null);

  const coverageQuery = useQuery({
    queryKey: ["mapped-with-active-provider-coverage"],
    queryFn: listMappedPincodesWithActiveProviderCoverage,
  });

  const providersQuery = useQuery({
    queryKey: ["mapping-covering-providers", selected?.mappingId],
    queryFn: () => listActiveProvidersForMapping(selected!.mappingId),
    enabled: selected !== null,
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
      cell: (row) => <span className="nums text-fg-muted">{row.pincodeCode}</span>,
    },
    {
      key: "activeProviderCount",
      header: "Providers",
      numeric: true,
      sortValue: (row) => row.activeProviderCount,
      cell: (row) => (
        <Badge tone="success" className="nums">
          {row.activeProviderCount}
        </Badge>
      ),
    },
  ];

  return (
    <>
      <DataTable
        title="Active coverage"
        description="Live, bookable service/pincode pairs — at least one active provider covers each. Click a row to see which."
        columns={columns}
        rows={coverageQuery.data}
        rowKey={(row) => row.mappingId}
        isLoading={coverageQuery.isPending}
        isFetching={coverageQuery.isFetching}
        error={coverageQuery.error}
        onRetry={() => coverageQuery.refetch()}
        onRowClick={(row) => setSelected(row)}
        caption="Active mappings with active provider coverage"
        emptyTitle="No active coverage"
        emptyDescription="No mapping currently has an active provider covering it."
        hideDensityToggle
        skeletonRows={3}
        minWidth="640px"
        maxHeight="360px"
      />

      <Modal
        open={selected !== null}
        onClose={() => setSelected(null)}
        title={selected ? `${selected.serviceName} · ${selected.pincodeCode}` : ""}
        description="Active providers covering this service/pincode pair."
        size="sm"
      >
        {providersQuery.isPending ? (
          <div className="flex justify-center py-6">
            <Spinner />
          </div>
        ) : providersQuery.data && providersQuery.data.length > 0 ? (
          <ul className="flex flex-col divide-y divide-line">
            {providersQuery.data.map((provider) => (
              <li key={provider.providerId} className="flex items-center justify-between gap-4 py-3 first:pt-0 last:pb-0">
                <div className="min-w-0">
                  <Link
                    href={`/providers/${provider.providerId}`}
                    className="truncate text-sm font-medium text-fg hover:text-brand-600 hover:underline"
                  >
                    {provider.displayName}
                  </Link>
                  <p className="nums text-xs text-fg-muted">{provider.phone}</p>
                </div>
              </li>
            ))}
          </ul>
        ) : (
          <EmptyState title="No providers found" description="Coverage may have just changed — try refreshing." />
        )}
      </Modal>
    </>
  );
}
