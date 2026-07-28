"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { RequireAuth } from "@/components/RequireAuth";
import { AddressForm, toUpsertBody } from "@/components/AddressForm";
import type { AddressPayload } from "@/components/AddressForm";
import { Card, PageHeading } from "@/components/ui";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import type { CustomerAddress } from "@/lib/types";

export default function NewAddressPage() {
  return (
    <RequireAuth>
      <NewAddress />
    </RequireAuth>
  );
}

function NewAddress() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const [error, setError] = useState<string | null>(null);

  const mutation = useMutation({
    mutationFn: (values: AddressPayload) =>
      apiFetch<CustomerAddress>(`${API_V1}/addresses`, {
        method: "POST",
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
      <PageHeading
        title="Add an address"
        subtitle="Your first address automatically becomes your default."
      />

      <Card title="Address details">
        <AddressForm
          submitLabel="Save address"
          error={error}
          isSubmitting={mutation.isPending}
          onSubmit={(values) => mutation.mutate(values)}
        />
      </Card>

      <p className="mt-6 text-sm">
        <Link href="/addresses" className="underline">
          Back to address book
        </Link>
      </p>
    </main>
  );
}
