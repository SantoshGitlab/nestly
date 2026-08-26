"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useQuery, useQueryClient, type QueryClient } from "@tanstack/react-query";
import { useState, type ComponentProps, type ReactNode } from "react";
import {
  Controller,
  useForm,
  type FieldValues,
  type Path,
  type Resolver,
  type UseFormReturn,
} from "react-hook-form";
import { z } from "zod";
import { Alert, Button, Card, CheckboxField, Field, PageHeading, Skeleton, useToast } from "@/components/ui";
import { FormActions, FormGrid } from "@/components/data-table";
import { SectionError } from "@/components/screen-states";
import { apiFetch, describeError } from "@/lib/api";
import { canWriteModule } from "@/lib/permissions";
import { useAdminClaims } from "@/lib/use-admin-claims";
import type {
  AllSystemSettingsResponse,
  BookingSettings,
  CancellationSettings,
  CouponSettings,
  RescheduleSettings,
  SlotSettings,
  TaxSettings,
  WalletSettings,
} from "@/lib/settings-types";

/**
 * System configuration / feature-flag management (SRS 12.19, tasks
 * 131a-131h): one card per settings group, each independently readable and
 * editable. Gated server-side behind "settings.read"/"settings.write"
 * (SystemSettingsController) - like every other admin page, this route's own
 * rendering is not the security boundary (see RequireAdminAuth's doc
 * comment); a 403 from the API surfaces as an inline alert per card.
 */
const SETTINGS_PATH = "/api/v1/settings";

/** react-hook-form's `register(name, { setValueAs })` helper for an optional numeric field: blank input means "unlimited"/"never" (null), matching the backend's nullable-cap convention. */
function emptyStringToNull(value: string): number | null {
  return value === "" ? null : Number(value);
}

function nullableNumberToInputValue(value: number | null): string {
  return value === null || value === undefined ? "" : String(value);
}

/**
 * Nullable numeric setting (a cap that's "unlimited" when unset), as a
 * `Controller`-bound field rather than `register(name, { setValueAs })` on a
 * plain uncontrolled input.
 *
 * `setValueAs` only runs inside the `onChange` handler `register` generates
 * - it transforms a value the admin actively types, but a field they never
 * touch keeps whatever `defaultValue` rendered the input with: `""` for
 * every one of these fields today, since they're all currently unset
 * (`null`) in the database. `z.number().nullable()` rejects that empty
 * string as neither a number nor null, so submitting the group without
 * first touching this one unrelated field silently failed client
 * validation - the whole card (every other field in it too) never reached
 * the API, with only a one-line "Invalid input" under this field as any
 * sign why. `Controller` keeps the field's RHF-tracked value correctly
 * typed (`number | null`) from the first render, whether the admin edits it
 * or not, so the untouched/blank/"unlimited" case - the actual current
 * state of every field this wraps - validates and saves like any other.
 */
function NullableNumberField<T extends FieldValues>({
  form,
  name,
  label,
}: {
  form: UseFormReturn<T>;
  name: Path<T>;
  label: string;
}) {
  return (
    <Controller
      control={form.control}
      name={name}
      render={({ field, fieldState }) => (
        <Field
          label={label}
          type="number"
          value={nullableNumberToInputValue(field.value as number | null)}
          onChange={(event) => field.onChange(emptyStringToNull(event.target.value))}
          onBlur={field.onBlur}
          error={fieldState.error?.message}
        />
      )}
    />
  );
}

/** Same fix as {@link NullableNumberField}, for the one nullable *text* setting (tax registration number). */
function NullableTextField<T extends FieldValues>({
  form,
  name,
  label,
}: {
  form: UseFormReturn<T>;
  name: Path<T>;
  label: string;
}) {
  return (
    <Controller
      control={form.control}
      name={name}
      render={({ field, fieldState }) => (
        <Field
          label={label}
          type="text"
          value={(field.value as string | null) ?? ""}
          onChange={(event) => field.onChange(event.target.value === "" ? null : event.target.value)}
          onBlur={field.onBlur}
          error={fieldState.error?.message}
        />
      )}
    />
  );
}

/**
 * A boolean setting spans the whole field grid. A boxed toggle carrying its
 * own explanation reads badly squeezed into one half-width column next to a
 * numeric input, and the feature flags here are the highest-consequence
 * controls on the page.
 */
function ToggleRow(props: ComponentProps<typeof CheckboxField>) {
  return (
    <div className="sm:col-span-2">
      <CheckboxField {...props} />
    </div>
  );
}

