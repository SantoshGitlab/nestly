import Link from "next/link";

/**
 * Branded 404 (task 228). Next's default not-found page ignores the design
 * system entirely, so an ordinary typo in a URL dropped the provider onto a
 * bare white page with no navigation back.
 */
export default function ProviderNotFound() {
  return (
    <main className="flex min-h-screen items-center justify-center px-4 py-12">
      <div className="w-full max-w-md text-center">
        <p className="nums text-display-md font-semibold text-brand-600 dark:text-brand-400">404</p>
        <h1 className="mt-2 text-display-sm font-semibold text-fg">Page not found</h1>
        <p className="mt-2 text-sm leading-relaxed text-fg-muted text-pretty">
          The page you&apos;re looking for doesn&apos;t exist or has moved.
        </p>

        <Link
          href="/dashboard"
          className="mt-8 inline-flex h-10 items-center justify-center rounded-lg bg-brand-600 px-4 text-sm font-medium text-fg-on-brand shadow-brand transition duration-fast ease-out hover:bg-brand-700"
        >
          Go to dashboard
        </Link>
      </div>
    </main>
  );
}
