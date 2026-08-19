"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useQuery } from "@tanstack/react-query";
import { useEffect } from "react";
import { Controller, useForm } from "react-hook-form";
import type { UseFormReturn } from "react-hook-form";
import { z } from "zod";
import { Alert, Button, Field, Select } from "@/components/ui";
import { FormActions, FormGrid } from "@/components/data-table";
import { describeError } from "@/lib/api";
import { listApplicableCategories } from "@/lib/coupon-api";
import { todayIsoDate } from "@/lib/date";
import { endOfLocalDayUtc, startOfLocalDayUtc, utcToLocalDateInput } from "@/lib/day-range";
import {
  CouponCustomerSegment,
  CouponDiscountType,
  type CouponAdminResponse,
  type CouponCreateRequest,
  type CouponUpdateRequest,
} from "@/lib/coupon-types";

/**
 * Create/edit form for every coupon rule dimension (SRS 12.12.1, task 119):
 * discount type/value, max discount cap, min order amount, validity window,
 * usage limits (global + per-customer), applicable category, and the
 * first/repeat-order segment. One component serves both modes - `coupon`
 * present means "edit" (the code becomes read-only and is never resubmitted,
 * matching CouponUpdateRequest's shape - see Coupon.Update's doc comment for
 * why the code itself cannot be changed after creation).
 */

const DISCOUNT_TYPE_OPTIONS = [
  { value: String(CouponDiscountType.Percentage), label: "Percentage" },
  { value: String(CouponDiscountType.Flat), label: "Flat amount" },
] as const;

const CUSTOMER_SEGMENT_OPTIONS = [
  { value: String(CouponCustomerSegment.All), label: "All customers" },
  { value: String(CouponCustomerSegment.FirstBookingOnly), label: "First booking only" },
  { value: String(CouponCustomerSegment.RepeatBookingOnly), label: "Repeat customers only" },
] as const;

const couponFormSchema = z
  .object({
    code: z.string().min(1, "Coupon code is required").max(50, "Coupon code must be 50 characters or fewer"),
    description: z.string().max(300, "Description must be 300 characters or fewer"),
    // Plain z.number() rather than z.coerce.number(): coerce's input/output
    // type split (input `unknown`, output `number`) doesn't line up with
    // useForm<CouponFormValues>'s single type parameter and fails to
    // typecheck - registering these with `valueAsNumber: true` below already
    // converts the select's string value to a number before zod ever sees it.
    discountType: z.number().int(),
    discountValue: z.number().positive("Discount value must be positive"),
    maxDiscountAmount: z.number().positive("Max discount amount must be positive").nullable(),
    minOrderAmount: z.number().min(0, "Minimum order amount cannot be negative"),
    validFromDate: z.string().min(1, "Start date is required"),
    validToDate: z.string().min(1, "End date is required"),
    usageLimitTotal: z.number().int().positive("Usage limit must be positive").nullable(),
    usageLimitPerCustomer: z.number().int().positive("Per-customer usage limit must be positive").nullable(),
    applicableCategoryId: z.string(),
    customerSegment: z.number().int(),
  })
  .refine((values) => values.discountType !== CouponDiscountType.Percentage || values.discountValue <= 100, {
    path: ["discountValue"],
    message: "A percentage discount cannot exceed 100.",
  })
  // Inclusive whole days, so equal dates are a legitimate one-day campaign.
  // A strict `>` here rejected exactly that.
  .refine((values) => values.validToDate >= values.validFromDate, {
    path: ["validToDate"],
    message: "The end date cannot be before the start date.",
  });

type CouponFormValues = z.infer<typeof couponFormSchema>;

function emptyStringToNull(value: string): number | null {
  return value === "" ? null : Number(value);
}

function nullableNumberToInputValue(value: number | null): string {
  return value === null || value === undefined ? "" : String(value);
}

/**
 * Nullable numeric field (an optional cap/limit that means "unlimited" when
 * unset), as a `Controller`-bound field rather than the plain
 * `register(name, { setValueAs })` + manual `defaultValue` combination this
 * form used before.
 *
 * `setValueAs` only runs inside the `onChange` handler `register` generates
 * - it transforms a value the admin actively types, but a field they never
 * touch keeps whatever the DOM `defaultValue` rendered it with (`""`, per
 * `nullableNumberToInputValue(null)`), and react-hook-form reads that raw
 * string at submit time. `z.number().positive().nullable()` rejects `""` as
 * neither a valid number nor `null`, so submitting the form without first
 * touching an untouched-but-legitimately-blank "Max discount cap"/"Global
 * usage limit" field failed with a confusing "must be positive" error on a
 * field the admin never edited - exactly the bug `NullableNumberField` in
 * `app/(admin)/settings/page.tsx` was already written to fix for that
 * screen's own nullable caps; this is the same fix applied here.
 */