function useAllSettings() {
  return useQuery({
    queryKey: ["settings", "all"] as const,
    queryFn: () => apiFetch<AllSystemSettingsResponse>(SETTINGS_PATH, { authenticated: true }),
  });
}

function updateSettingsCache<K extends keyof AllSystemSettingsResponse>(
  queryClient: QueryClient,
  groupKey: K,
  value: AllSystemSettingsResponse[K],
) {
  queryClient.setQueryData<AllSystemSettingsResponse>(["settings", "all"], (current) =>
    current ? { ...current, [groupKey]: value } : current,
  );
}

/**
 * Shared card/form/save-state scaffolding for one settings group. The field
 * inputs themselves (which differ per group) are supplied via `children` as
 * a render-prop over the group's own `react-hook-form` instance - keeps the
 * seven groups from needing seven near-identical copies of the submit/error/
 * success wiring, while still letting each group's field list stay a plain,
 * explicit list rather than a generic metadata-driven one.
 */
function SettingsGroupCard<T extends FieldValues>({
  title,
  description,
  groupPath,
  schema,
  defaultValues,
  onSaved,
  canWrite,
  children,
}: {
  title: string;
  description: string;
  groupPath: string;
  schema: z.ZodType<T>;
  defaultValues: T;
  onSaved: (value: T) => void;
  /** Gates every field and the submit button - settings.write, checked once at the page level. */
  canWrite: boolean;
  children: (form: UseFormReturn<T>) => ReactNode;
}) {
  const toast = useToast();
  const [error, setError] = useState<string | null>(null);
  const form = useForm<T>({
    resolver: zodResolver(schema as never) as unknown as Resolver<T>,
    defaultValues: defaultValues as never,
  });

  const onSubmit = form.handleSubmit(async (values) => {
    setError(null);
    try {
      const updated = await apiFetch<T>(`${SETTINGS_PATH}/${groupPath}`, {
        method: "PUT",
        authenticated: true,
        body: JSON.stringify(values),
      });
      // `reset` to the server's echo, not the submitted values: it clears the
      // dirty state and makes the card show what was actually persisted.
      form.reset(updated);
      onSaved(updated);
      // A toast rather than a persistent inline alert. The old "Saved." alert
      // had no way to clear itself, so it stayed under a card the admin had
      // since edited again, asserting a save that had not happened.
      toast("success", `${title} saved.`);
    } catch (err) {
      // The form keeps its values, so a rejected save can be corrected and
      // resubmitted rather than retyped.
      setError(describeError(err));
    }
  });

  return (
    <Card
      title={title}
      description={canWrite ? description : "Read-only — you do not hold settings write access."}
    >
      <form onSubmit={onSubmit} className="flex flex-col gap-5" noValidate>
        {error ? <Alert>{error}</Alert> : null}
        {/* A native <fieldset disabled> cascades to every descendant input/
            select/button in one place, rather than threading a `disabled`
            prop into each of the seven groups' own differently-shaped field
            lists (plain Fields, Controller-wrapped nullable fields, toggle
            rows) - this page previously had no write-gating at all, unlike
            every other module screen in this app. */}
        <fieldset disabled={!canWrite} className="contents">
          <FormGrid columns={2}>{children(form)}</FormGrid>
          {canWrite ? (
            <FormActions>
              <Button type="submit" loading={form.formState.isSubmitting}>
                Save changes
              </Button>
            </FormActions>
          ) : null}
        </fieldset>
      </form>
    </Card>
  );
}

const bookingSchema = z.object({
  minLeadTimeHours: z.number().int().min(0).max(720),
  maxAdvanceBookingDays: z.number().int().min(1).max(365),
  maxActiveBookingsPerCustomer: z.number().int().min(1).max(1000).nullable(),
  allowSameDayBooking: z.boolean(),
});

function BookingSettingsSection({ initial, queryClient, canWrite }: { initial: BookingSettings; queryClient: QueryClient; canWrite: boolean }) {
  return (
    <SettingsGroupCard<BookingSettings>
      title="Booking rules"
      description="How far ahead, and how close to a slot, a booking may be created (SRS 12.19)."
      groupPath="booking"
      schema={bookingSchema}
      defaultValues={initial}
      onSaved={(value) => updateSettingsCache(queryClient, "booking", value)}
      canWrite={canWrite}
    >
      {(form) => (
        <>
          <Field
            label="Minimum lead time (hours)"
            type="number"
            error={form.formState.errors.minLeadTimeHours?.message}
            {...form.register("minLeadTimeHours", { valueAsNumber: true })}
          />
          <Field
            label="Max advance booking (days)"
            type="number"
            error={form.formState.errors.maxAdvanceBookingDays?.message}
            {...form.register("maxAdvanceBookingDays", { valueAsNumber: true })}
          />
          <NullableNumberField
            form={form}
            name="maxActiveBookingsPerCustomer"
            label="Max active bookings per customer (blank = unlimited)"
          />
          <Controller
            control={form.control}
            name="allowSameDayBooking"
            render={({ field }) => (
              <ToggleRow label="Allow same-day booking" checked={field.value} onChange={field.onChange} />
            )}
          />
        </>
      )}
    </SettingsGroupCard>
  );
}

