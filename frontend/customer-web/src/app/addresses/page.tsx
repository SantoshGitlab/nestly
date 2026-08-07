"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useState } from "react";
import { Reveal, RevealItem } from "@/components/motion";
import { RequireAuth } from "@/components/RequireAuth";
import {
  Alert,
  Badge,
  Button,
  Card,
  EmptyState,
  Modal,
  PageHeading,
  Skeleton,
} from "@/components/ui";
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
  const [pendingDelete, setPendingDelete] = useState<CustomerAddress | null>(null);

  const query = useQuery({
    queryKey: ["addresses"],
    queryFn: () => apiFetch<CustomerAddress[]>(`${API_V1}/addresses`, { authenticated: true }),
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
      setPendingDelete(null);
      return invalidate();
    },
    onError: (err) => {
      setError(describeError(err));
      setPendingDelete(null);
    },
  });

  return (
    <main className="mx-auto w-full max-w-2xl px-4 py-8 sm:px-6 sm:py-12">
      <PageHeading
        title="Address book"
        subtitle="Where we should send your service professional."
      />

      <div className="mb-6">
        {/* A styled Link, not a Button inside a Link: nesting a <button> in an
            <a> is invalid HTML and gives one action two focusable controls.
            This also keeps the target middle-clickable. */}
        <Link
          href="/addresses/new"
          className="inline-flex h-10 items-center justify-center rounded-lg bg-brand-600 px-4 text-sm font-medium text-fg-on-brand shadow-brand transition duration-fast ease-out hover:bg-brand-700"
        >
          Add address
        </Link>
      </div>

      {error ? (
        <div className="mb-4">
          <Alert tone="error">{error}</Alert>
        </div>
      ) : null}

      {query.isPending ? (
        <ul className="flex flex-col gap-4" aria-hidden>
          {[0, 1].map((row) => (
            <li key={row}>
              <Skeleton className="h-44 rounded-2xl" />
            </li>
          ))}
        </ul>
      ) : query.isError ? (
        <Alert
          tone="error"
          title="Couldn't load your addresses"
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
          title="No addresses yet"
          description="Add your first address and we'll set it as your default automatically."
          action={
            <Link
              href="/addresses/new"
              className="inline-flex h-10 items-center justify-center rounded-lg bg-brand-600 px-4 text-sm font-medium text-fg-on-brand shadow-brand transition duration-fast ease-out hover:bg-brand-700"
            >
              Add address
            </Link>
          }
        />
      ) : (
        <Reveal as="ul" className="flex flex-col gap-4">
          {query.data.map((address) => (
            <RevealItem key={address.id}>
              <Card
                title={address.label}
                actions={address.isDefault ? <Badge tone="brand">Default</Badge> : null}
              >
                <div className="flex flex-col gap-4">
                  <address className="text-sm not-italic leading-relaxed text-fg-muted">
                    {address.line1}
                    {address.line2 ? <>, {address.line2}</> : null}
                    {address.landmark ? <>, near {address.landmark}</> : null}
                    <br />
                    {address.city}, {address.state} <span className="nums">{address.pincode}</span>
                    <br />
                    {address.contactName} ·{" "}
                    <span className="nums">{address.contactMobile}</span>
                  </address>

                  <div className="flex flex-wrap gap-2 border-t border-line pt-4">
                    <Link
                      href={`/addresses/${address.id}/edit`}
                      className="inline-flex h-10 items-center justify-center rounded-lg border border-line bg-surface px-4 text-sm font-medium text-fg shadow-xs transition duration-fast ease-out hover:border-line-strong hover:bg-surface-2"
                    >
                      Edit
                    </Link>
                    {!address.isDefault ? (
                      <Button
                        type="button"
                        variant="secondary"
                        // Keyed to this row's id: `isPending` alone disabled
                        // every row's button and gave no clue which one was
                        // actually in flight.
                        loading={setDefault.isPending && setDefault.variables === address.id}
                        disabled={setDefault.isPending}
                        onClick={() => setDefault.mutate(address.id)}
                      >
                        Set as default
                      </Button>
                    ) : null}
                    <Button
                      type="button"
                      variant="ghost"
                      className="text-danger hover:bg-danger-soft hover:text-danger"
                      onClick={() => setPendingDelete(address)}
                    >
                      Delete
                    </Button>
                  </div>
                </div>
              </Card>
            </RevealItem>
          ))}
        </Reveal>
      )}

      {/* Replaces window.confirm, which ignores the design system, cannot be
          styled or themed, and blocks the whole browser tab. */}
      <Modal
        open={pendingDelete !== null}
        onClose={() => setPendingDelete(null)}
        title="Delete this address?"
        description={
          pendingDelete
            ? `"${pendingDelete.label}" will be removed from your address book.`
            : undefined
        }
        size="sm"
        footer={
          <>
            <Button type="button" variant="secondary" onClick={() => setPendingDelete(null)}>
              Keep address
            </Button>
            <Button
              type="button"
              variant="danger"
              loading={remove.isPending}
              onClick={() => {
                if (pendingDelete) remove.mutate(pendingDelete.id);
              }}
            >
              Yes, delete
            </Button>
          </>
        }
      >
        <p className="text-sm leading-relaxed text-fg-muted">
          This can&apos;t be undone. Bookings already placed at this address are unaffected.
        </p>
      </Modal>
    </main>
  );
}
