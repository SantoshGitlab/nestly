"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import {
  ScreenSkeleton,
  formatInstantDate,
  inr,
} from "@/components/patterns";
import { Reveal, RevealItem } from "@/components/motion";
import { RequireAuth } from "@/components/RequireAuth";
import {
  Alert,
  Badge,
  Button,
  Card,
  EmptyState,
  LinkButton,
  PageHeading,
} from "@/components/ui";
import type { BadgeTone } from "@/components/ui";
import { describeError } from "@/lib/api";
import { listMyAmcContracts } from "@/lib/amc-api";
import { CustomerAmcContractStatus } from "@/lib/types";
import type { MyAmcContractResponse } from "@/lib/types";

const MY_AMC_CONTRACTS_KEY = ["my-amc-contracts"] as const;

function statusLabel(status: CustomerAmcContractStatus): string {
  switch (status) {
    case CustomerAmcContractStatus.Active:
      return "Active";
    case CustomerAmcContractStatus.Exhausted:
      return "All visits used";
    case CustomerAmcContractStatus.Expired:
      return "Expired";
    case CustomerAmcContractStatus.Cancelled:
      return "Cancelled";
    default:
      return "Unknown";
  }
}

/**
 * `Exhausted` is neutral, not a warning: the docs say it's a success, not a
 * failure ("the customer used every visit they paid for") - it just happens
 * to be a terminal state, same tier as Expired/Cancelled here.
 */
function statusTone(status: CustomerAmcContractStatus): BadgeTone {
  return status === CustomerAmcContractStatus.Active ? "success" : "neutral";
}

/**
 * "My AMC contracts" (docs/AMC.md, Phase 20): one card per contract, since -
 * unlike Subscription, which is account-wide and singular - a customer can
 * hold several AMC contracts at once, one per asset (docs/AMC.md OPEN
 * DECISIONS #3).
 */
export default function AmcContractsPage() {
  return (
    <RequireAuth>
      <AmcContractsScreen />
    </RequireAuth>
  );
}

function AmcContractsScreen() {
  const query = useQuery({
    queryKey: MY_AMC_CONTRACTS_KEY,
    queryFn: listMyAmcContracts,
  });

  return (
    <main className="mx-auto w-full max-w-3xl px-4 py-8 sm:px-6 sm:py-12">
      <PageHeading
        title="My AMC contracts"
        subtitle="Prepaid service cover for your appliances — buy once, redeem visits any time within the term."
        actions={
          <LinkButton href="/amc/new" size="sm">
            Buy an AMC plan
          </LinkButton>
        }
      />

      {query.isPending ? (
        <ScreenSkeleton cards={2} />
      ) : query.isError ? (
        <Alert
          tone="error"
          title="Couldn't load your AMC contracts"
          action={
            <Button size="sm" variant="secondary" onClick={() => query.refetch()}>
              Retry
            </Button>
          }
        >
          {describeError(query.error)}
        </Alert>
      ) : query.data.length === 0 ? (
        <EmptyState
          title="No AMC contracts yet"
          description="Cover a specific appliance for a fixed number of visits over the year — no per-visit charge once you're covered."
          action={<LinkButton href="/amc/new">Browse AMC plans</LinkButton>}
        />
      ) : (
        <Reveal as="ul" className="flex list-none flex-col gap-4">
          {query.data.map((contract) => (
            <RevealItem key={contract.id}>
              <ContractCard contract={contract} />
            </RevealItem>
          ))}
        </Reveal>
      )}
    </main>
  );
}

function ContractCard({ contract }: { contract: MyAmcContractResponse }) {
  return (
    <Card>
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="text-sm font-semibold text-fg">{contract.assetLabel}</p>
          <p className="mt-0.5 text-xs text-fg-muted">
            {contract.planName} · {contract.categoryName}
          </p>
        </div>
        <Badge tone={statusTone(contract.status)}>{statusLabel(contract.status)}</Badge>
      </div>

      <div className="mt-4 grid grid-cols-2 gap-4 border-t border-line pt-4 sm:grid-cols-4">
        <Stat label="Visits left" value={`${contract.visitsRemaining} / ${contract.visitsIncluded}`} />
        <Stat label="Term ends" value={formatInstantDate(contract.endDateUtc)} />
        <Stat label="Paid" value={inr(contract.price)} />
        <Stat label="Term" value={`${contract.termMonths} mo`} />
      </div>

      <div className="mt-4 flex flex-wrap items-center gap-2 border-t border-line pt-4">
        <Link
          href={`/amc/${contract.id}`}
          className="text-sm font-medium text-brand-600 underline-offset-4 hover:underline dark:text-brand-400"
        >
          View details
        </Link>
        {contract.canRedeemNow ? (
          <LinkButton href={`/amc/${contract.id}/redeem`} size="sm" className="ml-auto">
            Redeem a visit
          </LinkButton>
        ) : null}
      </div>
    </Card>
  );
}

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <div className="min-w-0">
      <p className="text-[0.6875rem] font-medium uppercase tracking-wide text-fg-subtle">{label}</p>
      <p className="nums mt-0.5 truncate text-sm font-medium text-fg">{value}</p>
    </div>
  );
}