const slotSchema = z.object({
  defaultSlotDurationMinutes: z.number().int().min(15).max(480),
  sameDayCutoffHours: z.number().int().min(0).max(24),
  maxAdvanceBookingDays: z.number().int().min(1).max(365),
  defaultSlotCapacity: z.number().int().min(1).max(1000),
  allowOverbooking: z.boolean(),
});

function SlotSettingsSection({ initial, queryClient, canWrite }: { initial: SlotSettings; queryClient: QueryClient; canWrite: boolean }) {
  return (
    <SettingsGroupCard<SlotSettings>
      title="Slot rules"
      description="How slots are generated and offered: duration, same-day cutoff, and capacity (SRS 15.2)."
      groupPath="slot"
      schema={slotSchema}
      defaultValues={initial}
      onSaved={(value) => updateSettingsCache(queryClient, "slot", value)}
      canWrite={canWrite}
    >
      {(form) => (
        <>
          <Field
            label="Default slot duration (minutes)"
            type="number"
            error={form.formState.errors.defaultSlotDurationMinutes?.message}
            {...form.register("defaultSlotDurationMinutes", { valueAsNumber: true })}
          />
          <Field
            label="Same-day cutoff (hours)"
            type="number"
            error={form.formState.errors.sameDayCutoffHours?.message}
            {...form.register("sameDayCutoffHours", { valueAsNumber: true })}
          />
          <Field
            label="Max advance booking (days)"
            type="number"
            error={form.formState.errors.maxAdvanceBookingDays?.message}
            {...form.register("maxAdvanceBookingDays", { valueAsNumber: true })}
          />
          <Field
            label="Default slot capacity"
            type="number"
            error={form.formState.errors.defaultSlotCapacity?.message}
            {...form.register("defaultSlotCapacity", { valueAsNumber: true })}
          />
          <Controller
            control={form.control}
            name="allowOverbooking"
            render={({ field }) => (
              <ToggleRow label="Allow overbooking" checked={field.value} onChange={field.onChange} />
            )}
          />
        </>
      )}
    </SettingsGroupCard>
  );
}

const cancellationSchema = z.object({
  freeCancellationWindowHours: z.number().min(0).max(720),
  lateCancellationFeePercentage: z.number().min(0).max(100),
  allowAdminOverride: z.boolean(),
});

function CancellationSettingsSection({ initial, queryClient, canWrite }: { initial: CancellationSettings; queryClient: QueryClient; canWrite: boolean }) {
  return (
    <SettingsGroupCard<CancellationSettings>
      title="Cancellation policy"
      description="Free cancellation window and the late-cancellation fee (SRS 11.14.1)."
      groupPath="cancellation"
      schema={cancellationSchema}
      defaultValues={initial}
      onSaved={(value) => updateSettingsCache(queryClient, "cancellation", value)}
      canWrite={canWrite}
    >
      {(form) => (
        <>
          <Field
            label="Free cancellation window (hours)"
            type="number"
            step="0.5"
            error={form.formState.errors.freeCancellationWindowHours?.message}
            {...form.register("freeCancellationWindowHours", { valueAsNumber: true })}
          />
          <Field
            label="Late cancellation fee (%)"
            type="number"
            step="0.5"
            error={form.formState.errors.lateCancellationFeePercentage?.message}
            {...form.register("lateCancellationFeePercentage", { valueAsNumber: true })}
          />
          <Controller
            control={form.control}
            name="allowAdminOverride"
            render={({ field }) => (
              <ToggleRow label="Allow admin override of the late fee" checked={field.value} onChange={field.onChange} />
            )}
          />
        </>
      )}
    </SettingsGroupCard>
  );
}

const rescheduleSchema = z.object({
  minHoursBeforeSlot: z.number().min(0).max(720),
  maxReschedulesPerBooking: z.number().int().min(0).max(50),
  lateFeeThresholdHours: z.number().min(0).max(720),
  lateRescheduleFeePercentage: z.number().min(0).max(100),
});

