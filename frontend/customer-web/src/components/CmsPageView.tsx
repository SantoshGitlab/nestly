"use client";

import { useQuery } from "@tanstack/react-query";
import { Alert, Button, EmptyState, Skeleton } from "@/components/ui";
import { ApiError, describeError } from "@/lib/api";
import { getCmsPage } from "@/lib/cms-api";

/**
 * Renders one admin-authored static page (Terms, Privacy, Refund Policy,
 * Contact Us, ...) by slug - the single component every legal/info route
 * wraps, so adding another such page later is a one-line route file, not new
 * fetch/loading/error plumbing.
 *
 * `body` is plain text, not HTML: `CmsPageForm`'s admin editor is a plain
 * textarea (see its own doc comment - no WYSIWYG/markdown field exists yet),
 * so rendering it as HTML would both do nothing useful today and open an
 * XSS hole the day it does contain a stray `<`/`&amp;`. `whitespace-pre-wrap`
 * is what turns the textarea's own line breaks back into paragraph breaks
 * without needing to interpret the content as markup.
 */
export function CmsPageView({ slug }: { slug: string }) {
  const query = useQuery({ queryKey: ["cms-page", slug], queryFn: () => getCmsPage(slug) });

  // Outer wrapper is `max-w-7xl px-4 sm:px-6` - the same container class the
  // home page uses (see `app/page.tsx`) - so this page's edges line up with
  // the header/footer. The reading-width prose block (`max-w-3xl`) then gets
  // its own `mx-auto` to center inside that wider container; without it the
  // text sits flush against the left edge with a large dead gap on the right
  // on desktop widths, which reads as broken rather than intentional.
  if (query.isPending) {
    return (
      <div className="mx-auto flex w-full max-w-7xl flex-col gap-4 px-4 py-10 sm:px-6 sm:py-14">
        <div className="mx-auto flex w-full max-w-3xl flex-col gap-4">
          <Skeleton className="h-8 w-2/3" />
          <Skeleton className="h-4 w-full" />
          <Skeleton className="h-4 w-full" />
          <Skeleton className="h-4 w-5/6" />
        </div>
      </div>
    );
  }

  if (query.isError) {
    // A 404 here just means this page hasn't been published yet (or was
    // unpublished) - CmsPagesController deliberately doesn't distinguish
    // that from "no such slug" (see its own doc comment), so neither does
    // this screen: it's a content gap, not a broken request, and gets the
    // calmer empty state below rather than a "couldn't load" error banner.
    if (query.error instanceof ApiError && query.error.status === 404) {
      return (
        <div className="mx-auto w-full max-w-7xl px-4 py-10 sm:px-6 sm:py-14">
          <div className="mx-auto max-w-3xl">
            <EmptyState title="This page isn't available yet" description="Check back soon." />
          </div>
        </div>
      );
    }

    return (
      <div className="mx-auto w-full max-w-7xl px-4 py-8 sm:px-6 sm:py-12">
        <div className="mx-auto max-w-3xl">
          <Alert
            tone="error"
            title="Couldn't load this page"
            action={
              <Button size="sm" variant="secondary" onClick={() => query.refetch()}>
                Retry
              </Button>
            }
          >
            {describeError(query.error)}
          </Alert>
        </div>
      </div>
    );
  }

  const page = query.data;

  return (
    <main className="mx-auto w-full max-w-7xl px-4 py-10 sm:px-6 sm:py-14">
      <div className="mx-auto max-w-3xl">
        <h1 className="text-display-sm font-bold tracking-tight text-fg">{page.title}</h1>
        <p className="mt-2 text-xs text-fg-subtle">
          Last updated {new Date(page.updatedAtUtc).toLocaleDateString("en-IN", { year: "numeric", month: "long", day: "numeric" })}
        </p>
        <div className="mt-8 whitespace-pre-wrap text-sm leading-relaxed text-fg-muted">{page.body}</div>
      </div>
    </main>
  );
}
