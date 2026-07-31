"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useQuery, useQueryClient, type QueryClient } from "@tanstack/react-query";
import { useState, type ReactNode } from "react";
import { Controller, useForm, type FieldValues, type Resolver, type UseFormReturn } from "react-hook-form";
import { z } from "zod";
import { Alert, Button, Card, CheckboxField, Field, PageHeading } from "@/components/ui";
import { apiFetch, describeError } from "@/lib/api";
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
  children,
}: {
  title: string;
  description: string;
  groupPath: string;
  schema: z.ZodType<T>;
  defaultValues: T;
  onSaved: (value: T) => void;
  children: (form: UseFormReturn<T>) => ReactNode;
}) {
  const [error, setError] = useState<string | null>(null);
  const [justSaved, setJustSaved] = useState(false);
  const form = useForm<T>({
    resolver: zodResolver(schema as never) as unknown as Resolver<T>,
    defaultValues: defaultValues as never,
  });

  const onSubmit = form.handleSubmit(async (values) => {
    setError(null);
    setJustSaved(false);
    try {
      const updated = await apiFetch<T>(`${SETTINGS_PATH}/${groupPath}`, {
        method: "PUT",
        authenticated: true,
        body: JSON.stringify(values),
      });
      form.reset(updated);
      onSaved(updated);
      setJustSaved(true);
    } catch (err) {
      setError(describeError(err));
    }
  });

  return (
    <Card title={title} description={description}>
      <form onSubmit={onSubmit} className="flex flex-col gap-4" noValidate>
        {error ? <Alert>{error}</Alert> : null}
        {justSaved && !error ? <Alert tone="success">Saved.</Alert> : null}
        {children(form)}
        <div>
          <Button type="submit" disabled={form.formState.isSubmitting}>
            {form.formState.isSubmitting ? "Saving…" : "Save changes"}
          </Button>
        </div>
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

function BookingSettingsSection({ initial, queryClient }: { initial: BookingSettings; queryClient: QueryClient }) {
  return (
    <SettingsGroupCard<BookingSettings>
      title="Booking rules"
      description="How far ahead, and how close to a slot, a booking may be created (SRS 12.19)."
      groupPath="booking"
      schema={bookingSchema}
      defaultValues={initial}
      onSaved={(value) => updateSettingsCache(queryClient, "booking", value)}
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
          <Field
            label="Max active bookings per customer (blank = unlimited)"
            type="number"
            defaultValue={nullableNumberToInputValue(initial.maxActiveBookingsPerCustomer)}
            error={form.formState.errors.maxActiveBookingsPerCustomer?.message}
            {...form.register("maxActiveBookingsPerCustomer", { setValueAs: emptyStringToNull })}
          />
          <Controller
            control={form.control}
            name="allowSameDayBooking"
            render={({ field }) => (
              <CheckboxField label="Allow same-day booking" checked={field.value} onChange={field.onChange} />
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

function SlotSettingsSection({ initial, queryClient }: { initial: SlotSettings; queryClient: QueryClient }) {
  return (
    <SettingsGroupCard<SlotSettings>
      title="Slot rules"
      description="How slots are generated and offered: duration, same-day cutoff, and capacity (SRS 15.2)."
      groupPath="slot"
      schema={slotSchema}
      defaultValues={initial}
      onSaved={(value) => updateSettingsCache(queryClient, "slot", value)}
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
              <CheckboxField label="Allow overbooking" checked={field.value} onChange={field.onChange} />
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

function CancellationSettingsSection({ initial, queryClient }: { initial: CancellationSettings; queryClient: QueryClient }) {
  return (
    <SettingsGroupCard<CancellationSettings>
      title="Cancellation policy"
      description="Free cancellation window and the late-cancellation fee (SRS 11.14.1)."
      groupPath="cancellation"
      schema={cancellationSchema}
      defaultValues={initial}
      onSaved={(value) => updateSettingsCache(queryClient, "cancellation", value)}
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
              <CheckboxField label="Allow admin override of the late fee" checked={field.value} onChange={field.onChange} />
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

function RescheduleSettingsSection({ initial, queryClient }: { initial: RescheduleSettings; queryClient: QueryClient }) {
  return (
    <SettingsGroupCard<RescheduleSettings>
      title="Reschedule policy"
      description="Reschedule blocking window, count limit, and the late-reschedule fee (SRS 11.15.1)."
      groupPath="reschedule"
      schema={rescheduleSchema}
      defaultValues={initial}
      onSaved={(value) => updateSettingsCache(queryClient, "reschedule", value)}
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

function TaxSettingsSection({ initial, queryClient }: { initial: TaxSettings; queryClient: QueryClient }) {
  return (
    <SettingsGroupCard<TaxSettings>
      title="Tax settings"
      description="Default tax rate and registration details shown on customer invoices."
      groupPath="tax"
      schema={taxSchema}
      defaultValues={initial}
      onSaved={(value) => updateSettingsCache(queryClient, "tax", value)}
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
          <Field
            label="Tax registration number (blank = not configured)"
            type="text"
            defaultValue={initial.taxRegistrationNumber ?? ""}
            error={form.formState.errors.taxRegistrationNumber?.message}
            {...form.register("taxRegistrationNumber", {
              setValueAs: (value: string) => (value === "" ? null : value),
            })}
          />
          <Controller
            control={form.control}
            name="taxInclusivePricing"
            render={({ field }) => (
              <CheckboxField label="Displayed prices already include tax" checked={field.value} onChange={field.onChange} />
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

function WalletSettingsSection({ initial, queryClient }: { initial: WalletSettings; queryClient: QueryClient }) {
  return (
    <SettingsGroupCard<WalletSettings>
      title="Wallet settings"
      description="Balance cap, how much of a booking wallet may cover, and credit expiry (SRS 14.5)."
      groupPath="wallet"
      schema={walletSchema}
      defaultValues={initial}
      onSaved={(value) => updateSettingsCache(queryClient, "wallet", value)}
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
          <Field
            label="Wallet credit expiry (days, blank = never)"
            type="number"
            defaultValue={nullableNumberToInputValue(initial.walletCreditExpiryDays)}
            error={form.formState.errors.walletCreditExpiryDays?.message}
            {...form.register("walletCreditExpiryDays", { setValueAs: emptyStringToNull })}
          />
          <Controller
            control={form.control}
            name="allowWalletTopUp"
            render={({ field }) => (
              <CheckboxField label="Allow customers to top up their wallet directly" checked={field.value} onChange={field.onChange} />
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

function CouponSettingsSection({ initial, queryClient }: { initial: CouponSettings; queryClient: QueryClient }) {
  return (
    <SettingsGroupCard<CouponSettings>
      title="Coupon settings"
      description="Platform-wide guardrails applied across every coupon, plus the coupons feature flag (SRS 14.2)."
      groupPath="coupon"
      schema={couponSchema}
      defaultValues={initial}
      onSaved={(value) => updateSettingsCache(queryClient, "coupon", value)}
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
          <Field
            label="Max active coupons per customer (blank = unlimited)"
            type="number"
            defaultValue={nullableNumberToInputValue(initial.maxActiveCouponsPerCustomer)}
            error={form.formState.errors.maxActiveCouponsPerCustomer?.message}
            {...form.register("maxActiveCouponsPerCustomer", { setValueAs: emptyStringToNull })}
          />
          <Controller
            control={form.control}
            name="allowCouponStacking"
            render={({ field }) => (
              <CheckboxField label="Allow more than one coupon per booking" checked={field.value} onChange={field.onChange} />
            )}
          />
          <Controller
            control={form.control}
            name="couponsEnabled"
            render={({ field }) => (
              <CheckboxField
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

export default function SystemSettingsPage() {
  const queryClient = useQueryClient();
  const { data, isLoading, isError, error } = useAllSettings();

  return (
    <div className="mx-auto flex w-full max-w-3xl flex-col gap-6">
      <PageHeading
        title="System settings"
        subtitle="Admin-configurable booking, slot, cancellation, reschedule, tax, wallet and coupon rules (SRS 12.19)."
      />

      {isLoading ? <Alert tone="info">Loading settings…</Alert> : null}
      {isError ? <Alert>{describeError(error)}</Alert> : null}

      {data ? (
        <>
          <BookingSettingsSection initial={data.booking} queryClient={queryClient} />
          <SlotSettingsSection initial={data.slot} queryClient={queryClient} />
          <CancellationSettingsSection initial={data.cancellation} queryClient={queryClient} />
          <RescheduleSettingsSection initial={data.reschedule} queryClient={queryClient} />
          <TaxSettingsSection initial={data.tax} queryClient={queryClient} />
          <WalletSettingsSection initial={data.wallet} queryClient={queryClient} />
          <CouponSettingsSection initial={data.coupon} queryClient={queryClient} />
        </>
      ) : null}
    </div>
  );
}
