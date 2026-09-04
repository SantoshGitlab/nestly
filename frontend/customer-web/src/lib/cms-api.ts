/** Typed client for the public static-page endpoint (`CmsPagesController`). No auth - same as `/categories`/`/landing/home`. */
import { API_V1, apiFetch } from "./api";
import type { CmsPageContent } from "./cms-types";

export const getCmsPage = (slug: string) => apiFetch<CmsPageContent>(`${API_V1}/cms/pages/${slug}`);
