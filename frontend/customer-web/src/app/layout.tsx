import type { Metadata } from "next";
import localFont from "next/font/local";
import { Jost } from "next/font/google";
import { BottomTabBar } from "@/components/BottomTabBar";
import { OfflineBanner } from "@/components/OfflineBanner";
import { SiteHeader } from "@/components/SiteHeader";
import { ToastProvider } from "@/components/ui";
import { THEME_INIT_SCRIPT } from "@/lib/theme";
import { Providers } from "./providers";
import "./globals.css";

// Visual-refresh only (look and feel, matching the Resido reference site):
// Jost replaces Geist as the product's primary typeface, kept under the same
// `--font-geist-sans` CSS variable name so tailwind.config.ts's `fontFamily.sans`
// and every existing call site keep resolving without a second change.
const jost = Jost({
  subsets: ["latin"],
  weight: ["400", "500", "600", "700"],
  variable: "--font-geist-sans",
  display: "swap",
});
const geistMono = localFont({
  src: "./fonts/GeistMonoVF.woff",
  variable: "--font-geist-mono",
  weight: "100 900",
});

export const metadata: Metadata = {
  title: {
    default: "Nestly — Trusted home services, booked in minutes",
    template: "%s · Nestly",
  },
  description:
    "Cleaning, repairs, salon and more — vetted professionals, upfront pricing, and slots that fit your day.",
  // Add-to-Home-Screen / standalone install (task #354). See
  // public/manifest.json's own comment-equivalent (JSON has none) for why
  // its icon is a hand-authored SVG rather than the usual PNG set - no
  // rasterizing tool was available to generate one from this environment.
  manifest: "/manifest.json",
};

/** Paints the browser chrome to match the theme on each side of the switch. */
export const viewport = {
  themeColor: [
    { media: "(prefers-color-scheme: light)", color: "#fafafc" },
    { media: "(prefers-color-scheme: dark)", color: "#0a0b10" },
  ],
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    // suppressHydrationWarning: the pre-paint script below mutates <html>'s
    // class and style before React hydrates, which React would otherwise flag
    // as a server/client mismatch on this element.
    <html lang="en" suppressHydrationWarning>
      <head>
        <script dangerouslySetInnerHTML={{ __html: THEME_INIT_SCRIPT }} />
      </head>
      <body className={`${jost.variable} ${geistMono.variable} antialiased`}>
        <Providers>
          <ToastProvider>
            {/* Lets keyboard and screen-reader users jump the nav on every page. */}
            <a
              href="#main"
              className="sr-only focus:not-sr-only focus:absolute focus:left-4 focus:top-4 focus:z-[70] focus:rounded-lg focus:bg-brand-600 focus:px-4 focus:py-2 focus:text-sm focus:font-medium focus:text-fg-on-brand"
            >
              Skip to content
            </a>
            <SiteHeader />
            <OfflineBanner />
            {/* Skip-link target. A wrapper rather than the pages' own <main>
                elements, so the anchor works without editing every route.
                `pt-[4.5rem]` compensates for `SiteHeader` now being
                permanently `fixed` (h-[4.5rem]) rather than `sticky` — every
                page keeps the exact spacing it had before; only the home
                hero cancels this out (`-mt-[4.5rem]` in HeroBanner.tsx) to
                sit flush under the header's transparent-over-photo state.
                `pb-20` clears `BottomTabBar`, fixed below `md` on every route
                that doesn't already carry its own much larger
                `STICKY_BAR_SPACER` for a `StickyActionBar` — same spacer
                relationship as that constant documents, one level up. */}
            <div id="main" className="pt-[4.5rem] pb-20 md:pb-0">
              {children}
            </div>
            <BottomTabBar />
          </ToastProvider>
        </Providers>
      </body>
    </html>
  );
}
