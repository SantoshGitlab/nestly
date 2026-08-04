"use client";

import { NavTabs } from "@/components/nav-tabs";

/** Sub-nav between the three SRS 12.16 CMS screens: static pages, banners, and site-level FAQs (tasks 125a-125c). */
export function CmsTabs() {
  return (
    <NavTabs
      label="CMS sections"
      tabs={[
        { href: "/cms", label: "Pages" },
        { href: "/cms/banners", label: "Banners" },
        { href: "/cms/faqs", label: "FAQs" },
      ]}
    />
  );
}
