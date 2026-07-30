namespace Nestly.Domain;

/// <summary>Applicability by order history (SRS 11.10.2 "first order / repeat order rule").</summary>
public enum CouponCustomerSegment
{
    All,
    FirstBookingOnly,
    RepeatBookingOnly
}
