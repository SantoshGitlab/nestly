import Link from "next/link";

export default function Home() {
  return (
    <main className="mx-auto w-full max-w-2xl px-6 py-16">
      <h1 className="text-3xl font-semibold tracking-tight">Nestly</h1>
      <p className="mt-3 text-neutral-600 dark:text-neutral-400">
        Book trusted home services. Sign in with your mobile number to manage your
        profile and saved addresses.
      </p>

      <div className="mt-8 flex flex-wrap gap-3">
        <Link
          href="/login"
          className="rounded-lg bg-black px-4 py-2 text-sm font-medium text-white hover:bg-neutral-800 dark:bg-white dark:text-black dark:hover:bg-neutral-200"
        >
          Sign in
        </Link>
        <Link
          href="/register"
          className="rounded-lg border border-black/15 px-4 py-2 text-sm font-medium hover:bg-black/5 dark:border-white/20 dark:hover:bg-white/10"
        >
          Create an account
        </Link>
      </div>
    </main>
  );
}
