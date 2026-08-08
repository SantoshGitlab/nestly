import { NextResponse } from "next/server";

/**
 * Server-side proxy for provider-api's dev-only
 * POST /api/v1/auth/dev/login-as-provider (docs/DEVOPS.md "Dev-only provider
 * test login"). Exists so the shared secret (DEV_AUTH_KEY) never ships to
 * the browser bundle - only NEXT_PUBLIC_* vars do that, and this is
 * deliberately not one.
 *
 * Double-gated like the backend: this route does nothing unless
 * NEXT_PUBLIC_ENABLE_DEV_AUTH=true, and the backend independently 404s
 * outside Development regardless of what this route sends it.
 */
const DEV_PROVIDER_MOBILE = "+919888888888";

export async function POST() {
  if (process.env.NEXT_PUBLIC_ENABLE_DEV_AUTH !== "true") {
    return NextResponse.json({ detail: "Not found" }, { status: 404 });
  }

  const devAuthKey = process.env.DEV_AUTH_KEY;
  if (!devAuthKey) {
    return NextResponse.json(
      { detail: "DEV_AUTH_KEY is not configured on the server." },
      { status: 500 },
    );
  }

  const apiBaseUrl = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5337";

  const upstream = await fetch(`${apiBaseUrl}/api/v1/auth/dev/login-as-provider`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "X-Dev-Auth-Key": devAuthKey,
    },
    body: JSON.stringify({ mobile: DEV_PROVIDER_MOBILE }),
  });

  const body = await upstream.text();
  return new NextResponse(body, {
    status: upstream.status,
    headers: { "Content-Type": "application/json" },
  });
}
