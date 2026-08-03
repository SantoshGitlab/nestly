"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { ErrorState } from "@/components/states";
import {
  Alert,
  Badge,
  Button,
  Card,
  EmptyState,
  Field,
  Select,
  Skeleton,
  cx,
  useToast,
} from "@/components/ui";
import { formatDate } from "@/lib/format";
import { getKycStatus, submitKycDocument } from "@/lib/profile-api";
import type { BadgeTone } from "@/components/ui";
import type { KycDocType, KycDocument, KycVerificationStatus } from "@/lib/profile-types";

const DOC_TYPE_OPTIONS: { value: KycDocType; label: string }[] = [
  { value: "IdentityProof", label: "Identity proof" },
  { value: "AddressProof", label: "Address proof" },
  { value: "BankAccountProof", label: "Bank account proof" },
  { value: "ProfessionalCertificate", label: "Professional certificate" },
  { value: "Other", label: "Other" },
];

const docTypeLabel = (docType: KycDocType | string) =>
  DOC_TYPE_OPTIONS.find((option) => option.value === docType)?.label ?? docType;

const VERIFICATION_STATUS_LABELS: Record<KycVerificationStatus, string> = {
  Pending: "Under review",
  Approved: "Approved",
  Rejected: "Rejected",
};

const VERIFICATION_STATUS_TONES: Record<KycVerificationStatus, BadgeTone> = {
  Pending: "warning",
  Approved: "success",
  Rejected: "danger",
};

const kycDocumentSchema = z.object({
  docType: z.enum([
    "IdentityProof",
    "AddressProof",
    "BankAccountProof",
    "ProfessionalCertificate",
    "Other",
  ]),
  fileRef: z.string().min(1, "Provide a file reference").max(2000),
  docNumber: z.string().max(100),
});
type KycDocumentFormValues = z.infer<typeof kycDocumentSchema>;

const EMPTY_FORM: KycDocumentFormValues = {
  docType: "IdentityProof",
  fileRef: "",
  docNumber: "",
};

/**
 * KYC status and document submission (docs/PARTNER.md's Identity domain,
 * `partner_kyc_document`).
 *
 * Per-document state is the whole point of this screen: a partner blocked from
 * going live needs to see *which* document is holding them up and what to do
 * about it. So every document carries its own status badge, a rejected one
 * gets an explicit "send a new one" affordance that pre-selects its type, and
 * the card leads with a single summary line for the account as a whole.
 *
 * There is still no file storage backend on the platform, so `fileRef` is a
 * text reference/URL the partner pastes in. The `<input type="file">` below is
 * NOT wired to any upload endpoint - it only copies the chosen file's *name*
 * into the reference field, which the partner can still edit. That is stated
 * on the control itself rather than left as a surprise.
 */
export function KycSection() {
  const queryClient = useQueryClient();
  const toast = useToast();

  const query = useQuery({ queryKey: ["partner-kyc"], queryFn: getKycStatus });

  const form = useForm<KycDocumentFormValues>({
    resolver: zodResolver(kycDocumentSchema),
    defaultValues: EMPTY_FORM,
  });

  const mutation = useMutation({
    mutationFn: (values: KycDocumentFormValues) =>
      submitKycDocument({
        docType: values.docType,
        fileRef: values.fileRef,
        docNumber: values.docNumber.trim() === "" ? undefined : values.docNumber,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["partner-kyc"] });
      // The onboarding step shown on the profile card advances on the first
      // submission, so that query is stale now too.
      queryClient.invalidateQueries({ queryKey: ["partner-profile"] });
      form.reset(EMPTY_FORM);
      toast("success", "Document submitted for review.");
    },
  });

  const onSubmit = form.handleSubmit((values) => mutation.mutate(values));

  /** Pre-selects a rejected document's type and drops the cursor in the reference field. */
  function resubmit(doc: KycDocument) {
    form.setValue("docType", doc.docType);
    form.setValue("docNumber", doc.docNumber ?? "");
    form.setValue("fileRef", "");
    form.setFocus("fileRef");
  }

  return (
    <Card
      title="KYC documents"
      description="Identity and verification documents required before you can go live."
    >
      <div className="flex flex-col gap-5">
        {query.isPending ? (
          <div className="flex flex-col gap-2.5" aria-hidden>
            {Array.from({ length: 2 }, (_, index) => (
              <Skeleton key={index} className="h-20 w-full rounded-xl" />
            ))}
          </div>
        ) : query.isError ? (
          <ErrorState
            title="Couldn't load your KYC status"
            error={query.error}
            onRetry={() => query.refetch()}
            isRetrying={query.isRefetching}
          />
        ) : query.data.documents.length === 0 ? (
          <EmptyState
            title="No documents submitted yet"
            description="Send an identity proof to start verification. You can add address and bank proofs afterwards."
            action={
              <Button variant="secondary" onClick={() => form.setFocus("fileRef")}>
                Add your first document
              </Button>
            }
          />
        ) : (
          <>
            <VerificationSummary documents={query.data.documents} />

            <ul className="flex flex-col gap-2.5">
              {query.data.documents.map((doc) => (
                <li key={doc.id}>
                  <DocumentRow doc={doc} onResubmit={() => resubmit(doc)} />
                </li>
              ))}
            </ul>
          </>
        )}

        <form
          onSubmit={onSubmit}
          className="flex flex-col gap-4 border-t border-line pt-5"
          noValidate
        >
          <h3 className="text-sm font-semibold text-fg">Submit a document</h3>

          {mutation.isError ? (
            <ErrorState title="Couldn't submit that document" error={mutation.error} />
          ) : null}

          <div className="grid gap-4 sm:grid-cols-2">
            <Select
              label="Document type"
              options={DOC_TYPE_OPTIONS}
              {...form.register("docType")}
            />
            <Field
              label="Document number (optional)"
              error={form.formState.errors.docNumber?.message}
              {...form.register("docNumber")}
            />
          </div>

          <Field
            label="File reference (URL or reference ID)"
            placeholder="https://…"
            hint="Paste a link to the document. File upload isn't available on the platform yet."
            error={form.formState.errors.fileRef?.message}
            {...form.register("fileRef")}
          />

          {/* Not wired to any upload endpoint - file storage does not exist on
              the platform yet. This only copies a local file's name into the
              reference field above; nothing leaves the device. */}
          <div className="flex flex-col gap-1.5">
            <label htmlFor="kyc-local-file" className="text-sm font-medium text-fg">
              Pick a local file
            </label>
            <input
              id="kyc-local-file"
              type="file"
              aria-describedby="kyc-local-file-hint"
              onChange={(event) => {
                const fileName = event.target.files?.[0]?.name;
                if (fileName) form.setValue("fileRef", fileName, { shouldValidate: true });
              }}
              className={cx(
                "w-full cursor-pointer rounded-lg border border-line bg-surface text-sm text-fg-muted shadow-xs outline-none transition duration-fast ease-out",
                "hover:border-line-strong focus:border-brand-600 focus:ring-2 focus:ring-brand-600/25",
                "file:mr-3 file:cursor-pointer file:border-0 file:bg-surface-3 file:px-4 file:py-2.5 file:text-sm file:font-medium file:text-fg",
              )}
            />
            <p id="kyc-local-file-hint" className="text-xs text-fg-muted">
              Copies the file&apos;s name into the reference above. Nothing is uploaded.
            </p>
          </div>

          <Button type="submit" size="lg" fullWidth loading={mutation.isPending}>
            Submit document
          </Button>
        </form>
      </div>
    </Card>
  );
}

