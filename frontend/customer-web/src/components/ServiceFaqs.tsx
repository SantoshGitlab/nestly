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
      <h2 id="faqs-heading" className="mb-2 text-sm font-semibold uppercase tracking-wide text-neutral-500">
        Frequently asked questions
      </h2>
      <div className="flex flex-col divide-y divide-black/10 dark:divide-white/15">
        {faqs.map((faq) => (
          <details key={faq.id} className="group py-3">
            <summary className="cursor-pointer list-none text-sm font-medium marker:content-none">
              <span className="flex items-center justify-between gap-3">
                {faq.question}
                <span className="text-neutral-400 transition-transform group-open:rotate-45">+</span>
              </span>
            </summary>
            <p className="mt-2 text-sm text-neutral-600 dark:text-neutral-400">{faq.answer}</p>
          </details>
        ))}
      </div>
    </section>
  );
}
