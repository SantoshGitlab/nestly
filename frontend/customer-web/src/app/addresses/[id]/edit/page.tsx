"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { useState } from "react";
import { RequireAuth } from "@/components/RequireAuth";
import { AddressForm, toUpsertBody } from "@/components/AddressForm";
import type { AddressPayload } from "@/components/AddressForm";
import { Alert, Card, PageHeading } from "@/components/ui";
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
    <main className="mx-auto w-full max-w-2xl px-6 py-12">
      <PageHeading title="Edit address" />

      {query.isPending ? (
        <p className="text-sm text-fg-muted">Loading address…</p>
      ) : query.isError ? (
        <Alert>{describeError(query.error)}</Alert>
      ) : !address ? (
        <Alert>That address no longer exists.</Alert>
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
