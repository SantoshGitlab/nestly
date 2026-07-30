"use client";

import { useQuery } from "@tanstack/react-query";
import { useParams } from "next/navigation";
import { ServiceCard } from "@/components/ServiceCard";
import { Alert } from "@/components/ui";
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
    return <main className="mx-auto w-full max-w-6xl px-6 py-10 text-sm text-neutral-500">Loading…</main>;
  }

  if (query.isError) {
    return (
      <main className="mx-auto w-full max-w-6xl px-6 py-10">
        <Alert>{describeError(query.error)}</Alert>
      </main>
    );
  }

  const category = query.data;

  return (
    <main className="mx-auto w-full max-w-6xl px-6 py-10">
      {category.bannerUrl ? (
        // eslint-disable-next-line @next/next/no-img-element -- admin-supplied external URL, unsuited to static optimization.
        <img
          src={category.bannerUrl}
          alt=""
          className="mb-6 h-40 w-full rounded-2xl object-cover sm:h-56"
        />
      ) : null}

      <h1 className="text-2xl font-semibold tracking-tight">{category.name}</h1>
      <p className="mt-2 max-w-2xl text-neutral-600 dark:text-neutral-400">{category.description}</p>

      <section aria-labelledby="services-heading" className="mt-8">
        <h2 id="services-heading" className="mb-4 text-lg font-semibold">
          Services
        </h2>

        {category.services.length === 0 ? (
          <p className="text-sm text-neutral-500">No services are listed under this category yet.</p>
        ) : (
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
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
