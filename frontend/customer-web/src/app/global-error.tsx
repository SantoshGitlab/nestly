"use client";

/**
 * Last-resort boundary: this one catches errors thrown by the root layout
 * itself, so it *replaces* that layout and must render its own <html> and
 * <body> (task 228).
 *
 * Deliberately dependency-free — no design-system imports, no Tailwind
 * classes, no fonts. Everything it would import is mounted by the very layout
 * that just failed, so relying on any of it risks this screen throwing too and
 * leaving the customer with a blank document. The inline <style> is the only
 * way to stay theme-aware without that dependency.
 */
export default function CustomerGlobalError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  return (
    <html lang="en">
      <body>
        <style>{`
          .ne-wrap {
            min-height: 100vh; display: flex; align-items: center;
            justify-content: center; padding: 3rem 1rem; margin: 0;
            font-family: system-ui, -apple-system, "Segoe UI", sans-serif;
            background: #fafafc; color: #101420;
          }
          .ne-card { max-width: 28rem; text-align: center; }
          .ne-title { font-size: 1.5rem; font-weight: 600; margin: 0 0 0.5rem; }
          .ne-body { font-size: 0.875rem; line-height: 1.6; color: #5a6274; margin: 0; }
          .ne-ref { font-size: 0.75rem; color: #858d9e; margin-top: 1rem;
            font-variant-numeric: tabular-nums; }
          .ne-btn {
            margin-top: 2rem; display: inline-flex; align-items: center; height: 2.5rem;
            padding: 0 1rem; border: 0; border-radius: 0.5rem; cursor: pointer;
            background: #764ae7; color: #fff; font-size: 0.875rem; font-weight: 500;
            font-family: inherit;
          }
          @media (prefers-color-scheme: dark) {
            .ne-wrap { background: #0a0b10; color: #f2f4f8; }
            .ne-body { color: #a2aabb; }
            .ne-ref { color: #798299; }
          }
        `}</style>

        <div className="ne-wrap">
          <div className="ne-card">
            <h1 className="ne-title">Something went wrong</h1>
            <p className="ne-body">
              The app failed to start. Reloading usually fixes it — your account and bookings are
              unaffected.
            </p>
            {error.digest ? <p className="ne-ref">Reference: {error.digest}</p> : null}
            <button type="button" className="ne-btn" onClick={reset}>
              Reload
            </button>
          </div>
        </div>
      </body>
    </html>
  );
}
