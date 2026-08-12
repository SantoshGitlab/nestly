import type { ComponentType } from "react";

/**
 * Fallback art for services/categories that have no admin-supplied
 * coverImageUrl/iconUrl set. Recognizable per-category icon art (picture
 * superiority effect: an icon is recalled faster than a name) beats a plain
 * text list - keyed off the service name itself, same pattern HeroBanner
 * already uses for its decorative cluster.
 */

const ICON_PROPS = {
  viewBox: "0 0 24 24",
  fill: "none",
  stroke: "currentColor",
  strokeWidth: "1.6",
  strokeLinecap: "round" as const,
  strokeLinejoin: "round" as const,
  className: "h-8 w-8",
};

function BroomIcon() {
  return (
    <svg {...ICON_PROPS}>
      <path d="M19 3 8.5 13.5M13 15l-8 6M9 11l4 4-1.5 3.5L3 20l1.5-8.5L9 11Z" />
    </svg>
  );
}
function DropletIcon() {
  return (
    <svg {...ICON_PROPS}>
      <path d="M12 3s6 6.5 6 11a6 6 0 1 1-12 0c0-4.5 6-11 6-11Z" />
    </svg>
  );
}
function BoltIcon() {
  return (
    <svg {...ICON_PROPS}>
      <path d="M13 2 4 14h6l-1 8 9-12h-6l1-8Z" />
    </svg>
  );
}
function ScissorsIcon() {
  return (
    <svg {...ICON_PROPS}>
      <circle cx="6" cy="6" r="2.5" />
      <circle cx="6" cy="18" r="2.5" />
      <path d="m20 4-12 12M8 12l12 8M8.5 8.5 12 12" />
    </svg>
  );
}
function BugIcon() {
  return (
    <svg {...ICON_PROPS}>
      <path d="M8 7V5.5a4 4 0 0 1 8 0V7M6 10h12M6 14h12M9 21l-2-3M15 21l2-3M4 8l2.5 2M20 8l-2.5 2" />
      <rect x="8" y="7" width="8" height="11" rx="4" />
    </svg>
  );
}
function PaintRollerIcon() {
  return (
    <svg {...ICON_PROPS}>
      <rect x="4" y="4" width="12" height="6" rx="1.5" />
      <path d="M8 10v4h4v6" />
    </svg>
  );
}
function HammerIcon() {
  return (
    <svg {...ICON_PROPS}>
      <path d="m14.5 5.5 4 4-2 2-4-4 2-2Z" />
      <path d="m12.5 7.5-9 9 2 2 9-9M17 4l3 3" />
    </svg>
  );
}
function BoxIcon() {
  return (
    <svg {...ICON_PROPS}>
      <path d="M3 8.5 12 4l9 4.5-9 4.5-9-4.5Z" />
      <path d="M3 8.5V17l9 4.5 9-4.5V8.5M12 13v8.5" />
    </svg>
  );
}
function SnowflakeIcon() {
  return (
    <svg {...ICON_PROPS}>
      <path d="M12 2v20M4.2 7l15.6 10M4.2 17 19.8 7" />
    </svg>
  );
}
function DumbbellIcon() {
  return (
    <svg {...ICON_PROPS}>
      <path d="M6 7v10M18 7v10M2 12h4M18 12h4M6 12h12" />
    </svg>
  );
}
function BookIcon() {
  return (
    <svg {...ICON_PROPS}>
      <path d="M4 5.5A2.5 2.5 0 0 1 6.5 3H20v15H6.5A2.5 2.5 0 0 0 4 20.5v-15Z" />
      <path d="M4 20.5A2.5 2.5 0 0 1 6.5 18H20" />
    </svg>
  );
}
function ChefHatIcon() {
  return (
    <svg {...ICON_PROPS}>
      <path d="M7 21h10M8 21v-6M16 21v-6M6 11a4 4 0 0 1 3-6.9A4 4 0 0 1 12 3a4 4 0 0 1 3 1.1A4 4 0 0 1 18 11c0 2.5-1.5 4-3 4H9c-1.5 0-3-1.5-3-4Z" />
    </svg>
  );
}
function WrenchIcon() {
  return (
    <svg {...ICON_PROPS}>
      <path d="M14.7 6.3a4 4 0 0 0-5.4 5.4L4 17l3 3 5.3-5.3a4 4 0 0 0 5.4-5.4l-2.6 2.6-2-2Z" />
    </svg>
  );
}
function SparkleIcon() {
  return (
    <svg {...ICON_PROPS}>
      <path d="M12 3v5M12 16v5M3 12h5M16 12h5M6 6l3 3M15 15l3 3M18 6l-3 3M9 15l-3 3" />
    </svg>
  );
}

interface ServiceVisual {
  icon: ComponentType;
  gradient: string;
}

const KEYWORD_VISUALS: { pattern: RegExp; visual: ServiceVisual }[] = [
  { pattern: /clean/i, visual: { icon: BroomIcon, gradient: "from-brand-500 to-brand-700" } },
  { pattern: /plumb|tap|pipe|leak|water/i, visual: { icon: DropletIcon, gradient: "from-info to-brand-600" } },
  { pattern: /electric|wiring|switch|fan/i, visual: { icon: BoltIcon, gradient: "from-warning to-accent-600" } },
  { pattern: /salon|beauty|spa|hair|makeup/i, visual: { icon: ScissorsIcon, gradient: "from-accent-500 to-accent-700" } },
  { pattern: /pest|termite|cockroach|rodent|mosquito/i, visual: { icon: BugIcon, gradient: "from-success to-brand-700" } },
  { pattern: /paint/i, visual: { icon: PaintRollerIcon, gradient: "from-danger to-accent-600" } },
  { pattern: /carpent|furniture|wood/i, visual: { icon: HammerIcon, gradient: "from-brand-700 to-brand-900" } },
  { pattern: /pack|mov|shift/i, visual: { icon: BoxIcon, gradient: "from-info to-accent-600" } },
  { pattern: /\bac\b|air.?condition|hvac|cooler|refrigerat/i, visual: { icon: SnowflakeIcon, gradient: "from-info to-brand-500" } },
  { pattern: /fitness|yoga|gym|trainer/i, visual: { icon: DumbbellIcon, gradient: "from-success to-accent-600" } },
  { pattern: /tutor|class|education|learn/i, visual: { icon: BookIcon, gradient: "from-accent-600 to-brand-700" } },
  { pattern: /cater|cook|chef|meal/i, visual: { icon: ChefHatIcon, gradient: "from-warning to-danger" } },
  { pattern: /applianc|repair|fix|install/i, visual: { icon: WrenchIcon, gradient: "from-brand-600 to-accent-600" } },
];

const DEFAULT_VISUAL: ServiceVisual = { icon: SparkleIcon, gradient: "from-brand-500 to-accent-600" };

/** Matched against the service name; category name works too if it reads better for a given surface. */
export function getServiceVisual(name: string): ServiceVisual {
  return KEYWORD_VISUALS.find((entry) => entry.pattern.test(name))?.visual ?? DEFAULT_VISUAL;
}
