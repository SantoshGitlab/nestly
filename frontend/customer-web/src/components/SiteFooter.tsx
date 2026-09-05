import Link from "next/link";
import { PROVIDER_WEB_URL } from "@/lib/unified-login-api";

const LEGAL_LINKS = [
  { href: "/terms", label: "Terms & Conditions" },
  { href: "/privacy", label: "Privacy Policy" },
  { href: "/refund-policy", label: "Refund & Cancellation Policy" },
  { href: "/contact", label: "Contact Us" },
];

/**
 * Site-wide footer. Exists mainly so the four admin-managed CMS pages
 * (see `CmsPageView`) are actually reachable from the storefront - without
 * this, a payment-gateway KYC reviewer visiting the live site would have no
 * way to find Terms/Privacy/Refund/Contact short of guessing the URL.
 */
export function SiteFooter() {
  return (
    <footer className="border-t border-border-subtle bg-surface-subtle">
      <div className="mx-auto flex w-full max-w-7xl flex-col gap-4 px-4 py-8 sm:flex-row sm:items-center sm:justify-between sm:px-6">
        <nav className="flex flex-wrap gap-x-6 gap-y-2 text-sm text-fg-muted">
          {LEGAL_LINKS.map((link) => (
            <Link key={link.href} href={link.href} className="hover:text-fg hover:underline">
              {link.label}
            </Link>
          ))}
          <a
            href={`${PROVIDER_WEB_URL}/register`}
            target="_blank"
            rel="noopener noreferrer"
            className="hover:text-fg hover:underline"
          >
            Become a Provider
          </a>
        </nav>
        <p className="text-xs text-fg-subtle">
          &copy; {new Date().getFullYear()} Glavyx. All rights reserved.
        </p>
      </div>
    </footer>
  );
}
