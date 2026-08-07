"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { Suspense, useState } from "react";
import { RequireAuth } from "@/components/RequireAuth";
import { AddressForm, toUpsertBody } from "@/components/AddressForm";
import type { AddressPayload } from "@/components/AddressForm";
import { Card, PageHeading } from "@/components/ui";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import { safeRedirectTarget } from "@/lib/auth";
import type { CustomerAddress } from "@/lib/types";

export default function NewAddressPage() {
  // Suspense boundary required around useSearchParams (used below to resume
  // wherever this form was opened from, e.g. mid-booking review, instead of
  // always landing on the standalone address book) - same requirement/
  // pattern as booking/summary/page.tsx and login/page.tsx.
  return (
    <Suspense fallback={null}>
      <RequireAuth>
        <NewAddress />
      </RequireAuth>
    </Suspense>
  );
}

function NewAddress() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const queryClient = useQueryClient();
  const [error, setError] = useState<string | null>(null);

  const returnTo = safeRedirectTarget(searchParams.get("returnTo"));

  const mutation = useMutation({
    mutationFn: (values: AddressPayload) =>
      apiFetch<CustomerAddress>(`${API_V1}/addresses`, {
        method: "POST",
        authenticated: true,
        body: JSON.stringify(toUpsertBody(values)),
      }),
    onSuccess: async (created) => {
      await queryClient.invalidateQueries({ queryKey: ["addresses"] });
      // Resume wherever this form was opened from rather than always
      // dropping the customer on the standalone address book - newAddressId
      // lets that page auto-select what was just created instead of making
      // the customer find and pick it again from the list.
      router.push(
        returnTo
          ? `${returnTo}${returnTo.includes("?") ? "&" : "?"}newAddressId=${created.id}`
          : "/addresses",
      );
    },
    onError: (err) => setError(describeError(err)),
  });

  return (
    <main className="mx-auto w-full max-w-2xl animate-rise px-6 py-12">
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
        <Link href={returnTo ?? "/addresses"} className="underline">
          {returnTo ? "Back to your booking" : "Back to address book"}
        </Link>
      </p>
    </main>
  );
}
