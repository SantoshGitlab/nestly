"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { Button, Card, PageHeading, Select } from "@/components/ui";
import { describeError } from "@/lib/api";
import { getSessionClaims, subscribeToAuthChanges } from "@/lib/auth";
import { createCmsFaq, searchCmsFaqs, setCmsFaqPublished, updateCmsFaq } from "@/lib/cms-api";
import { CmsContentStatus, type CmsFaqCreateRequest, type CmsFaqResponse, type CmsFaqUpdateRequest } from "@/lib/cms-types";
import { canWriteModule } from "@/lib/permissions";
import type { AdminSessionClaims } from "@/lib/types";
import { CmsFaqForm } from "../_components/CmsFaqForm";
import { CmsFaqsTable } from "../_components/CmsFaqsTable";
import { PLACEMENT_OPTIONS } from "../_components/cmsDisplay";
import { CmsTabs } from "../_components/CmsTabs";

const PAGE_SIZE = 20;

const STATUS_FILTER_OPTIONS = [
  { value: "", label: "All statuses" },
  { value: String(CmsContentStatus.Draft), label: "Draft" },
  { value: String(CmsContentStatus.Published), label: "Published" },
] as const;

const PLACEMENT_FILTER_OPTIONS = [{ value: "", label: "All placements" }, ...PLACEMENT_OPTIONS] as const;

/**
 * Site-level FAQ management (SRS 12.16.1, task 125c): search/filter by
 * placement and status, create, edit, and publish/unpublish. Gated behind
 * the "cms" permission module - every mutating control checks
 * `canWriteModule` the same way CmsPagesPage does; the route itself is only
 * reachable once AdminSidebar already filtered it in by "cms.read".
 */
export default function CmsFaqsPage() {
  const [claims, setClaims] = useState<AdminSessionClaims | null>(null);
  const [placementFilter, setPlacementFilter] = useState("");
  const [statusFilter, setStatusFilter] = useState("");
  const [page, setPage] = useState(1);
  const [editingFaq, setEditingFaq] = useState<CmsFaqResponse | null>(null);
  const [formError, setFormError] = useState<string | null>(null);

  useEffect(() => {
    const sync = () => setClaims(getSessionClaims());
    sync();
    return subscribeToAuthChanges(sync);
  }, []);

  const canWrite = canWriteModule(claims, "cms");
  const queryClient = useQueryClient();

  const placement = placementFilter === "" ? undefined : (Number(placementFilter) as CmsFaqResponse["placement"]);
  const status = statusFilter === "" ? undefined : (Number(statusFilter) as CmsContentStatus);

  const faqsQuery = useQuery({
    queryKey: ["cms", "faqs", "search", placementFilter, statusFilter, page] as const,
    queryFn: () => searchCmsFaqs({ placement, status, page, pageSize: PAGE_SIZE }),
  });

  const invalidateFaqs = () => queryClient.invalidateQueries({ queryKey: ["cms", "faqs", "search"] });

  const createMutation = useMutation({
    mutationFn: (request: CmsFaqCreateRequest) => createCmsFaq(request),
    onSuccess: () => {
      invalidateFaqs();
      setFormError(null);
    },
    onError: (error) => setFormError(describeError(error)),
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, request }: { id: string; request: CmsFaqUpdateRequest }) => updateCmsFaq(id, request),
    onSuccess: () => {
      invalidateFaqs();
      setFormError(null);
      setEditingFaq(null);
    },
    onError: (error) => setFormError(describeError(error)),
  });

  const toggleMutation = useMutation({
    mutationFn: ({ id, published }: { id: string; published: boolean }) => setCmsFaqPublished(id, published),
    onSuccess: invalidateFaqs,
  });

  const handleSubmit = (request: CmsFaqCreateRequest | CmsFaqUpdateRequest) => {
    if (editingFaq) {
      updateMutation.mutate({ id: editingFaq.id, request: request as CmsFaqUpdateRequest });
    } else {
      createMutation.mutate(request as CmsFaqCreateRequest);
    }
  };

  const totalPages = faqsQuery.data ? Math.max(1, Math.ceil(faqsQuery.data.totalCount / PAGE_SIZE)) : 1;

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
            title={editingFaq ? `Edit FAQ: ${editingFaq.question}` : "Create an FAQ"}
            description="Question, answer, placement, sort order, and an optional publish window (SRS 12.16.1/12.16.2)."
          >
            <CmsFaqForm
              faq={editingFaq}
              isSubmitting={createMutation.isPending || updateMutation.isPending}
              submitError={formError}
              onSubmit={handleSubmit}
              onCancel={
                editingFaq
                  ? () => {
                      setEditingFaq(null);
                      setFormError(null);
                    }
                  : undefined
              }
            />
          </Card>
        ) : null}

        <Card title="FAQs" description="Search and manage every site-level FAQ (SRS 12.16.1).">
          <div className="mb-4 flex flex-wrap items-end gap-3">
            <div className="w-52">
              <Select
                label="Placement"
                options={PLACEMENT_FILTER_OPTIONS}
                value={placementFilter}
                onChange={(event) => {
                  setPage(1);
                  setPlacementFilter(event.target.value);
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

          <CmsFaqsTable
            faqs={faqsQuery.data?.items}
            isLoading={faqsQuery.isLoading}
            errorMessage={faqsQuery.error ? describeError(faqsQuery.error) : null}
            canWrite={canWrite}
            onEdit={(faq) => {
              setEditingFaq(faq);
              setFormError(null);
            }}
            onTogglePublished={(faq) =>
              toggleMutation.mutate({ id: faq.id, published: faq.status !== CmsContentStatus.Published })
            }
            togglingId={toggleMutation.isPending ? toggleMutation.variables?.id : undefined}
          />

          {faqsQuery.data && faqsQuery.data.totalCount > PAGE_SIZE ? (
            <div className="mt-4 flex items-center justify-between text-sm text-neutral-600 dark:text-neutral-400">
              <span>
                Page {page} of {totalPages} ({faqsQuery.data.totalCount} total)
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
