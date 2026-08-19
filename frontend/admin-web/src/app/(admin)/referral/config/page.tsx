"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { Controller, useForm } from "react-hook-form";
import type { UseFormReturn } from "react-hook-form";
import { z } from "zod";
import { FormActions, FormGrid, formatCurrency, formatDate } from "@/components/data-table";
import { EntityTable } from "@/components/entity-table";
import type { EntityTableColumn } from "@/components/entity-table";
import { DetailError, DetailSkeleton } from "@/components/screen-states";
import {
  Alert,
  Button,
  Card,
  CheckboxField,
  Field,
  Modal,
  PageHeading,
  Select,
  useToast,
} from "@/components/ui";
import { describeError } from "@/lib/api";
import { canWriteModule } from "@/lib/permissions";
import { useAdminClaims } from "@/lib/use-admin-claims";
import { ReferralTabs } from "../_components/ReferralTabs";
import {
  createReferralMilestone,
  getReferralConfig,
  listReferralMilestones,
  setReferralMilestoneActive,
  updateReferralConfig,
} from "../_lib/referral-api";
import { REFERRAL_REWARD_TYPE_LABELS, ReferralRewardType } from "../_lib/referral-types";
import type { ReferralMilestone, ReferralProgramConfig } from "../_lib/referral-types";

const REWARD_TYPE_OPTIONS = Object.entries(REFERRAL_REWARD_TYPE_LABELS).map(([value, label]) => ({
  value,
  label,
}));

/** Empty optional numeric control → `null`, matching CouponForm's handling of the same shape. */
function emptyStringToNull(value: string): number | null {
  return value === "" ? null : Number(value);
}

function nullableNumberToInputValue(value: number | null): string {
  return value === null || value === undefined ? "" : String(value);
}

/**
 * Mirrors `ReferralProgramConfigUpdateRequestValidator` so a value the backend
 * would reject never leaves the browser. The previous form pushed
 * `Number(e.target.value)` straight into state, so clearing a numeric box sent
 * `NaN` — which serialises to `null` — and the save failed with a server-side
 * message that named no field.
 */
const configSchema = z.object({
  referrerRewardType: z.number().int(),
  referrerRewardValue: z.number().positive("Referrer reward must be greater than 0"),
  refereeRewardType: z.number().int(),
  refereeRewardValue: z.number().positive("Referee reward must be greater than 0"),
  minQualifyingOrderAmount: z.number().min(0, "Minimum qualifying order cannot be negative"),
  referralExpiryDays: z.number().int("Whole days only").positive("Expiry must be at least 1 day"),
  maxReferralsPerCustomer: z.number().int().positive("The cap must be at least 1").nullable(),
  isActive: z.boolean(),
});
type ConfigFormValues = z.infer<typeof configSchema>;

/**
 * Nullable numeric field (a cap that means "unlimited" when unset), as a
 * `Controller`-bound field rather than `register(name, { setValueAs })` +
 * a manual `defaultValue` prop.
 *
 * `setValueAs` only runs inside the `onChange` handler `register` generates
 * - it transforms a value the admin actively types, but a field they never
 * touch keeps whatever the DOM `defaultValue` rendered it with (`""`, per
 * `nullableNumberToInputValue(null)`), and react-hook-form reads that raw
 * string at submit time. `z.number().positive().nullable()` rejects `""` as
 * neither a valid number nor `null`, so saving this config without first
 * touching an untouched-but-legitimately-blank "Max referrals per customer"
 * field ("Leave blank for unlimited") failed with "The cap must be at least
 * 1" on a field the admin never edited - the same bug already fixed for
 * `app/(admin)/settings/page.tsx`'s own nullable caps and
 * `coupons/_components/CouponForm.tsx`'s max-discount/usage-limit fields;
 * this is that identical fix applied here.
 */
function NullableNumberField({
  form,
  label,
  hint,
  disabled,
}: {
  form: UseFormReturn<ConfigFormValues>;
  label: string;
  hint?: string;
  disabled?: boolean;
}) {
  return (
    <Controller
      control={form.control}
      name="maxReferralsPerCustomer"
      render={({ field, fieldState }) => (
        <Field
          label={label}
          type="number"
          min={1}
          hint={hint}
          disabled={disabled}
          value={nullableNumberToInputValue(field.value)}
          onChange={(event) => field.onChange(emptyStringToNull(event.target.value))}
          onBlur={field.onBlur}
          error={fieldState.error?.message}
        />
      )}
    />
  );
}

