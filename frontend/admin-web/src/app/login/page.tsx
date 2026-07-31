"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Button, Card, Field, PageHeading } from "@/components/ui";
import { API_V1, apiFetch, describeLoginError } from "@/lib/api";
import { isAuthenticated, storeSession, subscribeToAuthChanges } from "@/lib/auth";
import type { AdminLoginResponse } from "@/lib/types";

// Basic shape validation before spending a request; the server remains the
// authority on the real password policy (SRS 12.1.1) - this only catches
// empty/malformed input early.
const loginSchema = z.object({
  email: z.email("Enter a valid email address"),
  password: z.string().min(1, "Password is required"),
});

type LoginFormValues = z.infer<typeof loginSchema>;

export default function AdminLoginPage() {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);

  // Already signed in (e.g. back-button to /login with a live session) -
  // send straight to the dashboard instead of showing the form again.
  useEffect(() => {
    const sync = () => {
      if (isAuthenticated()) router.replace("/dashboard");
    };
    sync();
    return subscribeToAuthChanges(sync);
  }, [router]);

  const form = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: "", password: "" },
  });

  const onSubmit = form.handleSubmit(async (values) => {
    setError(null);
    try {
      const session = await apiFetch<AdminLoginResponse>(`${API_V1}/auth/login`, {
        method: "POST",
        body: JSON.stringify(values),
      });
      storeSession(session);
      router.push("/dashboard");
    } catch (err) {
      setError(describeLoginError(err));
    }
  });

  return (
    <main className="mx-auto flex min-h-screen w-full max-w-md flex-col justify-center px-6 py-12">
      <PageHeading title="Admin sign in" subtitle="Sign in with your admin email and password." />

      <Card title="Sign in">
        <form onSubmit={onSubmit} className="flex flex-col gap-4" noValidate>
          {error ? <Alert>{error}</Alert> : null}
          <Field
            label="Email"
            type="email"
            autoComplete="email"
            error={form.formState.errors.email?.message}
            {...form.register("email")}
          />
          <Field
            label="Password"
            type="password"
            autoComplete="current-password"
            error={form.formState.errors.password?.message}
            {...form.register("password")}
          />
          <Button type="submit" disabled={form.formState.isSubmitting}>
            {form.formState.isSubmitting ? "Signing in…" : "Sign in"}
          </Button>
        </form>
      </Card>
    </main>
  );
}
