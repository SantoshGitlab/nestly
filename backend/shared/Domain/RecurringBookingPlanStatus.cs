namespace Nestly.Domain;

/// <summary>
/// Lifecycle of a <see cref="RecurringBookingPlan"/> (PRODUCT-ENHANCEMENTS.md
/// section 2, task 184). The spec only names three states (active/paused/
/// cancelled); <see cref="Completed"/> is added here as a deliberate,
/// documented extension - without it a plan that has delivered every
/// occurrence it promised (occurrence_count reached, or end_date passed)
/// would sit forever in <see cref="Active"/> with nothing left to schedule,
/// which is a different, terminal condition from a customer or admin
/// choosing to stop early via <see cref="Cancelled"/>. Distinguishing the two
/// matters for reporting ("how many plans ran to completion vs were
/// cancelled early") even though both are terminal for scheduling purposes.
/// </summary>
public enum RecurringBookingPlanStatus
{
    Active,
    Paused,
    Cancelled,
    Completed
}
