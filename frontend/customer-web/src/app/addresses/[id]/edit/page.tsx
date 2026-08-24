"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { useState } from "react";
import type { ReactNode } from "react";
import { RequireAuth } from "@/components/RequireAuth";
import { AddressForm, AddressHelpCard, toUpsertBody } from "@/components/AddressForm";
import type { AddressPayload } from "@/components/AddressForm";
import { BannerBreadcrumb, STICKY_BAR_SPACER } from "@/components/patterns";
import { PageBanner } from "@/components/PageBanner";
import { Alert, Button, Card, EmptyState, LinkButton, Skeleton, cx } from "@/components/ui";
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
    <main className="flex w-full flex-col animate-rise">
      <PageBanner
        title="Edit address"
        breadcrumb={
          <BannerBreadcrumb
            items={[{ label: "Home", href: "/" }, { label: "Address book", href: "/addresses" }, { label: "Edit address" }]}
          />
        }
      />

      <div
        className={cx(
          "mx-auto w-full max-w-7xl px-4 py-10 sm:px-6 sm:py-14",
          // AddressForm (below) renders its submit inside a StickyActionBar
          // whenever it's actually reached - the skeleton/error/empty branches
          // above it never mount that bar, but the constant spacer here is
          // harmless padding on those, same trade-off STICKY_BAR_SPACER's own
          // doc comment describes.
          STICKY_BAR_SPACER,
        )}
      >
        <div className="grid grid-cols-1 gap-6 lg:grid-cols-12">
          {/* 8 of 12: the form (or its loading/error/empty stand-ins). Its own
              column - not the full max-w-7xl width - keeps the short fields
              from reading as unreasonably long boxes, matching addresses/new. */}
          <div className="flex flex-col gap-6 lg:col-span-8">
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
              // Not an error — the address was deleted, most likely in another
              // tab. The only useful move from here is back to the book, so
              // offer it as the action rather than leaving the footer link as
              // the sole way out.
              <EmptyState
                title="That address no longer exists"
                description="It may have been deleted from your address book on another device."
                action={<LinkButton href="/addresses">Back to address book</LinkButton>}
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

            <p className="text-sm">
              <Link href="/addresses" className="underline">
                Back to address book
              </Link>
            </p>
          </div>

          {/* 4 of 12: the same reassurance panel the add screen shows. */}
          <aside className="lg:col-span-4 lg:sticky lg:top-20 lg:self-start">
            <AddressHelpCard />
          </aside>
        </div>
      </div>
    </main>
  );
}

/**
 * Mirrors AddressForm inside its Card: three titled sections (Address, Location
 * pin, Contact) with the same field pairing, so the form does not jump into
 * place when the address arrives. Sized to the real controls (20px label + 6px
 * gap + 38px input).
 */
function AddressFormSkeleton() {
  return (
    <Card title="Address details">
      <div className="flex flex-col gap-7" aria-hidden>
        <SectionSkeleton>
          <FieldSkeleton />
          <FieldSkeleton />
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <FieldSkeleton />
            <FieldSkeleton />
          </div>
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
            <FieldSkeleton />
            <FieldSkeleton />
            <FieldSkeleton />
          </div>
        </SectionSkeleton>

        <SectionSkeleton>
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <FieldSkeleton />
            <FieldSkeleton />
          </div>
        </SectionSkeleton>

        <SectionSkeleton>
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <FieldSkeleton />
            <FieldSkeleton />
          </div>
          <div className="flex items-center gap-2.5">
            <Skeleton className="h-4 w-4 rounded" />
            <Skeleton className="h-3.5 w-48" />
          </div>
        </SectionSkeleton>

        <Skeleton className="h-10 w-full rounded-lg" />
      </div>
    </Card>
  );
}

function SectionSkeleton({ children }: { children: ReactNode }) {
  return (
    <div className="flex flex-col gap-4">
      <div className="border-b border-line pb-2">
        <Skeleton className="h-3.5 w-40" />
      </div>
      {children}
    </div>
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