function RescheduleSettingsSection({ initial, queryClient, canWrite }: { initial: RescheduleSettings; queryClient: QueryClient; canWrite: boolean }) {
  return (
    <SettingsGroupCard<RescheduleSettings>
      title="Reschedule policy"
      description="Reschedule blocking window, count limit, and the late-reschedule fee (SRS 11.15.1)."
      groupPath="reschedule"
      schema={rescheduleSchema}
      defaultValues={initial}
      onSaved={(value) => updateSettingsCache(queryClient, "reschedule", value)}
      canWrite={canWrite}
    >
      {(form) => (
        <>
          <Field
            label="Blocked within (hours before slot)"
            type="number"
            step="0.5"
            error={form.formState.errors.minHoursBeforeSlot?.message}
            {...form.register("minHoursBeforeSlot", { valueAsNumber: true })}
          />
          <Field
            label="Max reschedules per booking"
            type="number"
            error={form.formState.errors.maxReschedulesPerBooking?.message}
            {...form.register("maxReschedulesPerBooking", { valueAsNumber: true })}
          />
          <Field
            label="Late fee threshold (hours before slot)"
            type="number"
            step="0.5"
            error={form.formState.errors.lateFeeThresholdHours?.message}
            {...form.register("lateFeeThresholdHours", { valueAsNumber: true })}
          />
          <Field
            label="Late reschedule fee (%)"
            type="number"
            step="0.5"
            error={form.formState.errors.lateRescheduleFeePercentage?.message}
            {...form.register("lateRescheduleFeePercentage", { valueAsNumber: true })}
          />
        </>
      )}
    </SettingsGroupCard>
  );
}

const taxSchema = z.object({
  defaultTaxPercentage: z.number().min(0).max(100),
  taxRegistrationNumber: z.string().max(50).nullable(),
  taxInclusivePricing: z.boolean(),
});

function TaxSettingsSection({ initial, queryClient, canWrite }: { initial: TaxSettings; queryClient: QueryClient; canWrite: boolean }) {
  return (
    <SettingsGroupCard<TaxSettings>
      title="Tax settings"
      description="Default tax rate and registration details shown on customer invoices."
      groupPath="tax"
      schema={taxSchema}
      defaultValues={initial}
      onSaved={(value) => updateSettingsCache(queryClient, "tax", value)}
      canWrite={canWrite}
    >
      {(form) => (
        <>
          <Field
            label="Default tax rate (%)"
            type="number"
            step="0.01"
            error={form.formState.errors.defaultTaxPercentage?.message}
            {...form.register("defaultTaxPercentage", { valueAsNumber: true })}
          />
          <NullableTextField
            form={form}
            name="taxRegistrationNumber"
            label="Tax registration number (blank = not configured)"
          />
          <Controller
            control={form.control}
            name="taxInclusivePricing"
            render={({ field }) => (
              <ToggleRow label="Displayed prices already include tax" checked={field.value} onChange={field.onChange} />
            )}
          />
        </>
      )}
    </SettingsGroupCard>
  );
}

const walletSchema = z.object({
  maxWalletBalance: z.number().min(0),
  maxWalletUsagePercentagePerBooking: z.number().min(0).max(100),
  walletCreditExpiryDays: z.number().int().min(1).max(3650).nullable(),
  allowWalletTopUp: z.boolean(),
});

function WalletSettingsSection({ initial, queryClient, canWrite }: { initial: WalletSettings; queryClient: QueryClient; canWrite: boolean }) {
  return (
    <SettingsGroupCard<WalletSettings>
      title="Wallet settings"
      description="Balance cap, how much of a booking wallet may cover, and credit expiry (SRS 14.5)."
      groupPath="wallet"
      schema={walletSchema}
      defaultValues={initial}
      onSaved={(value) => updateSettingsCache(queryClient, "wallet", value)}
      canWrite={canWrite}
    >
      {(form) => (
        <>
          <Field
            label="Max wallet balance"
            type="number"
            step="0.01"
            error={form.formState.errors.maxWalletBalance?.message}
            {...form.register("maxWalletBalance", { valueAsNumber: true })}
          />
          <Field
            label="Max wallet usage per booking (%)"
            type="number"
            step="0.5"
            error={form.formState.errors.maxWalletUsagePercentagePerBooking?.message}
            {...form.register("maxWalletUsagePercentagePerBooking", { valueAsNumber: true })}
          />
          <NullableNumberField
            form={form}
            name="walletCreditExpiryDays"
            label="Wallet credit expiry (days, blank = never)"
          />
          <Controller
            control={form.control}
            name="allowWalletTopUp"
            render={({ field }) => (
              <ToggleRow label="Allow customers to top up their wallet directly" checked={field.value} onChange={field.onChange} />
            )}
          />
        </>
      )}
    </SettingsGroupCard>
  );
}

