"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useState } from "react";
import { RequireAuth } from "@/components/RequireAuth";
import { Alert, Button, Card, PageHeading } from "@/components/ui";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import type { CustomerAddress } from "@/lib/types";

export default function AddressesPage() {
  return (
    <RequireAuth>
      <AddressBook />
    </RequireAuth>
  );
}

function AddressBook() {
  const queryClient = useQueryClient();
  const [error, setError] = useState<string | null>(null);

  const query = useQuery({
    queryKey: ["addresses"],
    queryFn: () =>
      apiFetch<CustomerAddress[]>(`${API_V1}/addresses`, { authenticated: true }),
  });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ["addresses"] });

  const setDefault = useMutation({
    mutationFn: (id: string) =>
      apiFetch<void>(`${API_V1}/addresses/${id}/default`, {
        method: "POST",
        authenticated: true,
      }),
    onSuccess: () => {
      setError(null);
      return invalidate();
    },
    onError: (err) => setError(describeError(err)),
  });

  const remove = useMutation({
    mutationFn: (id: string) =>
      apiFetch<void>(`${API_V1}/addresses/${id}`, {
        method: "DELETE",
        authenticated: true,
      }),
    onSuccess: () => {
      setError(null);
      return invalidate();
    },
    onError: (err) => setError(describeError(err)),
  });

  return (
    <main className="mx-auto w-full max-w-2xl px-6 py-12">
      <PageHeading title="Address book" subtitle="Where we should send your service professional." />

      <div className="mb-6">
        <Link href="/addresses/new">
          <Button type="button">Add address</Button>
        </Link>
      </div>

      {error ? (
        <div className="mb-4">
          <Alert>{error}</Alert>
        </div>
      ) : null}

      {query.isPending ? (
        <p className="text-sm text-neutral-500">Loading addresses…</p>
      ) : query.isError ? (
        <Alert>{describeError(query.error)}</Alert>
      ) : query.data.length === 0 ? (
        <Card title="No addresses yet">
          <p className="text-sm text-neutral-600 dark:text-neutral-400">
            Add your first address and we&apos;ll set it as your default automatically.
          </p>
        </Card>
      ) : (
        <ul className="flex flex-col gap-4">
          {query.data.map((address) => (
            <li key={address.id}>
              <Card title={address.label}>
                <div className="flex flex-col gap-4">
                  {address.isDefault ? (
                    <span className="w-fit rounded-full bg-black px-2.5 py-0.5 text-xs font-medium text-white dark:bg-white dark:text-black">
                      Default
                    </span>
                  ) : null}

                  <address className="text-sm not-italic leading-relaxed text-neutral-700 dark:text-neutral-300">
                    {address.line1}
                    {address.line2 ? <>, {address.line2}</> : null}
                    {address.landmark ? <>, near {address.landmark}</> : null}
                    <br />
                    {address.city}, {address.state} {address.pincode}
                    <br />
                    {address.contactName} · {address.contactMobile}
                  </address>

                  <div className="flex flex-wrap gap-2">
                    <Link href={`/addresses/${address.id}/edit`}>
                      <Button type="button" variant="secondary">
                        Edit
                      </Button>
                    </Link>
                    {!address.isDefault ? (
                      <Button
                        type="button"
                        variant="secondary"
                        disabled={setDefault.isPending}
                        onClick={() => setDefault.mutate(address.id)}
                      >
                        Set as default
                      </Button>
                    ) : null}
                    <Button
                      type="button"
                      variant="danger"
                      disabled={remove.isPending}
                      onClick={() => {
                        if (window.confirm(`Delete the "${address.label}" address?`)) {
                          remove.mutate(address.id);
                        }
                      }}
                    >
                      Delete
                    </Button>
                  </div>
                </div>
              </Card>
            </li>
          ))}
        </ul>
      )}
    </main>
  );
}
