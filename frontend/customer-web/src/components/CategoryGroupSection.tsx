import { SubcategoryTileGrid } from "@/components/SubcategoryTileGrid";
import type { CategoryGroupSummary } from "@/lib/types";

/**
 * One subcategory-group section on a category page: a plain subheading
 * naming the group (e.g. "Large appliances" under "AC & Appliance Repair"),
 * followed by the same subcategory image-tile grid used for the ungrouped
 * case. Mirrors `ServiceGroupSection` one taxonomy level up. The header only
 * ever renders for a group the caller has already confirmed has at least one
 * subcategory - there is no empty-header case to guard against internally.
 */
export function CategoryGroupSection({ group }: { group: CategoryGroupSummary }) {
  return (
    <section aria-labelledby={`category-group-${group.id}-heading`}>
      <h3 id={`category-group-${group.id}-heading`} className="mb-3 text-base font-semibold tracking-tight text-fg">
        {group.name}
      </h3>
      <SubcategoryTileGrid subcategories={group.subcategories} />
    </section>
  );
}
