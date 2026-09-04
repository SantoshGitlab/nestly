import type { Metadata } from "next";
import { Manrope } from "next/font/google";
import localFont from "next/font/local";
import { ToastProvider } from "@/components/ui";
import { THEME_INIT_SCRIPT } from "@/lib/theme";
import { Providers } from "./providers";
import "./globals.css";

/** MatDash's reference typeface — see globals.css's top-of-file note. */
const manrope = Manrope({
  subsets: ["latin"],
  variable: "--font-geist-sans",
  weight: ["400", "500", "600", "700", "800"],
});
const geistMono = localFont({
  src: "./fonts/GeistMonoVF.woff",
  variable: "--font-geist-mono",
  weight: "100 900",
});

export const metadata: Metadata = {
  title: {
    default: "Glavyx Admin",
    template: "%s · Glavyx Admin",
  },
  description: "Glavyx admin panel.",
};

/** Paints the browser chrome to match the theme on each side of the switch. */
export const viewport = {
  themeColor: [
    { media: "(prefers-color-scheme: light)", color: "#fafafc" },
    { media: "(prefers-color-scheme: dark)", color: "#0a0b10" },
  ],
  // Task #351 audit finding (same root cause as provider-web's #338 note):
  // without this, `env(safe-area-inset-*)` resolves to 0 on iOS regardless
  // of how many components reference it. admin-web has no fixed bottom nav
  // or sticky CTA (desk-first per policy), but `ui.tsx`'s `Modal` still has
  // a bottom-sheet mobile state with `env(safe-area-inset-bottom)` padding
  // that needs this to work on a phone-width admin session. admin-web has no
  // manifest.json (not installable as a standalone PWA), so this only ever
  // matters inside a normal browser tab's bottom safe area, never a notch.
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
      <body className={`${manrope.variable} ${geistMono.variable} antialiased`}>
        <Providers>
          <ToastProvider>{children}</ToastProvider>
        </Providers>
      </body>
    </html>
  );
}