const milestoneSchema = z.object({
  thresholdCount: z.number().int("Whole referrals only").positive("Threshold must be at least 1"),
  bonusType: z.number().int(),
  bonusValue: z.number().positive("Bonus value must be greater than 0"),
});
type MilestoneFormValues = z.infer<typeof milestoneSchema>;

function configValues(config: ReferralProgramConfig): ConfigFormValues {
  return {
    referrerRewardType: config.referrerRewardType,
    referrerRewardValue: config.referrerRewardValue,
    refereeRewardType: config.refereeRewardType,
    refereeRewardValue: config.refereeRewardValue,
    minQualifyingOrderAmount: config.minQualifyingOrderAmount,
    referralExpiryDays: config.referralExpiryDays,
    maxReferralsPerCustomer: config.maxReferralsPerCustomer,
    isActive: config.isActive,
  };
}

const EMPTY_MILESTONE: MilestoneFormValues = {
  thresholdCount: 5,
  bonusType: ReferralRewardType.WalletCredit,
  bonusValue: 100,
};

/**
 * Referral program config + milestone tiers (tasks 167, 174): reward values per
 * side, the qualifying order amount, expiry, the per-customer cap, and the
 * bonus tiers awarded at N successful referrals.
 */
export default function ReferralConfigPage() {
  const claims = useAdminClaims();
  const canWrite = canWriteModule(claims, "referral");

  const configQuery = useQuery({ queryKey: ["referral-config"], queryFn: getReferralConfig });
  const milestonesQuery = useQuery({ queryKey: ["referral-milestones"], queryFn: listReferralMilestones });

  return (
    <div className="mx-auto w-full max-w-5xl">
      <PageHeading
        title="Referral program"
        subtitle="Reward values per side, the qualifying order amount, expiry, and the milestone bonus tiers."
      />

      <ReferralTabs />

      <div className="flex flex-col gap-6">
        {configQuery.isPending ? (
          <DetailSkeleton cards={1} className="flex w-full flex-col gap-6" />
        ) : configQuery.error || !configQuery.data ? (
          <DetailError
            title="Program config"
            error={configQuery.error}
            message={configQuery.error ? undefined : "No referral program config has been created yet."}
            onRetry={() => configQuery.refetch()}
            className="w-full"
          />
        ) : (
          <ConfigForm config={configQuery.data} canWrite={canWrite} />
        )}

        <MilestonesSection
          milestones={milestonesQuery.data}
          isLoading={milestonesQuery.isPending}
          isFetching={milestonesQuery.isFetching}
          error={milestonesQuery.error}
          onRetry={() => milestonesQuery.refetch()}
          canWrite={canWrite}
        />
      </div>
    </div>
  );
}

