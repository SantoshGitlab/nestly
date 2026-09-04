import { motion } from "motion/react";
import { ServiceCard } from "@/components/ServiceCard";
import { Reveal, revealItem } from "@/components/motion";
import type { ServiceGroupSummary } from "@/lib/types";

/**
 * One service-group section on a category page (Appliance/Service Group
 * catalog redesign): a plain subheading naming the group (e.g. "Repair &
 * gas refill" under "AC"), followed by the same service-card grid used
 * everywhere else. The header only ever renders for a group the caller has
 * already confirmed has at least one service - see the "if Service Group
 * exists" rule this mirrors (SRS 11.5.5): a group with none is never passed
 * here in the first place, so there is no empty-header case to guard against
 * internally.
 */
export function ServiceGroupSection({ group }: { group: ServiceGroupSummary }) {
  return (
    <section aria-labelledby={`service-group-${group.id}-heading`}>
      <h3 id={`service-group-${group.id}-heading`} className="mb-4 text-base font-semibold tracking-tight text-fg">
        {group.name}
      </h3>
      <Reveal className="grid grid-cols-2 gap-5 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5">
        {group.services.map((service) => (
          <motion.div key={service.id} variants={revealItem}>
            <ServiceCard
              slug={service.slug}
              name={service.name}
              price={service.price}
              coverImageUrl={service.coverImageUrl}
            />
          </motion.div>
        ))}
      </Reveal>
    </section>
  );
}
