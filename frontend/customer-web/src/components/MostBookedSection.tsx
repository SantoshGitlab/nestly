import { motion } from "motion/react";
import { Reveal, revealItem } from "@/components/motion";
import { ServiceCard } from "@/components/ServiceCard";
import type { LandingService } from "@/lib/landing-types";

/**
 * "Most Booked Services": admin-picked bookable services, same card grid as
 * `ServiceGroupSection` (a category page's group listing) so a service reads
 * identically everywhere it appears.
 */
export function MostBookedSection({ services }: { services: LandingService[] }) {
  if (services.length === 0) {
    return null;
  }

  return (
    <section aria-labelledby="most-booked-heading" className="flex flex-col gap-6">
      <h2 id="most-booked-heading" className="text-display-sm font-bold tracking-tight text-fg">
        Most Booked Services
      </h2>
      <Reveal className="grid grid-cols-2 gap-5 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5">
        {services.map((service) => (
          <motion.div key={service.id} variants={revealItem}>
            <ServiceCard
              slug={service.slug}
              name={service.name}
              price={service.price}
              coverImageUrl={service.imageUrl}
            />
          </motion.div>
        ))}
      </Reveal>
    </section>
  );
}
