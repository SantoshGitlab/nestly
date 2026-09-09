"use client";

import { useEffect, useState } from "react";
import { AuthShell } from "@/components/auth-ui";
import { Button } from "@/components/ui";

type Platform = "ios" | "android" | "desktop";

type BeforeInstallPromptEvent = Event & {
  prompt: () => Promise<void>;
  userChoice: Promise<{ outcome: "accepted" | "dismissed" }>;
};

function detectPlatform(): Platform {
  if (typeof navigator === "undefined") return "desktop";
  const ua = navigator.userAgent;
  if (/iPhone|iPad|iPod/.test(ua)) return "ios";
  if (/Android/.test(ua)) return "android";
  return "desktop";
}

function isStandalone(): boolean {
  if (typeof window === "undefined") return false;
  return (
    window.matchMedia("(display-mode: standalone)").matches ||
    (window.navigator as Navigator & { standalone?: boolean }).standalone === true
  );
}

/**
 * Standing "get the app" destination - linked from every auth screen (see
 * AuthShell) rather than pushed onto a provider mid sign-in/registration.
 * Adapts to whichever device opens it: triggers the native install prompt on
 * Android, walks through Add to Home Screen on iOS, and points a desktop
 * visitor at their phone since a PWA can't install from a desktop browser.
 */
export default function InstallAppPage() {
  const [platform, setPlatform] = useState<Platform | null>(null);
  const [standalone, setStandalone] = useState(false);
  const [deferredPrompt, setDeferredPrompt] = useState<BeforeInstallPromptEvent | null>(null);
  const [installed, setInstalled] = useState(false);

  useEffect(() => {
    setPlatform(detectPlatform());
    setStandalone(isStandalone());

    const onPrompt = (event: Event) => {
      event.preventDefault();
      setDeferredPrompt(event as BeforeInstallPromptEvent);
    };
    window.addEventListener("beforeinstallprompt", onPrompt);
    return () => window.removeEventListener("beforeinstallprompt", onPrompt);
  }, []);

  const install = async () => {
    if (!deferredPrompt) return;
    await deferredPrompt.prompt();
    const { outcome } = await deferredPrompt.userChoice;
    setDeferredPrompt(null);
    if (outcome === "accepted") setInstalled(true);
  };

  if (platform === null) {
    return (
      <AuthShell title="Get the Glavyx Provider app" subtitle="Setting things up.">
        <div />
      </AuthShell>
    );
  }

  if (standalone || installed) {
    return (
      <AuthShell
        title="You're all set"
        subtitle="Glavyx Provider is already on your home screen — open it any time, no browser tabs needed."
      >
        <div className="flex items-center justify-center rounded-xl bg-success-soft py-8">
          <CheckIcon />
        </div>
      </AuthShell>
    );
  }

  if (platform === "desktop") {
    return (
      <AuthShell
        title="Get Glavyx Provider on your phone"
        subtitle="The provider portal installs like an app from your phone's browser — this page can't install it here."
      >
        <p className="rounded-xl border border-line bg-surface-subtle px-4 py-3 text-center text-sm font-medium text-fg">
          Open this page on your phone to continue.
        </p>
      </AuthShell>
    );
  }

  return (
    <AuthShell
      title="Add Glavyx Provider to your home screen"
      subtitle="One tap, and the provider portal opens like any other app — faster, full-screen, and easy to find between jobs."
    >
      {platform === "android" && deferredPrompt ? (
        <Button size="lg" fullWidth onClick={install}>
          Install Glavyx Provider
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