const couponSchema = z.object({
  maxDiscountPercentagePerCoupon: z.number().min(0).max(100),
  maxActiveCouponsPerCustomer: z.number().int().min(1).max(100).nullable(),
  allowCouponStacking: z.boolean(),
  couponsEnabled: z.boolean(),
});

function CouponSettingsSection({ initial, queryClient, canWrite }: { initial: CouponSettings; queryClient: QueryClient; canWrite: boolean }) {
  return (
    <SettingsGroupCard<CouponSettings>
      title="Coupon settings"
      description="Platform-wide guardrails applied across every coupon, plus the coupons feature flag (SRS 14.2)."
      groupPath="coupon"
      schema={couponSchema}
      defaultValues={initial}
      onSaved={(value) => updateSettingsCache(queryClient, "coupon", value)}
      canWrite={canWrite}
    >
      {(form) => (
        <>
          <Field
            label="Max discount per coupon (%)"
            type="number"
            step="0.5"
            error={form.formState.errors.maxDiscountPercentagePerCoupon?.message}
            {...form.register("maxDiscountPercentagePerCoupon", { valueAsNumber: true })}
          />
          <NullableNumberField
            form={form}
            name="maxActiveCouponsPerCustomer"
            label="Max active coupons per customer (blank = unlimited)"
          />
          <Controller
            control={form.control}
            name="allowCouponStacking"
            render={({ field }) => (
              <ToggleRow label="Allow more than one coupon per booking" checked={field.value} onChange={field.onChange} />
            )}
          />
          <Controller
            control={form.control}
            name="couponsEnabled"
            render={({ field }) => (
              <ToggleRow
                label="Coupons enabled"
                description="Feature flag: turning this off disables coupon redemption everywhere, regardless of individual coupon state."
                checked={field.value}
                onChange={field.onChange}
              />
            )}
          />
        </>
      )}
    </SettingsGroupCard>
  );
}

/**
 * Shaped like a real settings card — header, a two-column field grid, a
 * button row — so the page does not reflow when the settings land.
 */
function SettingsCardSkeleton({ fields = 4 }: { fields?: number }) {
  return (
    <div className="rounded-2xl bg-surface p-6 shadow-sm">
      <Skeleton className="h-4 w-40" />
      <Skeleton className="mt-2 h-3.5 w-72 max-w-full" />
      <div className="mt-6 grid grid-cols-1 gap-4 sm:grid-cols-2">
        {Array.from({ length: fields }, (_, index) => (
          <div key={index} className="flex flex-col gap-2">
            <Skeleton className="h-3.5 w-40" />
            <Skeleton className="h-10 w-full" />
          </div>
        ))}
      </div>
      <div className="mt-5 flex justify-end">
        <Skeleton className="h-10 w-32" />
      </div>
    </div>
  );
}

export default function SystemSettingsPage() {
  const queryClient = useQueryClient();
  const claims = useAdminClaims();
  const canWrite = canWriteModule(claims, "settings");
  const { data, isPending, isError, error, refetch } = useAllSettings();

  return (
    <div className="flex w-full max-w-4xl animate-rise flex-col gap-6">
      <PageHeading
        title="System settings"
        subtitle="Admin-configurable booking, slot, cancellation, reschedule, tax, wallet and coupon rules (SRS 12.19)."
      />

      {isPending ? (
        <>
          <SettingsCardSkeleton fields={4} />
          <SettingsCardSkeleton fields={5} />
          <SettingsCardSkeleton fields={3} />
        </>
      ) : isError ? (
        // Previously a bare Alert with no way out: a transient failure left
        // the whole screen empty until the admin reloaded the browser.
        <SectionError error={error} onRetry={() => void refetch()} />
      ) : (
        <>
          <BookingSettingsSection initial={data.booking} queryClient={queryClient} canWrite={canWrite} />
          <SlotSettingsSection initial={data.slot} queryClient={queryClient} canWrite={canWrite} />
          <CancellationSettingsSection initial={data.cancellation} queryClient={queryClient} canWrite={canWrite} />
          <RescheduleSettingsSection initial={data.reschedule} queryClient={queryClient} canWrite={canWrite} />
          <TaxSettingsSection initial={data.tax} queryClient={queryClient} canWrite={canWrite} />
          <WalletSettingsSection initial={data.wallet} queryClient={queryClient} canWrite={canWrite} />
          <CouponSettingsSection initial={data.coupon} queryClient={queryClient} canWrite={canWrite} />
        </>
      )}
    </div>
  );
}
