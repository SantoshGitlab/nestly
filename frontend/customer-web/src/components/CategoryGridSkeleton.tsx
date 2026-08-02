import { Skeleton } from "@/components/ui";

/**
 * Loading placeholder for a category tile grid.
 *
 * Shared by the home tiles, the category listing and search results so all
 * three reserve the same shape as the real grid — a skeleton whose dimensions
 * don't match what replaces it causes a layout jump, which is worse than
 * showing nothing at all.
 */
export function CategoryGridSkeleton({ count = 8 }: { count?: number }) {
  return (
    <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 md:grid-cols-4">
      {Array.from({ length: count }, (_, index) => (
        <Skeleton key={index} className="h-[9.5rem] rounded-2xl" />
      ))}
    </div>
  );
}
