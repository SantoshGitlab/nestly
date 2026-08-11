import { Skeleton } from "@/components/ui";

/**
 * Loading placeholder for a category tile grid.
 *
 * Shared by the home tiles, the category listing and search results so all
 * three reserve the same shape as the real grid — a skeleton whose dimensions
 * don't match what replaces it causes a layout jump, which is worse than
 * showing nothing at all.
 */
export function CategoryGridSkeleton({ count = 9 }: { count?: number }) {
  return (
    <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3">
      {Array.from({ length: count }, (_, index) => (
        <Skeleton key={index} className="h-72 rounded-2xl" />
      ))}
    </div>
  );
}