/**
 * One line telling the partner where verification as a whole stands. A
 * rejection outranks everything else — it is the only state that needs them to
 * do something.
 */
function VerificationSummary({ documents }: { documents: KycDocument[] }) {
  const rejected = documents.filter((doc) => doc.verificationStatus === "Rejected");
  const pending = documents.filter((doc) => doc.verificationStatus === "Pending");

  if (rejected.length > 0) {
    return (
      <Alert tone="error" title={rejected.length === 1 ? "A document was rejected" : "Documents were rejected"}>
        Send a clearer copy of{" "}
        {rejected.map((doc) => docTypeLabel(doc.docType)).join(", ")} using the form
        below. Verification can&apos;t finish until it&apos;s accepted.
      </Alert>
    );
  }

  if (pending.length > 0) {
    return (
      <Alert
        tone="info"
        title={pending.length === 1 ? "1 document under review" : `${pending.length} documents under review`}
      >
        We&apos;ll update this page as soon as they&apos;ve been checked. Nothing is needed from
        you meanwhile.
      </Alert>
    );
  }

  return (
    <Alert tone="success" title="All documents approved">
      Your paperwork is verified. Keep your availability and service areas current to get matched.
    </Alert>
  );
}

/**
 * One submitted document. Kept as a stacked row at every breakpoint rather
 * than a table: there are only ever a handful, and the status is the field
 * that matters, so it stays beside the type instead of at the end of a row
 * that has to be scrolled sideways on a phone.
 */
function DocumentRow({ doc, onResubmit }: { doc: KycDocument; onResubmit: () => void }) {
  const isRejected = doc.verificationStatus === "Rejected";

  return (
    <div
      className={cx(
        "rounded-xl border p-3.5",
        isRejected ? "border-danger/30 bg-danger-soft/40" : "border-line bg-surface-2",
      )}
    >
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="text-sm font-medium text-fg">{docTypeLabel(doc.docType)}</p>
          {doc.docNumber ? (
            <p className="nums mt-0.5 truncate text-xs text-fg-muted">{doc.docNumber}</p>
          ) : null}
        </div>
        <Badge tone={VERIFICATION_STATUS_TONES[doc.verificationStatus]}>
          {VERIFICATION_STATUS_LABELS[doc.verificationStatus] ?? doc.verificationStatus}
        </Badge>
      </div>

      <p className="nums mt-2 text-xs text-fg-subtle">
        Submitted {formatDate(doc.submittedAt)}
        {doc.verifiedAt ? ` · Reviewed ${formatDate(doc.verifiedAt)}` : ""}
      </p>

      {isRejected ? (
        <Button variant="secondary" size="sm" className="mt-3" onClick={onResubmit}>
          Send a new one
        </Button>
      ) : null}
    </div>
  );
}
