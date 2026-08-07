"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useParams } from "next/navigation";
import { PriceCalculator } from "@/components/PriceCalculator";
import { ReviewsSummary } from "@/components/ReviewsSummary";
import { ServiceAvailability } from "@/components/ServiceAvailability";
import { ServiceFaqs } from "@/components/ServiceFaqs";
import { Alert, Button, Skeleton } from "@/components/ui";
import { useSelectedCity } from "@/hooks/useSelectedCity";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import type { ServiceDetail } from "@/lib/types";

/**
 * Service detail page (SRS 11.6.1): inclusions, exclusions, add-ons, pricing,
 * FAQs, cancellation/reschedule policy, and a reviews/rating summary.
 */
export default function ServiceDetailPage() {
  const { slug } = useParams<{ slug: string }>();
  const { city } = useSelectedCity();

  const query = useQuery({
    queryKey: ["service", slug],
    queryFn: () => apiFetch<ServiceDetail>(`${API_V1}/services/${slug}`),
  });

  if (query.isPending) {
    return <ServiceDetailSkeleton />;
  }

  if (query.isError) {
    return (
      <main className="mx-auto w-full max-w-5xl px-4 py-8 sm:px-6 sm:py-12">
        <Alert
          tone="error"
          title="Couldn't load this service"
          action={
            <Button size="sm" variant="secondary" onClick={() => query.refetch()}>
              Retry
            </Button>
          }
        >
          {describeError(query.error)}
        </Alert>
      </main>
    );
  }

  const service = query.data;

  return (
    <main className="mx-auto w-full max-w-5xl animate-rise px-4 py-8 sm:px-6 sm:py-12">
      <nav aria-label="Breadcrumb" className="mb-5 text-sm">
        <ol className="flex flex-wrap items-center gap-1.5 text-fg-muted">
          <li>
            <Link href="/categories" className="hover:text-fg">
              Categories
            </Link>
          </li>
          <li aria-hidden>/</li>
          <li>
            <Link href={`/categories/${service.categorySlug}`} className="hover:text-fg">
              {service.categoryName}
            </Link>
          </li>
          <li aria-hidden>/</li>
          <li className="font-medium text-fg" aria-current="page">
            {service.name}
          </li>
        </ol>
      </nav>

      <div className="grid gap-8 md:grid-cols-[1fr_20rem]">
        <div className="flex min-w-0 flex-col gap-8">
          <div>
            <h1 className="text-display-sm font-semibold text-fg">{service.name}</h1>
            <p className="mt-3 leading-relaxed text-fg-muted text-pretty">{service.description}</p>
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <InclusionList
              headingId="inclusions-heading"
              title="What's included"
              body={service.inclusions}
              tone="included"
            />
            <InclusionList
              headingId="exclusions-heading"
              title="What's not included"
              body={service.exclusions}
              tone="excluded"
            />
          </div>

          {service.cancellationPolicy || service.reschedulePolicy ? (
            <section aria-labelledby="policies-heading">
              <h2
                id="policies-heading"
                className="mb-3 text-lg font-semibold tracking-tight text-fg"
              >
                Cancellation &amp; rescheduling
              </h2>
              <ul className="flex flex-col gap-2 rounded-2xl border border-line bg-surface p-4 text-sm leading-relaxed text-fg-muted">
                {service.cancellationPolicy ? <li>{service.cancellationPolicy}</li> : null}
                {service.reschedulePolicy ? <li>{service.reschedulePolicy}</li> : null}
              </ul>
            </section>
          ) : null}

          <ServiceFaqs faqs={service.faqs} />

          <ReviewsSummary slug={service.slug} />
        </div>

        <aside className="flex flex-col gap-4 md:sticky md:top-20 md:self-start">
          <PriceCalculator
            serviceId={service.id}
            addOns={service.addOns}
            cityId={city ? city.id : null}
          />
          <ServiceAvailability serviceId={service.id} />

          {/* A styled Link, not <Link><Button/></Link>: nesting a button
              inside an anchor is invalid HTML and gives assistive tech two
              nested interactive elements for one action. */}
          <Link
            href={`/booking/summary?serviceSlug=${service.slug}`}
            className="inline-flex h-12 w-full items-center justify-center rounded-lg bg-brand-600 text-[0.9375rem] font-medium text-fg-on-brand shadow-brand transition duration-fast ease-out hover:bg-brand-700 active:scale-[0.98]"
          >
            Book now
          </Link>
        </aside>
      </div>
    </main>
  );
}

function InclusionList({
  headingId,
  title,
  body,
  tone,
}: {
  headingId: string;
  title: string;
  body: string;
  tone: "included" | "excluded";
}) {
  if (!body) return null;

  return (
    <section aria-labelledby={headingId} className="rounded-2xl border border-line bg-surface p-4">
      <h2 id={headingId} className="flex items-center gap-2 text-sm font-semibold text-fg">
        {tone === "included" ? (
          <svg
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2.25"
            strokeLinecap="round"
            strokeLinejoin="round"
            className="h-4 w-4 text-success"
            aria-hidden
          >
            <path d="m5 13 4 4L19 7" />
          </svg>
        ) : (
          <svg
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2.25"
            strokeLinecap="round"
            className="h-4 w-4 text-fg-subtle"
            aria-hidden
          >
            <path d="M18 6 6 18M6 6l12 12" />
          </svg>
        )}
        {title}
      </h2>
      <p className="mt-2 text-sm leading-relaxed text-fg-muted">{body}</p>
    </section>
  );
}

/** Mirrors the loaded layout so the two columns don't jump into place. */
function ServiceDetailSkeleton() {
  return (
    <main className="mx-auto w-full max-w-5xl px-4 py-8 sm:px-6 sm:py-12">
      <Skeleton className="h-4 w-72" />
      <div className="mt-5 grid gap-8 md:grid-cols-[1fr_20rem]">
        <div className="flex flex-col gap-6">
          <Skeleton className="h-9 w-3/4" />
          <div className="flex flex-col gap-2">
            <Skeleton className="h-4 w-full" />
            <Skeleton className="h-4 w-5/6" />
          </div>
          <div className="grid gap-4 sm:grid-cols-2">
            <Skeleton className="h-28 rounded-2xl" />
            <Skeleton className="h-28 rounded-2xl" />
          </div>
          <Skeleton className="h-40 rounded-2xl" />
        </div>
        <div className="flex flex-col gap-4">
          <Skeleton className="h-56 rounded-2xl" />
          <Skeleton className="h-40 rounded-2xl" />
          <Skeleton className="h-12 rounded-lg" />
        </div>
      </div>
    </main>
  );
}
