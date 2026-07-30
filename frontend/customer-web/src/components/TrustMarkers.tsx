/** Trust markers / benefits (SRS 11.1.2). Static content - no backend-driven ratings/testimonials exist yet. */
export function TrustMarkers() {
  const markers = [
    { icon: "✅", title: "Verified professionals", detail: "Background-checked and trained." },
    { icon: "💳", title: "Upfront pricing", detail: "No surprises - see the full price before you book." },
    { icon: "🔁", title: "Easy rescheduling", detail: "Change your slot in a couple of taps." },
    { icon: "🛟", title: "Support that answers", detail: "Real help if anything goes wrong." },
  ];

  return (
    <section
      aria-label="Why book with Nestly"
      className="grid grid-cols-2 gap-4 sm:grid-cols-4"
    >
      {markers.map((marker) => (
        <div
          key={marker.title}
          className="rounded-xl border border-black/10 bg-white p-4 text-center dark:border-white/15 dark:bg-neutral-900"
        >
          <div aria-hidden="true" className="text-2xl">
            {marker.icon}
          </div>
          <p className="mt-2 text-sm font-medium">{marker.title}</p>
          <p className="mt-1 text-xs text-neutral-600 dark:text-neutral-400">{marker.detail}</p>
        </div>
      ))}
    </section>
  );
}