function NullableNumberField({
  form,
  name,
  label,
  hint,
  min,
  leading,
}: {
  form: UseFormReturn<CouponFormValues>;
  name: "maxDiscountAmount" | "usageLimitTotal";
  label: string;
  hint?: string;
  min?: number;
  leading?: string;
}) {
  return (
    <Controller
      control={form.control}
      name={name}
      render={({ field, fieldState }) => (
        <Field
          label={label}
          type="number"
          step={leading ? "0.01" : undefined}
          min={min}
          leading={leading}
          hint={hint}
          value={nullableNumberToInputValue(field.value)}
          onChange={(event) => field.onChange(emptyStringToNull(event.target.value))}
          onBlur={field.onBlur}
          error={fieldState.error?.message}
        />
      )}
    />
  );
}

function defaultValuesFor(coupon: CouponAdminResponse | null): CouponFormValues {
  if (!coupon) {
    const today = todayIsoDate();
    return {
      code: "",
      description: "",
      discountType: CouponDiscountType.Percentage,
      discountValue: 10,
      maxDiscountAmount: null,
      minOrderAmount: 0,
      validFromDate: today,
      validToDate: today,
      usageLimitTotal: null,
      usageLimitPerCustomer: 1,
      applicableCategoryId: "",
      customerSegment: CouponCustomerSegment.All,
    };
  }

  return {
    code: coupon.code,
    description: coupon.description ?? "",
    discountType: coupon.discountType,
    discountValue: coupon.discountValue,
    maxDiscountAmount: coupon.maxDiscountAmount,
    minOrderAmount: coupon.minOrderAmount,
    validFromDate: utcToLocalDateInput(coupon.validFromUtc),
    validToDate: utcToLocalDateInput(coupon.validToUtc),
    usageLimitTotal: coupon.usageLimitTotal,
    usageLimitPerCustomer: coupon.usageLimitPerCustomer,
    applicableCategoryId: coupon.applicableCategoryId ?? "",
    customerSegment: coupon.customerSegment,
  };
}

