/**
 * Serves the Firebase Cloud Messaging service worker at "/firebase-messaging-sw.js"
 * (task 307's customer-side counterpart - mirrors provider-web's identical
 * route). A Route Handler, not a static file under public/, specifically
 * because the worker needs process.env values baked into its source text and
 * Next.js does not template public/ assets - this is the standard escape
 * hatch for that (folder name carries the literal ".js" so the route path
 * matches exactly what messaging.getToken() in ../../lib/push.ts registers).
 *
 * Config is public web config, not secret (Firebase's own docs say so) - see
 * lib/push.ts for the corresponding main-thread loader and the "absent env
 * var -> no push" degradation this mirrors.
 */
export function GET() {
  const config = {
    apiKey: process.env.NEXT_PUBLIC_FIREBASE_API_KEY ?? "",
    authDomain: process.env.NEXT_PUBLIC_FIREBASE_AUTH_DOMAIN ?? "",
    projectId: process.env.NEXT_PUBLIC_FIREBASE_PROJECT_ID ?? "",
    messagingSenderId: process.env.NEXT_PUBLIC_FIREBASE_MESSAGING_SENDER_ID ?? "",
    appId: process.env.NEXT_PUBLIC_FIREBASE_APP_ID ?? "",
  };

  const body = `
importScripts("https://www.gstatic.com/firebasejs/10.13.2/firebase-app-compat.js");
importScripts("https://www.gstatic.com/firebasejs/10.13.2/firebase-messaging-compat.js");

// Firebase is not configured in this environment - see lib/push.ts, which
// never calls messaging.getToken() (and so never registers this worker) when
// NEXT_PUBLIC_FIREBASE_API_KEY is unset. Installed defensively anyway so a
// stale cached registration from a previous deploy does nothing rather than
// throwing on every activation.
if (${JSON.stringify(Boolean(config.apiKey))}) {
  firebase.initializeApp(${JSON.stringify(config)});

  const messaging = firebase.messaging();

  // Background handler: the tab is closed or unfocused, so there is no React
  // tree to render a notification - use the raw Notifications API directly.
  messaging.onBackgroundMessage((payload) => {
    const title = payload.notification && payload.notification.title || "Glavyx";
    const body = payload.notification && payload.notification.body || "";
    self.registration.showNotification(title, { body, icon: "/favicon.ico" });
  });
}
`.trimStart();

  return new Response(body, {
    headers: {
      "Content-Type": "application/javascript; charset=utf-8",
      // A stale worker serving a stale FCM sender id is worse than a slow
      // update - never let a CDN/browser cache this past its own request.
      "Cache-Control": "no-cache",
    },
  });
}
