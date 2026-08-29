import { motion } from "motion/react";
import Link from "next/link";
import { Reveal, revealItem } from "@/components/motion";
import { ServiceCard } from "@/components/ServiceCard";
import type { LandingCategorySection } from "@/lib/landing-types";

/**
 * The category-wise strips below "Most Booked Services": one section per
 * admin-configured category, headed by that category's name, showing its
 * (at most 5) picked services in the same card grid as everywhere else.
 * Mirrors `ServiceGroupSection`'s shape one level up - a heading plus a
 * service-card `Reveal` grid - but the heading here links to the category
 * page rather than being plain text, since it names a real destination.
 */
export function CategoryLandingSections({ sections }: { sections: LandingCategorySection[] }) {
  if (sections.length === 0) {
    return null;
  }

  return (
    <div className="flex flex-col gap-14">
      {sections.map((section) => (
        <section key={section.categoryId} aria-labelledby={`category-section-${section.categoryId}-heading`} className="flex flex-col gap-6">
          <div className="flex items-baseline justify-between gap-3">
            <h2 id={`category-section-${section.categoryId}-heading`} className="text-xl font-semibold tracking-tight text-fg">
              {section.categoryName}
            </h2>
            <Link
              href={`/categories/${section.categorySlug}`}
              className="shrink-0 text-sm font-medium text-brand-600 underline-offset-4 hover:underline dark:text-brand-400"
            >
              View all
            </Link>
          </div>

          <Reveal className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
            {section.services.map((service) => (
              <motion.div key={service.id} variants={revealItem}>
                <ServiceCard
                  slug={service.slug}
                  name={service.name}
                  description=""
                  price={service.price}
                  coverImageUrl={service.imageUrl}
                />
              </motion.div>
            ))}
          </Reveal>
        </section>
      ))}
    </div>
  );
}
