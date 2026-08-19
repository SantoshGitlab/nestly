import type { Metadata } from "next";
import localFont from "next/font/local";
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
    default: "Nestly Provider",
    template: "%s · Nestly Provider",
  },
  description: "Nestly provider portal.",
  // Task #354: Add-to-Home-Screen support. See public/manifest.json, and
  // scripts/generate-pwa-icons.sh for how its PNGs are rasterized from
  // public/icon.svg (task #368). iOS reads src/app/apple-icon.png instead,
  // via Next's file convention - it never looks at the manifest's icons.
  manifest: "/manifest.json",
};

/** Paints the browser chrome to match the theme on each side of the switch. */
export const viewport = {
  themeColor: [
    { media: "(prefers-color-scheme: light)", color: "#fafafc" },
    { media: "(prefers-color-scheme: dark)", color: "#0a0b10" },
  ],
  // Task #338 audit finding: without this, `env(safe-area-inset-*)` resolves
  // to 0 on iOS regardless of how many components reference it - it only
  // activates once the viewport opts into drawing under the notch/home
  // indicator. `ProviderTabBar`'s `pb-[env(safe-area-inset-bottom)]` and
  // `ui.tsx`'s `Modal` bottom-sheet padding were both silently inert without
  // this; it is also what makes StickyActionBar's own safe-area padding
  // (#345) work as a home-screen PWA (#354) on a notched phone.
  viewportFit: "cover",
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
          <ToastProvider>{children}</ToastProvider>
        </Providers>
      </body>
    </html>
  );
}
