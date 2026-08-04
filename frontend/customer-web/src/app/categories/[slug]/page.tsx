"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useParams } from "next/navigation";
import { ServiceCard } from "@/components/ServiceCard";
import { Alert, Button, EmptyState, Skeleton } from "@/components/ui";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import type { CategoryDetail } from "@/lib/types";

/** Category detail page (SRS 11.5.2): banner, description, and its service listing. */
export default function CategoryDetailPage() {
  const { slug } = useParams<{ slug: string }>();

  const query = useQuery({
    queryKey: ["category", slug],
    queryFn: () => apiFetch<CategoryDetail>(`${API_V1}/categories/${slug}`),
  });

  if (query.isPending) {
    return <CategoryDetailSkeleton />;
  }

  if (query.isError) {
    return (
      <main className="mx-auto w-full max-w-6xl px-4 py-8 sm:px-6 sm:py-12">
        <Alert
          tone="error"
          title="Couldn't load this category"
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

  const category = query.data;

  return (
    <main className="mx-auto w-full max-w-6xl px-4 py-8 sm:px-6 sm:py-12">
      <nav aria-label="Breadcrumb" className="mb-5 text-sm">
        <ol className="flex items-center gap-1.5 text-fg-muted">
          <li>
            <Link href="/" className="hover:text-fg">
              Home
            </Link>
          </li>
          <li aria-hidden>/</li>
          <li>
            <Link href="/categories" className="hover:text-fg">
              Categories
            </Link>
          </li>
          <li aria-hidden>/</li>
          <li className="truncate font-medium text-fg" aria-current="page">
            {category.name}
          </li>
        </ol>
      </nav>

      {category.bannerUrl ? (
        // eslint-disable-next-line @next/next/no-img-element -- admin-supplied external URL, unsuited to static optimization.
        <img
          src={category.bannerUrl}
          alt=""
          className="mb-8 h-44 w-full rounded-3xl object-cover shadow-sm sm:h-64"
        />
      ) : null}

      <h1 className="text-display-sm font-semibold text-fg">{category.name}</h1>
      {category.description ? (
        <p className="mt-3 max-w-2xl leading-relaxed text-fg-muted text-pretty">
          {category.description}
        </p>
      ) : null}

      <section aria-labelledby="services-heading" className="mt-10">
        <h2 id="services-heading" className="mb-5 text-lg font-semibold tracking-tight text-fg">
          Services
          <span className="ml-2 text-sm font-normal text-fg-subtle">
            {category.services.length}
          </span>
        </h2>

        {category.services.length === 0 ? (
          <EmptyState
            title="Nothing listed yet"
            description="No services are listed under this category in your city yet — check back soon."
            action={
              <Link
                href="/categories"
                className="inline-flex h-10 items-center rounded-lg border border-line bg-surface px-4 text-sm font-medium text-fg shadow-xs transition duration-fast ease-out hover:border-line-strong hover:bg-surface-2"
              >
                Browse other categories
              </Link>
            }
          />
        ) : (
          <div className="grid animate-fade-in grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {category.services.map((service) => (
              <ServiceCard
                key={service.id}
                slug={service.slug}
                name={service.name}
                description={service.description}
                price={service.price}
                addOnCount={service.addOns.length}
              />
            ))}
          </div>
        )}
      </section>
    </main>
  );
}

/** Mirrors the loaded page's frame so the heading and grid don't jump into place. */
function CategoryDetailSkeleton() {
  return (
    <main className="mx-auto w-full max-w-6xl px-4 py-8 sm:px-6 sm:py-12">
      <Skeleton className="h-4 w-56" />
      <Skeleton className="mt-5 h-44 w-full rounded-3xl sm:h-64" />
      <Skeleton className="mt-8 h-9 w-72" />
      <Skeleton className="mt-3 h-4 w-full max-w-2xl" />
      <Skeleton className="mt-2 h-4 w-2/3 max-w-2xl" />
      <Skeleton className="mt-10 h-6 w-28" />
      <div className="mt-5 grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {Array.from({ length: 6 }, (_, index) => (
          <Skeleton key={index} className="h-[10.5rem] rounded-2xl" />
        ))}
      </div>
    </main>
  );
}
