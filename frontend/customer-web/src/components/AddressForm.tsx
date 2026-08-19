"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { StickyActionBar } from "@/components/patterns";
import { Alert, Button, Checkbox, Field } from "@/components/ui";
import type { CustomerAddress } from "@/lib/types";

/**
 * Shared by the add and edit screens — both post the same
 * UpsertAddressRequest shape (AddressContracts.cs), so the field list and its
 * validation live in one place.
 */
export const addressSchema = z.object({
  label: z.string().min(1, "Label is required").max(50),
  line1: z.string().min(1, "Address line 1 is required").max(200),
  line2: z.string().max(200),
  landmark: z.string().max(200),
  pincode: z.string().regex(/^\d{6}$/, "Pincode must be 6 digits"),
  city: z.string().min(1, "City is required").max(100),
  state: z.string().min(1, "State is required").max(100),
  latitude: z.coerce
    .number({ message: "Latitude is required" })
    .min(-90, "Latitude must be between -90 and 90")
    .max(90, "Latitude must be between -90 and 90"),
  longitude: z.coerce
    .number({ message: "Longitude is required" })
    .min(-180, "Longitude must be between -180 and 180")
    .max(180, "Longitude must be between -180 and 180"),
  contactName: z.string().min(1, "Contact name is required").max(200),
  contactMobile: z
    .string()
    .regex(/^\+?[1-9]\d{7,14}$/, "Enter a valid mobile number"),
  isDefault: z.boolean(),
});

export type AddressFormValues = z.input<typeof addressSchema>;
export type AddressPayload = z.output<typeof addressSchema>;

export function AddressForm({
  initial,
  submitLabel,
  error,
  isSubmitting,
  onSubmit,
}: {
  initial?: CustomerAddress;
  submitLabel: string;
  error: string | null;
  isSubmitting: boolean;
  onSubmit: (values: AddressPayload) => void;
}) {
  const form = useForm<AddressFormValues, unknown, AddressPayload>({
    resolver: zodResolver(addressSchema),
    defaultValues: {
      label: initial?.label ?? "Home",
      line1: initial?.line1 ?? "",
      line2: initial?.line2 ?? "",
      landmark: initial?.landmark ?? "",
      pincode: initial?.pincode ?? "",
      city: initial?.city ?? "",
      state: initial?.state ?? "",
      latitude: initial?.latitude ?? 0,
      longitude: initial?.longitude ?? 0,
      contactName: initial?.contactName ?? "",
      contactMobile: initial?.contactMobile ?? "",
      isDefault: initial?.isDefault ?? false,
    },
  });

  const { errors } = form.formState;

  return (
    <form
      onSubmit={form.handleSubmit((values) => onSubmit(values))}
      className="flex flex-col gap-4"
      noValidate
    >
      {error ? <Alert>{error}</Alert> : null}

      <Field label="Label" placeholder="Home, Work…" error={errors.label?.message} {...form.register("label")} />
      <Field label="Address line 1" error={errors.line1?.message} {...form.register("line1")} />
      <Field label="Address line 2 (optional)" error={errors.line2?.message} {...form.register("line2")} />
      <Field label="Landmark (optional)" error={errors.landmark?.message} {...form.register("landmark")} />
      <Field
        label="Pincode"
        inputMode="numeric"
        autoComplete="postal-code"
        maxLength={6}
        error={errors.pincode?.message}
        {...form.register("pincode")}
      />
      <Field label="City" error={errors.city?.message} {...form.register("city")} />
      <Field label="State" error={errors.state?.message} {...form.register("state")} />
      <div className="grid grid-cols-2 gap-4">
        <Field
          label="Latitude"
          type="number"
          step="any"
          error={errors.latitude?.message}
          {...form.register("latitude")}
        />
        <Field
          label="Longitude"
          type="number"
          step="any"
          error={errors.longitude?.message}
          {...form.register("longitude")}
        />
      </div>
      <Field label="Contact name" error={errors.contactName?.message} {...form.register("contactName")} />
      <Field
        label="Contact mobile"
        type="tel"
        inputMode="tel"
        autoComplete="tel"
        error={errors.contactMobile?.message}
        {...form.register("contactMobile")}
      />
      <Checkbox label="Use as my default address" {...form.register("isDefault")} />

      {/* Reachable without hunting for it below nine stacked fields on a
          phone (task #344 - addresses/new is reached mid-booking via
          booking/summary's "add a new address", so this is a real booking-
          funnel screen, not only account-management furniture; addresses/[id]/edit
          shares this component and gets the same treatment for free rather
          than forking the form in two). */}
      <StickyActionBar>
        <Button type="submit" fullWidth size="lg" disabled={isSubmitting}>
          {isSubmitting ? "Saving…" : submitLabel}
        </Button>
      </StickyActionBar>
    </form>
  );
}

/** Empty optional fields go to the API as null rather than "". */
export function toUpsertBody(values: AddressPayload) {
  return {
    ...values,
    line2: values.line2 === "" ? null : values.line2,
    landmark: values.landmark === "" ? null : values.landmark,
  };
}
