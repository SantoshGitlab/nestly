"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { Suspense, useState } from "react";
import { RequireAuth } from "@/components/RequireAuth";
import { AddressForm, AddressHelpCard, toUpsertBody } from "@/components/AddressForm";
import type { AddressPayload } from "@/components/AddressForm";
import { BannerBreadcrumb, STICKY_BAR_SPACER, ScreenSkeleton } from "@/components/patterns";
import { PageBanner } from "@/components/PageBanner";
import { Card, cx } from "@/components/ui";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import { safeRedirectTarget } from "@/lib/auth";
import type { CustomerAddress } from "@/lib/types";

export default function NewAddressPage() {
  // Suspense boundary required around useSearchParams (used below to resume
  // wherever this form was opened from, e.g. mid-booking review, instead of
  // always landing on the standalone address book) - same requirement/
  // pattern as booking/summary/page.tsx and login/page.tsx. Sized fallback
  // (not `null`) so the form doesn't flash in blank, matching support/new's
  // equivalent boundary.
  return (
    <Suspense
      fallback={
        <main className="flex w-full flex-col">
          <div className="listing-banner h-[13.5rem] w-full sm:h-[15.5rem]" aria-hidden />
          <ScreenSkeleton cards={1} className="mx-auto w-full max-w-7xl px-4 py-10 sm:px-6 sm:py-14" />
        </main>
      }
    >
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
    <main className="flex w-full flex-col animate-rise">
      <PageBanner
        title="Add an address"
        description="Your first address automatically becomes your default."
        breadcrumb={
          <BannerBreadcrumb
            items={[{ label: "Home", href: "/" }, { label: "Address book", href: "/addresses" }, { label: "Add an address" }]}
          />
        }
      />

      <div className={cx("mx-auto w-full max-w-7xl px-4 py-10 sm:px-6 sm:py-14", STICKY_BAR_SPACER)}>
        <div className="grid grid-cols-1 gap-6 lg:grid-cols-12">
          {/* 8 of 12: the form itself. Its own column - not the full max-w-7xl
              width - is what stops the short fields reading as unreasonably
              long single-line boxes (same fix the profile page uses). */}
          <div className="flex flex-col gap-6 lg:col-span-8">
            <Card title="Address details">
              <AddressForm
                submitLabel="Save address"
                error={error}
                isSubmitting={mutation.isPending}
                onSubmit={(values) => mutation.mutate(values)}
              />
            </Card>

            <p className="text-sm">
              <Link href={returnTo ?? "/addresses"} className="underline">
                {returnTo ? "Back to your booking" : "Back to address book"}
              </Link>
            </p>
          </div>

          {/* 4 of 12: reassurance and tips, sticky beside a form this tall. */}
          <aside className="lg:col-span-4 lg:sticky lg:top-20 lg:self-start">
            <AddressHelpCard />
          </aside>
        </div>
      </div>
    </main>
  );
}
