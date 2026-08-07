"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { useState } from "react";
import { RequireAuth } from "@/components/RequireAuth";
import { AddressForm, toUpsertBody } from "@/components/AddressForm";
import type { AddressPayload } from "@/components/AddressForm";
import { Alert, Button, Card, EmptyState, PageHeading, Skeleton } from "@/components/ui";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import type { CustomerAddress } from "@/lib/types";

export default function EditAddressPage() {
  return (
    <RequireAuth>
      <EditAddress />
    </RequireAuth>
  );
}

function EditAddress() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const params = useParams<{ id: string }>();
  const id = params.id;
  const [error, setError] = useState<string | null>(null);

  // The API has no "get one address" endpoint (SRS 24.2 lists only the
  // collection), so the address is picked out of the list the book already
  // loads. Fetching the list here keeps the page usable on a direct link.
  const query = useQuery({
    queryKey: ["addresses"],
    queryFn: () =>
      apiFetch<CustomerAddress[]>(`${API_V1}/addresses`, { authenticated: true }),
  });

  const address = query.data?.find((candidate) => candidate.id === id);

  const mutation = useMutation({
    mutationFn: (values: AddressPayload) =>
      apiFetch<CustomerAddress>(`${API_V1}/addresses/${id}`, {
        method: "PUT",
        authenticated: true,
        body: JSON.stringify(toUpsertBody(values)),
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["addresses"] });
      router.push("/addresses");
    },
    onError: (err) => setError(describeError(err)),
  });

  return (
    <main className="mx-auto w-full max-w-2xl animate-rise px-6 py-12">
      <PageHeading title="Edit address" />

      {query.isPending ? (
        <AddressFormSkeleton />
      ) : query.isError ? (
        <Alert
          tone="error"
          title="Couldn't load this address"
          action={
            <Button
              size="sm"
              variant="secondary"
              loading={query.isRefetching}
              onClick={() => query.refetch()}
            >
              Retry
            </Button>
          }
        >
          {describeError(query.error)}
        </Alert>
      ) : !address ? (
        // Not an error — the address was deleted, most likely in another tab.
        // The only useful move from here is back to the book, so offer it as
        // the action rather than leaving the footer link as the sole way out.
        <EmptyState
          title="That address no longer exists"
          description="It may have been deleted from your address book on another device."
          action={
            <Link
              href="/addresses"
              className="inline-flex h-10 items-center justify-center rounded-lg bg-brand-600 px-4 text-sm font-medium text-fg-on-brand shadow-brand transition duration-fast ease-out hover:bg-brand-700"
            >
              Back to address book
            </Link>
          }
        />
      ) : (
        <Card title="Address details">
          <AddressForm
            initial={address}
            submitLabel="Save changes"
            error={error}
            isSubmitting={mutation.isPending}
            onSubmit={(values) => mutation.mutate(values)}
          />
        </Card>
      )}

      <p className="mt-6 text-sm">
        <Link href="/addresses" className="underline">
          Back to address book
        </Link>
      </p>
    </main>
  );
}

/**
 * Mirrors AddressForm inside its Card: nine stacked fields, the lat/long pair
 * on one row, then the checkbox and submit button. Sized to the real controls
 * (20px label + 6px gap + 38px input) so the form does not jump into place
 * when the address arrives.
 */
function AddressFormSkeleton() {
  return (
    <Card title="Address details">
      <div className="flex flex-col gap-4" aria-hidden>
        {Array.from({ length: 9 }, (_, index) => (
          <FieldSkeleton key={index} />
        ))}
        <div className="grid grid-cols-2 gap-4">
          <FieldSkeleton />
          <FieldSkeleton />
        </div>
        <div className="flex items-center gap-2.5">
          <Skeleton className="h-4 w-4 rounded" />
          <Skeleton className="h-3.5 w-48" />
        </div>
        <Skeleton className="h-10 w-full rounded-lg" />
      </div>
    </Card>
  );
}

function FieldSkeleton() {
  return (
    <div className="flex flex-col gap-1.5">
      <div className="flex h-5 items-center">
        <Skeleton className="h-3.5 w-28" />
      </div>
      <Skeleton className="h-[38px] w-full" />
    </div>
  );
}
