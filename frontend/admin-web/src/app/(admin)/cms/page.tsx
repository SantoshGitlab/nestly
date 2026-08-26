"use client";

import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { Button, Card, Field, PageHeading, Select } from "@/components/ui";
import { FilterBar, Pagination, countActiveFilters } from "@/components/data-table";
import { describeError } from "@/lib/api";
import { createCmsPage, searchCmsPages, setCmsPagePublished, updateCmsPage } from "@/lib/cms-api";
import { CmsContentStatus, type CmsPageCreateRequest, type CmsPageResponse, type CmsPageUpdateRequest } from "@/lib/cms-types";
import { canWriteModule } from "@/lib/permissions";
import { useAdminClaims } from "@/lib/use-admin-claims";
import { STATUS_FILTER_OPTIONS } from "./_components/cmsDisplay";
import { CmsPageForm } from "./_components/CmsPageForm";
import { CmsPagesTable } from "./_components/CmsPagesTable";
import { CmsTabs } from "./_components/CmsTabs";

const PAGE_SIZE = 20;

interface PageFilters {
  title: string;
  status: string;
}

const EMPTY_FILTERS: PageFilters = { title: "", status: "" };

/**
 * Static page management (SRS 12.16.1, task 125b): search/filter, create,
 * edit, and publish/unpublish. Gated behind the "cms" permission module -
 * every mutating control checks `canWriteModule` the same way every other
 * admin screen does (see CouponsPage's doc comment); the route itself is
 * only reachable once AdminSidebar already filtered it in by "cms.read".
 */
export default function CmsPagesPage() {
  const claims = useAdminClaims();
  const [filters, setFilters] = useState<PageFilters>(EMPTY_FILTERS);
  const [appliedFilters, setAppliedFilters] = useState<PageFilters>(EMPTY_FILTERS);
  const [page, setPage] = useState(1);
  const [editingPage, setEditingPage] = useState<CmsPageResponse | null>(null);
  const [formError, setFormError] = useState<string | null>(null);

  const canWrite = canWriteModule(claims, "cms");
  const queryClient = useQueryClient();

  const pagesQuery = useQuery({
    // The title filter used to sit straight in the key, so every keystroke
    // fired a paged search. Applying explicitly is one request per search.
    queryKey: ["cms", "pages", "search", appliedFilters, page] as const,
    queryFn: () =>
      searchCmsPages({
        title: appliedFilters.title || undefined,
        status:
          appliedFilters.status === "" ? undefined : (Number(appliedFilters.status) as CmsContentStatus),
        page,
        pageSize: PAGE_SIZE,
      }),
    placeholderData: keepPreviousData,
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

  const applyFilters = () => {
    setPage(1);
    setAppliedFilters(filters);
  };

  const clearFilters = () => {
    setFilters(EMPTY_FILTERS);
    setAppliedFilters(EMPTY_FILTERS);
    setPage(1);
  };

  return (
    <div className="w-full max-w-6xl">
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

        <FilterBar
          columns={2}
          onSubmit={applyFilters}
          onClear={clearFilters}
          activeCount={countActiveFilters(appliedFilters)}
          busy={pagesQuery.isFetching}
        >
          <Field
            label="Title"
            name="title"
            autoComplete="on"
            placeholder="Search by title…"
            value={filters.title}
            onChange={(event) => setFilters((current) => ({ ...current, title: event.target.value }))}
          />
          <Select
            label="Status"
            options={STATUS_FILTER_OPTIONS}
            value={filters.status}
            onChange={(event) => setFilters((current) => ({ ...current, status: event.target.value }))}
          />
        </FilterBar>

        <CmsPagesTable
          pages={pagesQuery.data?.items}
          isLoading={pagesQuery.isPending}
          isFetching={pagesQuery.isFetching}
          error={pagesQuery.error}
          onRetry={() => pagesQuery.refetch()}
          canWrite={canWrite}
          onEdit={(cmsPage) => {
            setEditingPage(cmsPage);
            setFormError(null);
          }}
          onTogglePublished={(cmsPage) =>
            toggleMutation.mutate({
              id: cmsPage.id,
              published: cmsPage.status !== CmsContentStatus.Published,
            })
          }
          togglingId={toggleMutation.isPending ? toggleMutation.variables?.id : undefined}
          toggleError={toggleMutation.error}
          emptyAction={
            countActiveFilters(appliedFilters) > 0 ? (
              <Button variant="secondary" onClick={clearFilters}>
                Clear filters
              </Button>
            ) : undefined
          }
          footer={
            pagesQuery.data ? (
              <Pagination
                page={page}
                pageSize={PAGE_SIZE}
                totalCount={pagesQuery.data.totalCount}
                onPageChange={setPage}
                busy={pagesQuery.isFetching}
                itemLabel="page"
              />
            ) : null
          }
        />
      </div>
    </div>
  );
}