function ConfigForm({ config, canWrite }: { config: ReferralProgramConfig; canWrite: boolean }) {
  const queryClient = useQueryClient();
  const pushToast = useToast();

  const form = useForm<ConfigFormValues>({
    resolver: zodResolver(configSchema),
    defaultValues: configValues(config),
  });

  // Re-seed only when the server copy has genuinely moved on (`updatedAtUtc`
  // changes on save and nothing else). The previous
  // `useEffect(() => setForm(config), [config])` fired on every refetch —
  // each one hands back a new object identity — so a background refetch part
  // way through editing silently discarded what had been typed.
  const savedAt = config.updatedAtUtc;
  useEffect(() => {
    form.reset(configValues(config));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [savedAt, config.id]);

  const updateMutation = useMutation({
    mutationFn: (values: ConfigFormValues) =>
      updateReferralConfig({
        referrerRewardType: values.referrerRewardType as ReferralRewardType,
        referrerRewardValue: values.referrerRewardValue,
        refereeRewardType: values.refereeRewardType as ReferralRewardType,
        refereeRewardValue: values.refereeRewardValue,
        minQualifyingOrderAmount: values.minQualifyingOrderAmount,
        referralExpiryDays: values.referralExpiryDays,
        maxReferralsPerCustomer: values.maxReferralsPerCustomer,
        isActive: values.isActive,
      }),
    onSuccess: (updated) => {
      // Seeding the cache rather than invalidating keeps the just-saved values
      // on screen without a second round trip.
      queryClient.setQueryData(["referral-config"], updated);
      pushToast("success", "Referral program config saved.");
    },
  });

  const isActive = form.watch("isActive");

  return (
    <Card
      title="Program config"
      description={
        canWrite
          ? `Last saved ${formatDate(config.updatedAtUtc)}.`
          : "Read-only — you do not hold referral write access."
      }
    >
      <form
        onSubmit={form.handleSubmit((values) => updateMutation.mutate(values))}
        className="flex flex-col gap-5"
        noValidate
      >
        {updateMutation.isError ? <Alert>{describeError(updateMutation.error)}</Alert> : null}

        <FormGrid>
          <Select
            label="Referrer reward type"
            options={REWARD_TYPE_OPTIONS}
            disabled={!canWrite}
            error={form.formState.errors.referrerRewardType?.message}
            {...form.register("referrerRewardType", { valueAsNumber: true })}
          />
          <Field
            label="Referrer reward value"
            type="number"
            step="0.01"
            min={0}
            required
            leading="₹"
            disabled={!canWrite}
            error={form.formState.errors.referrerRewardValue?.message}
            {...form.register("referrerRewardValue", { valueAsNumber: true })}
          />
          <Select
            label="Referee reward type"
            options={REWARD_TYPE_OPTIONS}
            disabled={!canWrite}
            error={form.formState.errors.refereeRewardType?.message}
            {...form.register("refereeRewardType", { valueAsNumber: true })}
          />
          <Field
            label="Referee reward value"
            type="number"
            step="0.01"
            min={0}
            required
            leading="₹"
            disabled={!canWrite}
            error={form.formState.errors.refereeRewardValue?.message}
            {...form.register("refereeRewardValue", { valueAsNumber: true })}
          />
        </FormGrid>

        <FormGrid columns={3}>
          <Field
            label="Minimum qualifying order"
            type="number"
            step="0.01"
            min={0}
            leading="₹"
            hint="A referee's booking must reach this to qualify."
            disabled={!canWrite}
            error={form.formState.errors.minQualifyingOrderAmount?.message}
            {...form.register("minQualifyingOrderAmount", { valueAsNumber: true })}
          />
          <Field
            label="Referral expiry (days)"
            type="number"
            min={1}
            required
            hint="How long a referral has to qualify."
            disabled={!canWrite}
            error={form.formState.errors.referralExpiryDays?.message}
            {...form.register("referralExpiryDays", { valueAsNumber: true })}
          />
          <NullableNumberField
            form={form}
            label="Max referrals per customer"
            hint="Leave blank for unlimited."
            disabled={!canWrite}
          />
        </FormGrid>

        <CheckboxField
          label="Program active"
          description="While this is off no new referral earns a reward. Referrals that already qualified are unaffected."
          checked={isActive}
          disabled={!canWrite}
          onChange={(checked) => form.setValue("isActive", checked, { shouldDirty: true })}
        />

        {canWrite ? (
          <FormActions>
            <Button type="submit" loading={updateMutation.isPending}>
              Save config
            </Button>
          </FormActions>
        ) : null}
      </form>
    </Card>
  );
}

function MilestonesSection({
  milestones,
  isLoading,
  isFetching,
  error,
  onRetry,
  canWrite,
}: {
  milestones: ReferralMilestone[] | undefined;
  isLoading: boolean;
  isFetching: boolean;
  error: unknown;
  onRetry: () => void;
  canWrite: boolean;
}) {
  const queryClient = useQueryClient();
  const pushToast = useToast();
  const [isAdding, setIsAdding] = useState(false);

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ["referral-milestones"] });

  const createMutation = useMutation({
    mutationFn: (values: MilestoneFormValues) =>
      createReferralMilestone({
        thresholdCount: values.thresholdCount,
        bonusType: values.bonusType as ReferralRewardType,
        bonusValue: values.bonusValue,
      }),
    onSuccess: () => {
      invalidate();
      setIsAdding(false);
      pushToast("success", "Milestone tier added.");
    },
  });

  const columns: readonly EntityTableColumn<ReferralMilestone>[] = [
    {
      header: "Threshold",
      numeric: true,
      render: (milestone) => `${milestone.thresholdCount.toLocaleString("en-IN")} referrals`,
      sortValue: (milestone) => milestone.thresholdCount,
    },
    {
      header: "Bonus",
      numeric: true,
      render: (milestone) => formatCurrency(milestone.bonusValue),
      sortValue: (milestone) => milestone.bonusValue,
    },
    {
      header: "Paid as",
      render: (milestone) => REFERRAL_REWARD_TYPE_LABELS[milestone.bonusType] ?? "—",
      sortValue: (milestone) => milestone.bonusType,
    },
    {
      header: "Created",
      render: (milestone) => <span className="nums whitespace-nowrap">{formatDate(milestone.createdAtUtc)}</span>,
      sortValue: (milestone) => milestone.createdAtUtc,
    },
  ];

  const toggleMutation = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => setReferralMilestoneActive(id, isActive),
    onSuccess: invalidate,
  });

  return (
    <>
      <EntityTable<ReferralMilestone>
        title="Milestone tiers"
        description="A one-off bonus paid to a referrer the first time they reach each threshold."
        items={milestones}
        columns={columns}
        isLoading={isLoading}
        isFetching={isFetching}
        error={error}
        onRetry={onRetry}
        emptyMessage="No milestone tiers configured"
        emptyAction={canWrite ? <Button onClick={() => setIsAdding(true)}>Add a tier</Button> : undefined}
        canWrite={canWrite}
        entityLabel="milestone tier"
        labelOf={(milestone) => `${milestone.thresholdCount} referrals → ${formatCurrency(milestone.bonusValue)}`}
        onToggleActive={(milestone) => toggleMutation.mutate({ id: milestone.id, isActive: !milestone.isActive })}
        // Only the row being toggled is disabled — this used to disable every
        // row's button for the duration of any one toggle.
        togglingId={toggleMutation.isPending ? toggleMutation.variables?.id : undefined}
        toggleError={toggleMutation.error}
        minWidth="720px"
        skeletonRows={3}
        actions={
          canWrite ? (
            <Button size="sm" onClick={() => setIsAdding(true)}>
              Add tier
            </Button>
          ) : undefined
        }
      />

      <AddMilestoneModal
        open={isAdding}
        isSubmitting={createMutation.isPending}
        error={createMutation.error}
        onSubmit={(values) => createMutation.mutate(values)}
        onClose={() => {
          // Closing mid-request would leave the mutation running against a
          // dialog that is no longer showing its outcome.
          if (createMutation.isPending) return;
          createMutation.reset();
          setIsAdding(false);
        }}
      />
    </>
  );
}

