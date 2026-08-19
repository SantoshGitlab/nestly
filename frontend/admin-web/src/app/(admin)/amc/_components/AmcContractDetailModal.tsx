"use client";

import { Badge, Modal, Skeleton } from "@/components/ui";
import type { BadgeTone } from "@/components/ui";
import { DescriptionList, formatDate } from "@/components/data-table";
import { SectionError } from "@/components/screen-states";
import { CONTRACT_STATUS_LABELS, CustomerAmcContractStatus } from "../_lib/amc-api";
import type { AmcContractAdminListItemResponse } from "../_lib/amc-api";

const STATUS_TONES: Record<CustomerAmcContractStatus, BadgeTone> = {
  [CustomerAmcContractStatus.Active]: "success",
  [CustomerAmcContractStatus.Exhausted]: "warning",
  [CustomerAmcContractStatus.Expired]: "neutral",
  [CustomerAmcContractStatus.Cancelled]: "neutral",
};

/**
 * Contract detail as a dialog rather than a dedicated route: the admin
 * surface is read-only (docs/AMC.md's RBAC ADDITIONS - AmcContractsController
 * has no mutating actions, same as RecurringPlansController), and
 * `GetContractByIdAsync` returns the exact same
 * `AmcContractAdminListItemResponse` shape the list row already carries, so
 * there is no deeper "detail" payload (e.g. visit history isn't exposed to
 * admin) that would justify a full page of its own.
 */
export function AmcContractDetailModal({
  contractId,
  contract,
  isLoading,
  error,
  onClose,
}: {
  /** Which row's detail is open, or null to keep the dialog closed. */
  contractId: string | null;
  contract: AmcContractAdminListItemResponse | undefined;
  isLoading: boolean;
  error?: unknown;
  onClose: () => void;
}) {
  return (
    <Modal
      open={contractId !== null}
      onClose={onClose}
      title={contract ? contract.assetLabel : "Contract detail"}
      description={contract ? `${contract.planName} · ${contract.customerName}` : undefined}
      size="md"
    >
      {isLoading ? (
        <div className="flex flex-col gap-3">
          <Skeleton className="h-4 w-3/4" />
          <Skeleton className="h-4 w-1/2" />
          <Skeleton className="h-4 w-2/3" />
        </div>
      ) : error ? (
        <SectionError error={error} />
      ) : contract ? (
        <div className="flex flex-col gap-4">
          <Badge tone={STATUS_TONES[contract.status] ?? "neutral"}>
            {CONTRACT_STATUS_LABELS[contract.status] ?? String(contract.status)}
          </Badge>

          <DescriptionList
            columns={2}
            items={[
              { label: "Customer", value: contract.customerName },
              { label: "Plan", value: contract.planName },
              { label: "Asset", value: contract.assetLabel },
              {
                label: "Visits",
                value: `${contract.visitsRemaining} of ${contract.visitsIncluded} remaining`,
              },
              { label: "Cover starts", value: formatDate(contract.startDateUtc) },
              { label: "Cover ends", value: formatDate(contract.endDateUtc) },
              { label: "Purchased", value: formatDate(contract.createdAtUtc) },
            ]}
          />
        </div>
      ) : null}
    </Modal>
  );
}
