import type { Metadata } from "next";
import localFont from "next/font/local";
import { SiteHeader } from "@/components/SiteHeader";
import { ToastProvider } from "@/components/ui";
import { THEME_INIT_SCRIPT } from "@/lib/theme";
import { Providers } from "./providers";
import "./globals.css";

const geistSans = localFont({
  src: "./fonts/GeistVF.woff",
  variable: "--font-geist-sans",
  weight: "100 900",
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
      <body className={`${geistSans.variable} ${geistMono.variable} antialiased`}>
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
            {/* Skip-link target. A wrapper rather than the pages' own <main>
                elements, so the anchor works without editing every route. */}
            <div id="main">{children}</div>
          </ToastProvider>
        </Providers>
      </body>
    </html>
  );
}
