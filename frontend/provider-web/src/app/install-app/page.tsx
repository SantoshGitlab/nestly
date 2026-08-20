"use client";

import { useEffect, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { Suspense } from "react";
import { AuthShell } from "@/components/auth-ui";
import { Button } from "@/components/ui";

/** Set once this screen has been shown, so a returning sign-in doesn't nag every time - only a fresh registration always shows it. */
const SEEN_KEY = "nestly.provider.install-prompt.seen";

const DEFAULT_NEXT = "/jobs";

/** Same open-redirect guard as customer-web's return-to.ts: only a same-site path is ever followed. */
function resolveNext(value: string | null): string {
  if (!value || !value.startsWith("/") || value.startsWith("//") || value.startsWith("/\\")) {
    return DEFAULT_NEXT;
  }
  return value;
}

type Platform = "ios" | "android" | "other";

type BeforeInstallPromptEvent = Event & {
  prompt: () => Promise<void>;
  userChoice: Promise<{ outcome: "accepted" | "dismissed" }>;
};

function detectPlatform(): Platform {
  if (typeof navigator === "undefined") return "other";
  const ua = navigator.userAgent;
  if (/iPhone|iPad|iPod/.test(ua)) return "ios";
  if (/Android/.test(ua)) return "android";
  return "other";
}

function isStandalone(): boolean {
  if (typeof window === "undefined") return false;
  return (
    window.matchMedia("(display-mode: standalone)").matches ||
    (window.navigator as Navigator & { standalone?: boolean }).standalone === true
  );
}

/**
 * Post-signup / mobile sign-in "install as an app" screen. Providers work
 * this portal from their phone in the field, so this is the moment they're
 * most receptive to putting it on their home screen. Desktop visitors and
 * anyone already running the installed PWA skip straight past it.
 */
export default function InstallAppPage() {
  return (
    <Suspense
      fallback={
        <AuthShell title="Almost there" subtitle="Setting things up.">
          <div />
        </AuthShell>
      }
    >
      <InstallScreen />
    </Suspense>
  );
}

function InstallScreen() {
  const router = useRouter();
  const next = resolveNext(useSearchParams().get("next"));
  const [platform, setPlatform] = useState<Platform | null>(null);
  const [deferredPrompt, setDeferredPrompt] = useState<BeforeInstallPromptEvent | null>(null);
  const [installed, setInstalled] = useState(false);

  useEffect(() => {
    const detected = detectPlatform();
    const alreadySeen = window.localStorage.getItem(SEEN_KEY) === "1";

    // Desktop, already installed, or this device has seen the prompt before
    // (e.g. a returning mobile sign-in) - nothing new to show, so don't nag.
    if (detected === "other" || isStandalone() || alreadySeen) {
      router.replace(next);
      return;
    }

    setPlatform(detected);

    const onPrompt = (event: Event) => {
      event.preventDefault();
      setDeferredPrompt(event as BeforeInstallPromptEvent);
    };
    window.addEventListener("beforeinstallprompt", onPrompt);
    return () => window.removeEventListener("beforeinstallprompt", onPrompt);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const install = async () => {
    if (!deferredPrompt) return;
    await deferredPrompt.prompt();
    const { outcome } = await deferredPrompt.userChoice;
    setDeferredPrompt(null);
    if (outcome === "accepted") setInstalled(true);
  };

  if (platform === null) {
    // Detection + redirect runs synchronously in the effect above; this
    // frame only ever paints on a genuine mobile browser, never on desktop.
    return (
      <AuthShell title="Almost there" subtitle="Setting things up.">
        <div />
      </AuthShell>
    );
  }

  return (
    <AuthShell
      title={installed ? "You're all set" : "Add Nestly Provider to your home screen"}
      subtitle={
        installed
          ? "Open it any time straight from your home screen — no browser tabs, no typing the address again."
          : "One tap, and the provider portal opens like any other app — faster, full-screen, and easy to find between jobs."
      }
    >
      <div className="flex flex-col gap-6">
        {installed ? (
          <div className="flex items-center justify-center rounded-xl bg-success-soft py-8">
            <CheckIcon />
          </div>
        ) : platform === "android" && deferredPrompt ? (
          <Button size="lg" fullWidth onClick={install}>
            Install Nestly Provider
          </Button>
        ) : (
          <ol className="flex flex-col gap-4">
            {(platform === "ios" ? IOS_STEPS : ANDROID_STEPS).map((step, index) => (
              <li key={step} className="flex items-start gap-3">
                <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-brand-gradient text-sm font-semibold text-fg-on-brand shadow-brand">
                  {index + 1}
                </span>
                <p className="pt-0.5 text-sm leading-relaxed text-fg">{step}</p>
              </li>
            ))}
          </ol>
        )}

        <Button
          size="lg"
          variant={installed ? "primary" : "ghost"}
          fullWidth
          onClick={() => {
            window.localStorage.setItem(SEEN_KEY, "1");
            router.push(next);
          }}
        >
          {installed ? "Continue" : "Maybe later"}
        </Button>
      </div>
    </AuthShell>
  );
}

const IOS_STEPS = [
  "Tap the Share icon in Safari's toolbar.",
  'Scroll down and tap "Add to Home Screen".',
  'Tap "Add" in the top corner to confirm.',
];

const ANDROID_STEPS = [
  "Tap the menu (⋮) in the top corner of your browser.",
  'Tap "Install app" or "Add to Home screen".',
  "Confirm, and the provider portal appears on your home screen.",
];

function CheckIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" className="h-10 w-10 text-success">
      <circle cx="12" cy="12" r="10" fill="currentColor" opacity="0.15" />
      <path
        d="M8 12.5l2.5 2.5L16 9"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}
