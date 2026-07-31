"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { Button, Card, Field, PageHeading, Select } from "@/components/ui";
import { describeError } from "@/lib/api";
import { getSessionClaims, subscribeToAuthChanges } from "@/lib/auth";
import { createCmsPage, searchCmsPages, setCmsPagePublished, updateCmsPage } from "@/lib/cms-api";
import { CmsContentStatus, type CmsPageCreateRequest, type CmsPageResponse, type CmsPageUpdateRequest } from "@/lib/cms-types";
import { canWriteModule } from "@/lib/permissions";
import type { AdminSessionClaims } from "@/lib/types";
import { CmsPageForm } from "./_components/CmsPageForm";
import { CmsPagesTable } from "./_components/CmsPagesTable";
import { CmsTabs } from "./_components/CmsTabs";

const PAGE_SIZE = 20;

const STATUS_FILTER_OPTIONS = [
  { value: "", label: "All statuses" },
  { value: String(CmsContentStatus.Draft), label: "Draft" },
  { value: String(CmsContentStatus.Published), label: "Published" },
] as const;

/**
 * Static page management (SRS 12.16.1, task 125b): search/filter, create,
 * edit, and publish/unpublish. Gated behind the "cms" permission module -
 * every mutating control checks `canWriteModule` the same way every other
 * admin screen does (see CouponsPage's doc comment); the route itself is
 * only reachable once AdminSidebar already filtered it in by "cms.read".
 */
export default function CmsPagesPage() {
  const [claims, setClaims] = useState<AdminSessionClaims | null>(null);
  const [titleFilter, setTitleFilter] = useState("");
  const [statusFilter, setStatusFilter] = useState("");
  const [page, setPage] = useState(1);
  const [editingPage, setEditingPage] = useState<CmsPageResponse | null>(null);
  const [formError, setFormError] = useState<string | null>(null);

  useEffect(() => {
    const sync = () => setClaims(getSessionClaims());
    sync();
    return subscribeToAuthChanges(sync);
  }, []);

  const canWrite = canWriteModule(claims, "cms");
  const queryClient = useQueryClient();

  const status = statusFilter === "" ? undefined : (Number(statusFilter) as CmsContentStatus);

  const pagesQuery = useQuery({
    queryKey: ["cms", "pages", "search", titleFilter, statusFilter, page] as const,
    queryFn: () => searchCmsPages({ title: titleFilter || undefined, status, page, pageSize: PAGE_SIZE }),
  });

  const invalidatePages = () => queryClient.invalidateQueries({ queryKey: ["cms", "pages", "search"] });

  const createMutation = useMutation({
    mutationFn: (request: CmsPageCreateRequest) => createCmsPage(request),
    onSuccess: () => {
      invalidatePages();
      setFormError(null);
    },
    onError: (error) => setFormError(describeError(error)),
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, request }: { id: string; request: CmsPageUpdateRequest }) => updateCmsPage(id, request),
    onSuccess: () => {
      invalidatePages();
      setFormError(null);
      setEditingPage(null);
    },
    onError: (error) => setFormError(describeError(error)),
  });

  const toggleMutation = useMutation({
    mutationFn: ({ id, published }: { id: string; published: boolean }) => setCmsPagePublished(id, published),
    onSuccess: invalidatePages,
  });

  const handleSubmit = (request: CmsPageCreateRequest | CmsPageUpdateRequest) => {
    if (editingPage) {
      updateMutation.mutate({ id: editingPage.id, request: request as CmsPageUpdateRequest });
    } else {
      createMutation.mutate(request as CmsPageCreateRequest);
    }
  };

  const totalPages = pagesQuery.data ? Math.max(1, Math.ceil(pagesQuery.data.totalCount / PAGE_SIZE)) : 1;

  return (
    <div className="mx-auto w-full max-w-6xl">
      <PageHeading
        title="CMS & Content"
        subtitle="Static pages, banners, and site-level FAQs with draft/publish, scheduling, and placement (SRS 12.16)."
      />

      <CmsTabs />

      <div className="flex flex-col gap-6">
        {canWrite ? (
          <Card
            title={editingPage ? `Edit page: ${editingPage.title}` : "Create a page"}
            description="Title, slug, body, SEO fields, placement, and an optional publish window (SRS 12.16.1/12.16.2)."
          >
            <CmsPageForm
              page={editingPage}
              isSubmitting={createMutation.isPending || updateMutation.isPending}
              submitError={formError}
              onSubmit={handleSubmit}
              onCancel={
                editingPage
                  ? () => {
                      setEditingPage(null);
                      setFormError(null);
                    }
                  : undefined
              }
            />
          </Card>
        ) : null}

        <Card title="Pages" description="Search and manage every static page (SRS 12.16.1).">
          <div className="mb-4 flex flex-wrap items-end gap-3">
            <div className="w-56">
              <Field
                label="Title"
                placeholder="Search by title…"
                value={titleFilter}
                onChange={(event) => {
                  setPage(1);
                  setTitleFilter(event.target.value);
                }}
              />
            </div>
            <div className="w-44">
              <Select
                label="Status"
                options={STATUS_FILTER_OPTIONS}
                value={statusFilter}
                onChange={(event) => {
                  setPage(1);
                  setStatusFilter(event.target.value);
                }}
              />
            </div>
          </div>

          <CmsPagesTable
            pages={pagesQuery.data?.items}
            isLoading={pagesQuery.isLoading}
            errorMessage={pagesQuery.error ? describeError(pagesQuery.error) : null}
            canWrite={canWrite}
            onEdit={(page) => {
              setEditingPage(page);
              setFormError(null);
            }}
            onTogglePublished={(page) =>
              toggleMutation.mutate({ id: page.id, published: page.status !== CmsContentStatus.Published })
            }
            togglingId={toggleMutation.isPending ? toggleMutation.variables?.id : undefined}
          />

          {pagesQuery.data && pagesQuery.data.totalCount > PAGE_SIZE ? (
            <div className="mt-4 flex items-center justify-between text-sm text-neutral-600 dark:text-neutral-400">
              <span>
                Page {page} of {totalPages} ({pagesQuery.data.totalCount} total)
              </span>
              <div className="flex gap-2">
                <Button type="button" variant="secondary" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
                  Previous
                </Button>
                <Button
                  type="button"
                  variant="secondary"
                  disabled={page >= totalPages}
                  onClick={() => setPage((p) => p + 1)}
                >
                  Next
                </Button>
              </div>
            </div>
          ) : null}
        </Card>
      </div>
    </div>
  );
}
