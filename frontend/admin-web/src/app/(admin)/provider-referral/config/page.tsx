"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect } from "react";
import { Controller, useForm } from "react-hook-form";
import type { UseFormReturn } from "react-hook-form";
import { z } from "zod";
import { FormActions, FormGrid, formatDate } from "@/components/data-table";
import { DetailError, DetailSkeleton } from "@/components/screen-states";
import { Alert, Button, Card, CheckboxField, Field, PageHeading, useToast } from "@/components/ui";
import { describeError } from "@/lib/api";
import { canWriteModule } from "@/lib/permissions";
import { useAdminClaims } from "@/lib/use-admin-claims";
import { ProviderReferralTabs } from "../_components/ProviderReferralTabs";
import { getProviderReferralConfig, updateProviderReferralConfig } from "../_lib/provider-referral-api";
import type { ProviderReferralProgramConfig } from "../_lib/provider-referral-types";

/** Empty optional numeric control → `null`, mirrors (admin)/referral/config/page.tsx's identical handling. */
function emptyStringToNull(value: string): number | null {
  return value === "" ? null : Number(value);
}

function nullableNumberToInputValue(value: number | null): string {
  return value === null || value === undefined ? "" : String(value);
}

/** Mirrors `ProviderReferralProgramConfigUpdateRequestValidator` so a value the backend would reject never leaves the browser. */
const configSchema = z.object({
  referrerRewardValue: z.number().positive("Referrer reward must be greater than 0"),
  refereeRewardValue: z.number().positive("Referee reward must be greater than 0"),
  qualifyingCompletedJobsCount: z.number().int("Whole jobs only").positive("Must be at least 1 completed job"),
  referralExpiryDays: z.number().int("Whole days only").positive("Expiry must be at least 1 day"),
  maxReferralsPerProvider: z.number().int().positive("The cap must be at least 1").nullable(),
  isActive: z.boolean(),
});
type ConfigFormValues = z.infer<typeof configSchema>;

/** Nullable numeric field (a cap that means "unlimited" when unset), mirrors (admin)/referral/config/page.tsx's NullableNumberField. */
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
      name="maxReferralsPerProvider"
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

function configValues(config: ProviderReferralProgramConfig): ConfigFormValues {
  return {
    referrerRewardValue: config.referrerRewardValue,
    refereeRewardValue: config.refereeRewardValue,
    qualifyingCompletedJobsCount: config.qualifyingCompletedJobsCount,
    referralExpiryDays: config.referralExpiryDays,
    maxReferralsPerProvider: config.maxReferralsPerProvider,
    isActive: config.isActive,
  };
}

/**
 * Provider referral program config (PROVIDER-REFERRAL.md): reward values per
 * side, the qualifying completed-job count, expiry, and the per-provider cap.
 * Mirrors (admin)/referral/config/page.tsx, minus the reward-type selects
 * (providers only ever earn via the earning ledger, no coupon option) and
 * the milestone-tier section (not included in this v1).
 */
export default function ProviderReferralConfigPage() {
  const claims = useAdminClaims();
  const canWrite = canWriteModule(claims, "provider-referral");

  const configQuery = useQuery({ queryKey: ["provider-referral-config"], queryFn: getProviderReferralConfig });

  return (
    <div className="mx-auto w-full max-w-5xl">
      <PageHeading
        title="Provider referral program"
        subtitle="Reward values per side, the qualifying completed-job count, and expiry."
      />

      <ProviderReferralTabs />

      <div className="flex flex-col gap-6">
        {configQuery.isPending ? (
          <DetailSkeleton cards={1} className="flex w-full flex-col gap-6" />
        ) : configQuery.error || !configQuery.data ? (
          <DetailError
            title="Program config"
            error={configQuery.error}
            message={configQuery.error ? undefined : "No provider referral program config has been created yet."}
            onRetry={() => configQuery.refetch()}
            className="w-full"
          />
        ) : (
          <ConfigForm config={configQuery.data} canWrite={canWrite} />
        )}
      </div>
    </div>
  );
}

function ConfigForm({ config, canWrite }: { config: ProviderReferralProgramConfig; canWrite: boolean }) {
  const queryClient = useQueryClient();
  const pushToast = useToast();

  const form = useForm<ConfigFormValues>({
    resolver: zodResolver(configSchema),
    defaultValues: configValues(config),
  });

  // Re-seed only when the server copy has genuinely moved on (`updatedAtUtc`
  // changes on save and nothing else) - mirrors (admin)/referral/config/page.tsx's
  // identical fix for a background refetch discarding an in-progress edit.
  const savedAt = config.updatedAtUtc;
  useEffect(() => {
    form.reset(configValues(config));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [savedAt, config.id]);

  const updateMutation = useMutation({
    mutationFn: (values: ConfigFormValues) =>
      updateProviderReferralConfig({
        referrerRewardValue: values.referrerRewardValue,
        refereeRewardValue: values.refereeRewardValue,
        qualifyingCompletedJobsCount: values.qualifyingCompletedJobsCount,
        referralExpiryDays: values.referralExpiryDays,
        maxReferralsPerProvider: values.maxReferralsPerProvider,
        isActive: values.isActive,
      }),
    onSuccess: (updated) => {
      queryClient.setQueryData(["provider-referral-config"], updated);
      pushToast("success", "Provider referral program config saved.");
    },
  });

  const isActive = form.watch("isActive");

  return (
    <Card
      title="Program config"
      description={
        canWrite
          ? `Last saved ${formatDate(config.updatedAtUtc)}.`
          : "Read-only — you do not hold provider-referral write access."
      }
    >
      <form
        onSubmit={form.handleSubmit((values) => updateMutation.mutate(values))}
        className="flex flex-col gap-5"
        noValidate
      >
        {updateMutation.isError ? <Alert>{describeError(updateMutation.error)}</Alert> : null}

        <FormGrid>
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
            label="Qualifying completed jobs"
            type="number"
            min={1}
            required
            hint="How many jobs the referee must complete to qualify."
            disabled={!canWrite}
            error={form.formState.errors.qualifyingCompletedJobsCount?.message}
            {...form.register("qualifyingCompletedJobsCount", { valueAsNumber: true })}
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
            label="Max referrals per provider"
            hint="Leave blank for unlimited."
            disabled={!canWrite}
          />
        </FormGrid>

        <CheckboxField
          label="Program active"
          description="While this is off no new provider referral earns a reward. Referrals that already qualified are unaffected."
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