function AddMilestoneModal({
  open,
  isSubmitting,
  error,
  onSubmit,
  onClose,
}: {
  open: boolean;
  isSubmitting: boolean;
  error: unknown;
  onSubmit: (values: MilestoneFormValues) => void;
  onClose: () => void;
}) {
  const form = useForm<MilestoneFormValues>({
    resolver: zodResolver(milestoneSchema),
    defaultValues: EMPTY_MILESTONE,
  });

  // The dialog stays mounted while closed, so without this a second "Add tier"
  // would open on the previous attempt's values and errors.
  useEffect(() => {
    if (open) form.reset(EMPTY_MILESTONE);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  return (
    <Modal
      open={open}
      onClose={onClose}
      title="Add a milestone tier"
      description="Awarded once, the first time a referrer's successful referral count reaches the threshold."
      footer={
        <>
          <Button type="button" variant="secondary" onClick={onClose} disabled={isSubmitting}>
            Cancel
          </Button>
          <Button type="submit" form="add-milestone-form" loading={isSubmitting}>
            Add tier
          </Button>
        </>
      }
    >
      <form id="add-milestone-form" onSubmit={form.handleSubmit(onSubmit)} className="flex flex-col gap-4" noValidate>
        {error ? <Alert>{describeError(error)}</Alert> : null}

        <FormGrid columns={1}>
          <Field
            label="Threshold"
            type="number"
            min={1}
            required
            hint="Successful referrals needed to earn this bonus."
            error={form.formState.errors.thresholdCount?.message}
            {...form.register("thresholdCount", { valueAsNumber: true })}
          />
          <Select
            label="Bonus type"
            options={REWARD_TYPE_OPTIONS}
            error={form.formState.errors.bonusType?.message}
            {...form.register("bonusType", { valueAsNumber: true })}
          />
          <Field
            label="Bonus value"
            type="number"
            step="0.01"
            min={0}
            required
            leading="₹"
            error={form.formState.errors.bonusValue?.message}
            {...form.register("bonusValue", { valueAsNumber: true })}
          />
        </FormGrid>
      </form>
    </Modal>
  );
}
