/** Public projection of a live CMS page (Terms, Privacy, Refund Policy, Contact Us, ...) - matches the backend's `CmsPageContentResponse` field-for-field. */
export interface CmsPageContent {
  title: string;
  slug: string;
  body: string;
  seoTitle: string | null;
  seoDescription: string | null;
  updatedAtUtc: string;
}
