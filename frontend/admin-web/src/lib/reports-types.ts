/**
 * Types for the Admin API's reports and exports surface (SRS 12.18, tasks
 * 128a-128d): `ReportsController`. Mirrors
 * `backend/shared/Application/Reports/ReportingContracts.cs` and
 * `ExportJobContracts.cs` field-for-field. Re-uses `BookingStatus`,
 * `CustomerStatus` and `SupportTicketCategory` from lib/types.ts rather than
 * redeclaring them - same backend enums the bookings/customers/support
 * screens already mirror.
 */
import type { BookingStatus, CustomerStatus, SupportTicketCategory } from "./types";

export enum RefundType {
  Full = 0,
  Partial = 1,
}

/** Mirrors `Nestly.Domain.RefundMethod`'s declaration order exactly. */
export enum RefundMethod {
  Gateway = 0,
  Wallet = 1,
}

/** Mirrors `Nestly.Domain.RefundStatus`'s declaration order exactly. */
export enum RefundStatus {
  Initiated = 0,
  Processing = 1,
  Refunded = 2,
  Failed = 3,
}

export interface BookingRevenueReportRow {
  bookingId: string;
  createdAtUtc: string;
  city: string;
  status: BookingStatus;
  couponCode: string | null;
  subtotalSnapshot: number;
  taxAmountSnapshot: number;
  totalPayableSnapshot: number;
}

export interface BookingRevenueReportResponse {
  rows: BookingRevenueReportRow[];
  totalBookingsCount: number;
  totalRevenue: number;
}

export interface RefundReportRow {
  refundId: string;
  bookingId: string;
  type: RefundType;
  method: RefundMethod;
  amount: number;
  status: RefundStatus;
  reason: string;
  createdAtUtc: string;
  processedAtUtc: string | null;
}

export interface RefundReportResponse {
  rows: RefundReportRow[];
  totalCount: number;
  totalRefundedAmount: number;
}

export interface CouponUsageReportRow {
  couponId: string;
  couponCode: string;
  redemptionCount: number;
  totalDiscountAmount: number;
}

export interface CouponUsageReportResponse {
  rows: CouponUsageReportRow[];
  totalRedemptions: number;
  totalDiscountAmount: number;
}

export interface CustomerStatusSegmentRow {
  status: CustomerStatus;
  count: number;
}

export interface CustomerCitySegmentRow {
  city: string;
  count: number;
}

export interface CustomerSegmentationReportResponse {
  totalCustomers: number;
  byStatus: CustomerStatusSegmentRow[];
  byCity: CustomerCitySegmentRow[];
}

export interface SupportTicketCategoryVolumeRow {
  category: SupportTicketCategory;
  count: number;
}

export interface SupportTicketReportResponse {
  totalTickets: number;
  resolvedCount: number;
  averageResolutionHours: number | null;
  byCategory: SupportTicketCategoryVolumeRow[];
}

/** Mirrors `Nestly.Domain.ExportReportType`'s declaration order exactly. */
export enum ExportReportType {
  BookingRevenue = 0,
  RefundUsage = 1,
  CouponUsage = 2,
  CustomerSegmentation = 3,
  SupportTicket = 4,
}

/** Mirrors `Nestly.Domain.ExportJobStatus`'s declaration order exactly. */
export enum ExportJobStatus {
  Pending = 0,
  Processing = 1,
  Completed = 2,
  Failed = 3,
}

export interface RequestExportJobRequest {
  reportType: ExportReportType;
  fromUtc: string;
  toUtc: string;
  city?: string | null;
  categoryId?: string | null;
}

export interface ExportJobStatusResponse {
  id: string;
  reportType: ExportReportType;
  status: ExportJobStatus;
  requestedAtUtc: string;
  completedAtUtc: string | null;
  errorMessage: string | null;
  hasResult: boolean;
}
