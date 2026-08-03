import type { ServiceFaq } from "@/lib/types";

/**
 * FAQ accordion on the service detail page (SRS 11.6.1 "FAQs", task 52d).
 * Native <details>/<summary> rather than a JS accordion widget - it gives
 * expand/collapse, keyboard support, and screen-reader semantics for free.
 */
export function ServiceFaqs({ faqs }: { faqs: ServiceFaq[] }) {
  if (faqs.length === 0) {
    return null;
  }

  return (
    <section aria-labelledby="faqs-heading">
      <h2 id="faqs-heading" className="mb-4 text-lg font-semibold tracking-tight text-fg">
        Frequently asked questions
      </h2>
      <div className="divide-y divide-line overflow-hidden rounded-2xl border border-line bg-surface">
        {faqs.map((faq) => (
          <details key={faq.id} className="group">
            <summary className="flex cursor-pointer list-none items-center justify-between gap-3 px-4 py-3.5 text-sm font-medium text-fg transition-colors duration-fast ease-out marker:content-none hover:bg-surface-2">
              {faq.question}
              <svg
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                className="h-4 w-4 shrink-0 text-fg-subtle transition-transform duration-fast ease-out group-open:rotate-180"
                aria-hidden
              >
                <path d="m6 9 6 6 6-6" />
              </svg>
            </summary>
            <p className="px-4 pb-4 text-sm leading-relaxed text-fg-muted">{faq.answer}</p>
          </details>
        ))}
      </div>
    </section>
  );
}
