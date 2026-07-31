"use client";

import Link from "next/link";
import { Button } from "@/components/ui";
import { CouponCustomerSegment, CouponDiscountType, type CouponAdminResponse } from "@/lib/coupon-types";

/** Sentence-case label for the discount type, matching the codebase's Field/Select label convention. */
function formatDiscount(coupon: CouponAdminResponse): string {
  const value =
    coupon.discountType === CouponDiscountType.Percentage
      ? `${coupon.discountValue}%`
      : `₹${coupon.discountValue.toFixed(2)}`;
  return coupon.maxDiscountAmount ? `${value} (capped at ₹${coupon.maxDiscountAmount.toFixed(2)})` : value;
}

function formatSegment(segment: CouponCustomerSegment): string {
  switch (segment) {
    case CouponCustomerSegment.FirstBookingOnly:
      return "First booking only";
    case CouponCustomerSegment.RepeatBookingOnly:
      return "Repeat customers only";
    default:
      return "All customers";
  }
}

function formatUsage(coupon: CouponAdminResponse): string {
  const total = coupon.usageLimitTotal === null ? "unlimited" : String(coupon.usageLimitTotal);
  return `${coupon.redemptionCount} / ${total}`;
}

function formatDateOnly(iso: string): string {
  return new Date(iso).toLocaleDateString();
}

export function CouponsTable({
  coupons,
  isLoading,
  errorMessage,
  canWrite,
  onEdit,
  onToggleActive,
  togglingId,
}: {
  coupons: CouponAdminResponse[] | undefined;
  isLoading: boolean;
  errorMessage: string | null;
  canWrite: boolean;
  onEdit: (coupon: CouponAdminResponse) => void;
  onToggleActive: (coupon: CouponAdminResponse) => void;
  togglingId?: string;
}) {
  if (isLoading) {
    return <p className="text-sm text-neutral-600 dark:text-neutral-400">Loading…</p>;
  }

  if (errorMessage) {
    return <p className="text-sm text-red-600 dark:text-red-400">{errorMessage}</p>;
  }

  if (!coupons || coupons.length === 0) {
    return <p className="text-sm text-neutral-600 dark:text-neutral-400">No coupons match the current filters.</p>;
  }

  return (
    <div className="overflow-x-auto">
      <table className="w-full min-w-full text-left text-sm">
        <thead>
          <tr className="border-b border-black/10 text-xs uppercase tracking-wide text-neutral-500 dark:border-white/15 dark:text-neutral-400">
            <th scope="col" className="px-3 py-2 font-medium">Code</th>
            <th scope="col" className="px-3 py-2 font-medium">Discount</th>
            <th scope="col" className="px-3 py-2 font-medium">Min order</th>
            <th scope="col" className="px-3 py-2 font-medium">Validity</th>
            <th scope="col" className="px-3 py-2 font-medium">Usage</th>
            <th scope="col" className="px-3 py-2 font-medium">Segment</th>
            <th scope="col" className="px-3 py-2 font-medium">Category</th>
            <th scope="col" className="px-3 py-2 font-medium">Status</th>
            <th scope="col" className="px-3 py-2 font-medium">
              <span className="sr-only">Actions</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {coupons.map((coupon) => (
            <tr key={coupon.id} className="border-b border-black/5 last:border-0 dark:border-white/10">
              <td className="px-3 py-2 font-medium">{coupon.code}</td>
              <td className="px-3 py-2">{formatDiscount(coupon)}</td>
              <td className="px-3 py-2">₹{coupon.minOrderAmount.toFixed(2)}</td>
              <td className="px-3 py-2">
                {formatDateOnly(coupon.validFromUtc)} – {formatDateOnly(coupon.validToUtc)}
              </td>
              <td className="px-3 py-2">{formatUsage(coupon)}</td>
              <td className="px-3 py-2">{formatSegment(coupon.customerSegment)}</td>
              <td className="px-3 py-2">{coupon.applicableCategoryName ?? "All categories"}</td>
              <td className="px-3 py-2">
                <span
                  className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${
                    coupon.isActive
                      ? "bg-green-100 text-green-800 dark:bg-green-950 dark:text-green-200"
                      : "bg-neutral-200 text-neutral-700 dark:bg-neutral-800 dark:text-neutral-300"
                  }`}
                >
                  {coupon.isActive ? "Active" : "Suspended"}
                </span>
              </td>
              <td className="px-3 py-2">
                <div className="flex flex-wrap gap-2">
                  <Link
                    href={`/coupons/redemptions?couponId=${coupon.id}`}
                    className="inline-flex items-center justify-center rounded-lg border border-black/15 px-2 py-1 text-xs font-medium transition-colors hover:bg-black/5 dark:border-white/20 dark:hover:bg-white/10"
                  >
                    Report
                  </Link>
                  {canWrite ? (
                    <>
                      <Button type="button" variant="secondary" className="px-2 py-1 text-xs" onClick={() => onEdit(coupon)}>
                        Edit
                      </Button>
                      <Button
                        type="button"
                        variant={coupon.isActive ? "danger" : "secondary"}
                        className="px-2 py-1 text-xs"
                        disabled={togglingId === coupon.id}
                        onClick={() => onToggleActive(coupon)}
                      >
                        {togglingId === coupon.id ? "Saving…" : coupon.isActive ? "Suspend" : "Activate"}
                      </Button>
                    </>
                  ) : null}
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
