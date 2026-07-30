"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useParams } from "next/navigation";
import { PriceCalculator } from "@/components/PriceCalculator";
import { ServiceAvailability } from "@/components/ServiceAvailability";
import { Alert, Button } from "@/components/ui";
import { useSelectedCity } from "@/hooks/useSelectedCity";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import type { ServiceDetail } from "@/lib/types";

/**
 * Service detail page (SRS 11.6.1): inclusions, exclusions, add-ons, pricing,
 * and cancellation/reschedule policy.
 *
 * Out of scope, deliberately: FAQs and a reviews/rating summary, both listed
 * in SRS 11.6.1 - there is no FAQ or Review/Testimonial entity or API
 * anywhere in the backend yet, so this does not fabricate one.
 */
export default function ServiceDetailPage() {
  const { slug } = useParams<{ slug: string }>();
  const { city } = useSelectedCity();

  const query = useQuery({
    queryKey: ["service", slug],
    queryFn: () => apiFetch<ServiceDetail>(`${API_V1}/services/${slug}`),
  });

  if (query.isPending) {
    return <main className="mx-auto w-full max-w-4xl px-6 py-10 text-sm text-neutral-500">Loading…</main>;
  }

  if (query.isError) {
    return (
      <main className="mx-auto w-full max-w-4xl px-6 py-10">
        <Alert>{describeError(query.error)}</Alert>
      </main>
    );
  }

  const service = query.data;

  return (
    <main className="mx-auto grid w-full max-w-4xl gap-8 px-6 py-10 md:grid-cols-[1fr_320px]">
      <div className="flex flex-col gap-6">
        <div>
          <Link
            href={`/categories/${service.categorySlug}`}
            className="text-sm text-neutral-500 hover:underline"
          >
            {service.categoryName}
          </Link>
          <h1 className="mt-1 text-2xl font-semibold tracking-tight">{service.name}</h1>
          <p className="mt-2 text-neutral-600 dark:text-neutral-400">{service.description}</p>
        </div>

        <section aria-labelledby="inclusions-heading">
          <h2 id="inclusions-heading" className="mb-2 text-sm font-semibold uppercase tracking-wide text-neutral-500">
            What&apos;s included
          </h2>
          <p className="text-sm">{service.inclusions}</p>
        </section>

        <section aria-labelledby="exclusions-heading">
          <h2 id="exclusions-heading" className="mb-2 text-sm font-semibold uppercase tracking-wide text-neutral-500">
            What&apos;s not included
          </h2>
          <p className="text-sm">{service.exclusions}</p>
        </section>

        {service.cancellationPolicy || service.reschedulePolicy ? (
          <section aria-labelledby="policies-heading">
            <h2 id="policies-heading" className="mb-2 text-sm font-semibold uppercase tracking-wide text-neutral-500">
              Cancellation &amp; rescheduling
            </h2>
            <ul className="flex flex-col gap-1 text-sm text-neutral-600 dark:text-neutral-400">
              {service.cancellationPolicy ? <li>{service.cancellationPolicy}</li> : null}
              {service.reschedulePolicy ? <li>{service.reschedulePolicy}</li> : null}
            </ul>
          </section>
        ) : null}
      </div>

      <aside className="flex flex-col gap-4 md:sticky md:top-6 md:self-start">
        <PriceCalculator serviceId={service.id} addOns={service.addOns} cityId={city ? city.id : null} />
        <ServiceAvailability serviceId={service.id} />
        <Link href={`/booking/summary?serviceSlug=${service.slug}`}>
          <Button type="button" className="w-full">
            Book now
          </Button>
        </Link>
      </aside>
    </main>
  );
}