export function CouponForm({
  coupon,
  isSubmitting,
  submitError,
  onSubmit,
  onCancel,
}: {
  /** Present in edit mode; null when creating a new coupon. */
  coupon: CouponAdminResponse | null;
  isSubmitting: boolean;
  submitError: string | null;
  onSubmit: (request: CouponCreateRequest | CouponUpdateRequest) => void;
  onCancel?: () => void;
}) {
  const categoriesQuery = useQuery({ queryKey: ["coupons", "categories"], queryFn: listApplicableCategories });

  const form = useForm<CouponFormValues>({
    resolver: zodResolver(couponFormSchema),
    defaultValues: defaultValuesFor(coupon),
  });

  // Re-seed the form whenever the coupon being edited changes (switching
  // from "create" to "edit", or between two different rows) - react-hook-form
  // only applies `defaultValues` once, at mount.
  useEffect(() => {
    form.reset(defaultValuesFor(coupon));
  }, [coupon, form]);

  const isEditing = coupon !== null;
  // Drives the value field's ₹/% adornment, so the unit is never ambiguous.
  const isPercentage = form.watch("discountType") === CouponDiscountType.Percentage;

  const submit = form.handleSubmit((values) => {
    const validFromUtc = startOfLocalDayUtc(values.validFromDate);
    const validToUtc = endOfLocalDayUtc(values.validToDate);
    // The date controls are already validated as required, so this only fires
    // if a browser hands back something that is not `yyyy-mm-dd`. Reporting it
    // on the control beats posting a null validity window.
    if (!validFromUtc || !validToUtc) {
      form.setError(validFromUtc ? "validToDate" : "validFromDate", {
        message: "Enter a valid date.",
      });
      return;
    }

    const shared = {
      description: values.description.trim() === "" ? null : values.description.trim(),
      discountType: values.discountType as CouponDiscountType,
      discountValue: values.discountValue,
      maxDiscountAmount: values.maxDiscountAmount,
      minOrderAmount: values.minOrderAmount,
      validFromUtc,
      validToUtc,
      usageLimitTotal: values.usageLimitTotal,
      usageLimitPerCustomer: values.usageLimitPerCustomer,
      applicableCategoryId: values.applicableCategoryId === "" ? null : values.applicableCategoryId,
      customerSegment: values.customerSegment as CouponCustomerSegment,
    };

    onSubmit(isEditing ? shared : { code: values.code, ...shared });
  });

  const categoryOptions = [
    { value: "", label: "No restriction (applies to every category)" },
    ...(categoriesQuery.data ?? []).map((category) => ({ value: category.id, label: category.name })),
  ];

  return (
    <form onSubmit={submit} className="flex flex-col gap-4" noValidate>
      {submitError ? <Alert>{submitError}</Alert> : null}
      {categoriesQuery.isError ? (
        <Alert
          tone="warning"
          action={
            <Button size="sm" variant="secondary" onClick={() => categoriesQuery.refetch()}>
              Retry
            </Button>
          }
        >
          Categories could not be loaded, so the coupon can only be saved without a category
          restriction. {describeError(categoriesQuery.error)}
        </Alert>
      ) : null}

      <FormGrid>
        <Field
          label="Coupon code"
          required
          error={form.formState.errors.code?.message}
          hint={isEditing ? "The code cannot be changed after creation." : undefined}
          // readOnly, not disabled: a disabled react-hook-form field is
          // excluded from the submitted values entirely, which would fail
          // this field's own "required" rule on every edit-mode submit.
          // readOnly still submits the (unusable) value - harmless, since
          // CouponUpdateRequest has no `code` property to send it to.
          readOnly={isEditing}
          title={isEditing ? "The coupon code cannot be changed after creation." : undefined}
          {...form.register("code")}
        />
        <Field label="Description" error={form.formState.errors.description?.message} {...form.register("description")} />
      </FormGrid>

      <FormGrid columns={3}>
        <Select
          label="Discount type"
          error={form.formState.errors.discountType?.message}
          options={DISCOUNT_TYPE_OPTIONS}
          {...form.register("discountType", { valueAsNumber: true })}
        />
        <Field
          label="Discount value"
          type="number"
          step="0.01"
          min={0}
          required
          leading={isPercentage ? "%" : "₹"}
          error={form.formState.errors.discountValue?.message}
          {...form.register("discountValue", { valueAsNumber: true })}
        />
        <NullableNumberField
          form={form}
          name="maxDiscountAmount"
          label="Max discount cap"
          min={0}
          leading="₹"
          hint={isPercentage ? "Optional — caps a percentage discount." : "Optional."}
        />
      </FormGrid>

      <FormGrid columns={3}>
        <Field
          label="Minimum order amount"
          type="number"
          step="0.01"
          min={0}
          leading="₹"
          error={form.formState.errors.minOrderAmount?.message}
          {...form.register("minOrderAmount", { valueAsNumber: true })}
        />
        <Field
          label="Valid from"
          type="date"
          required
          error={form.formState.errors.validFromDate?.message}
          {...form.register("validFromDate")}
        />
        <Field
          label="Valid to"
          type="date"
          required
          hint="Inclusive — the coupon runs to the end of this day."
          error={form.formState.errors.validToDate?.message}
          {...form.register("validToDate")}
        />
      </FormGrid>

      <FormGrid columns={3}>
        <NullableNumberField
          form={form}
          name="usageLimitTotal"
          label="Global usage limit"
          min={1}
          hint="Optional — leave empty for unlimited."
        />
        <Field
          label="Per-customer usage limit"
          type="number"
          min={1}
          hint="Optional — leave empty for unlimited."
          error={form.formState.errors.usageLimitPerCustomer?.message}
          {...form.register("usageLimitPerCustomer", { setValueAs: emptyStringToNull })}
          defaultValue={nullableNumberToInputValue(form.getValues("usageLimitPerCustomer"))}
        />
        <Select
          label="Applicable category"
          error={form.formState.errors.applicableCategoryId?.message}
          options={categoryOptions}
          {...form.register("applicableCategoryId")}
        />
      </FormGrid>

      <Select
        label="Customer segment"
        error={form.formState.errors.customerSegment?.message}
        options={CUSTOMER_SEGMENT_OPTIONS}
        {...form.register("customerSegment", { valueAsNumber: true })}
      />

      <FormActions align="start">
        <Button type="submit" loading={isSubmitting}>
          {isEditing ? "Save changes" : "Create coupon"}
        </Button>
        {onCancel ? (
          <Button type="button" variant="secondary" onClick={onCancel} disabled={isSubmitting}>
            Cancel
          </Button>
        ) : null}
      </FormActions>
    </form>
  );
}
