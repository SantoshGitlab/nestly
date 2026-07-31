"use client";

import { useRouter } from "next/navigation";
import { useEffect } from "react";
import { isAuthenticated } from "@/lib/auth";

/**
 * The admin panel has no public landing page - send the visitor straight to
 * the dashboard (if a live session exists) or the login screen otherwise.
 */
export default function RootPage() {
  const router = useRouter();

  useEffect(() => {
    router.replace(isAuthenticated() ? "/dashboard" : "/login");
  }, [router]);

  return <p className="p-8 text-sm text-neutral-500">Loading…</p>;
}
