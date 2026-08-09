/**
 * Lazy client-side loader for Firebase Cloud Messaging web push (task 307).
 *
 * Mirrors src/lib/googleMaps.ts's shape deliberately: nothing here runs at
 * import time, the Firebase JS SDK loads via a CDN `<script>` tag rather than
 * an npm dependency (no bundler-side Firebase package exists in this repo,
 * and the domain model's DevicePlatform enum only knows Fcm/Apns - it does
 * not know "no push provider configured" as a distinct state, so this has to
 * degrade to null rather than ever calling the registration endpoint with a
 * placeholder token).
 *
 * `NEXT_PUBLIC_FIREBASE_*` are optional. This project ships with no real
 * FCM credentials (see SandboxPushNotificationProvider's doc comment - the
 * *server* side of push delivery is a logging sandbox in every environment
 * today), so by default these resolve to null and the caller shows its
 * documented no-push state instead of registering a token nobody can ever
 * deliver to.
 */

declare global {
  interface Window {
    firebase?: {
      initializeApp: (config: Record<string, string>) => void;
      messaging: () => {
        getToken: (options: {
          vapidKey?: string;
          serviceWorkerRegistration: ServiceWorkerRegistration;
        }) => Promise<string>;
      };
      apps?: unknown[];
    };
  }
}

interface FirebaseWebConfig extends Record<string, string> {
  apiKey: string;
  authDomain: string;
  projectId: string;
  messagingSenderId: string;
  appId: string;
}

function readConfig(): FirebaseWebConfig | null {
  const apiKey = process.env.NEXT_PUBLIC_FIREBASE_API_KEY;
  const authDomain = process.env.NEXT_PUBLIC_FIREBASE_AUTH_DOMAIN;
  const projectId = process.env.NEXT_PUBLIC_FIREBASE_PROJECT_ID;
  const messagingSenderId = process.env.NEXT_PUBLIC_FIREBASE_MESSAGING_SENDER_ID;
  const appId = process.env.NEXT_PUBLIC_FIREBASE_APP_ID;

  if (!apiKey || !authDomain || !projectId || !messagingSenderId || !appId) {
    return null;
  }

  return { apiKey, authDomain, projectId, messagingSenderId, appId };
}

let loadPromise: Promise<typeof window.firebase | null> | null = null;

function loadScript(src: string): Promise<void> {
  return new Promise((resolve, reject) => {
    const script = document.createElement("script");
    script.src = src;
    script.async = true;
    script.onload = () => resolve();
    script.onerror = () => reject(new Error(`Failed to load ${src}`));
    document.head.appendChild(script);
  });
}

function loadFirebaseSdk(): Promise<typeof window.firebase | null> {
  if (loadPromise) return loadPromise;

  const config = readConfig();
  if (!config) {
    loadPromise = Promise.resolve(null);
    return loadPromise;
  }

  loadPromise = (async () => {
    if (!window.firebase) {
      await loadScript("https://www.gstatic.com/firebasejs/10.13.2/firebase-app-compat.js");
      await loadScript("https://www.gstatic.com/firebasejs/10.13.2/firebase-messaging-compat.js");
    }
    if (!window.firebase) return null;
    if (!window.firebase.apps?.length) {
      window.firebase.initializeApp(config);
    }
    return window.firebase;
  })().catch((): typeof window.firebase | null => {
    // A network failure loading the SDK degrades to the same "no push"
    // state as an absent config - one failure mode, not two.
    return null;
  });

  return loadPromise;
}

/**
 * Requests notification permission, registers the FCM service worker, and
 * returns an FCM registration token - or null if Firebase is not configured,
 * the browser does not support push (no ServiceWorker/PushManager - Safari
 * before 16.4, most in-app browsers), or the provider declines the
 * permission prompt. Never throws; every failure mode is "no push for this
 * session," which the caller (../app/(provider)/layout.tsx) treats as
 * routine, not an error to surface.
 */
export async function requestPushToken(): Promise<string | null> {
  if (typeof window === "undefined") return null;
  if (!("serviceWorker" in navigator) || !("PushManager" in window) || !("Notification" in window)) {
    return null;
  }

  const firebase = await loadFirebaseSdk();
  if (!firebase) return null;

  const permission = await Notification.requestPermission();
  if (permission !== "granted") return null;

  try {
    const registration = await navigator.serviceWorker.register("/firebase-messaging-sw.js");
    const vapidKey = process.env.NEXT_PUBLIC_FIREBASE_VAPID_KEY;
    const token = await firebase.messaging().getToken({
      vapidKey,
      serviceWorkerRegistration: registration,
    });
    return token || null;
  } catch {
    // getToken rejects on a misconfigured VAPID key, a revoked service
    // worker registration, etc. - all of it is "push did not set up this
    // time," not something the caller can act on.
    return null;
  }
}
