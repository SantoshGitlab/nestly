"use client";

import { Badge } from "@/components/ui";
import { CmsContentStatus } from "@/lib/cms-types";

/**
 * Draft/Published pill for the three CMS lists.
 *
 * Each list previously built this inline from raw `green-*`/`neutral-*`
 * classes, which is both off-token and three places to keep in step.
 */
export function CmsStatusBadge({ status }: { status: CmsContentStatus }) {
  const published = status === CmsContentStatus.Published;
  return <Badge tone={published ? "success" : "neutral"}>{published ? "Published" : "Draft"}</Badge>;
}
