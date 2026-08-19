using FluentAssertions;
using Nestly.Domain;
using Nestly.Domain.Events;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Pure domain-logic coverage for <see cref="CustomerAmcContract"/> (Phase
/// 20, docs/AMC.md): entitlement drawdown, term/status transitions, and the
/// once-per-term expiring-soon reminder. No database needed - the aggregate
/// holds no infrastructure dependencies, the same reasoning
/// <see cref="RecurringBookingPlanTests"/> gives for its own pure-domain
/// suite.
/// </summary>
public sealed class CustomerAmcContractTests
{
    private static readonly DateTime Now = new(2026, 8, 18, 0, 0, 0, DateTimeKind.Utc);

    private static AmcPlan BuildPlan(int termMonths = 12, int visitsIncluded = 4, decimal price = 3499m) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "AC AMC " + Guid.NewGuid(), "2 services a year", price, termMonths, visitsIncluded);

    private static CustomerAmcContract Purchase(AmcPlan plan, DateTime? nowUtc = null, Guid? customerId = null) =>
        new(Guid.NewGuid(), customerId ?? Guid.NewGuid(), plan, "Living room split AC", paymentTransactionId: null, nowUtc ?? Now);

    [Fact]
    public void Purchase_snapshots_the_plans_terms_and_starts_fully_entitled_and_active()
    {
        var plan = BuildPlan(termMonths: 12, visitsIncluded: 4, price: 3499m);

        var contract = Purchase(plan);

        contract.PlanId.Should().Be(plan.Id);
        contract.PlanNameSnapshot.Should().Be(plan.Name);
        contract.CategoryIdSnapshot.Should().Be(plan.CategoryId);
        contract.PriceSnapshot.Should().Be(3499m);
        contract.TermMonthsSnapshot.Should().Be(12);
        contract.VisitsIncludedSnapshot.Should().Be(4);
        contract.VisitsRemaining.Should().Be(4);
        contract.Status.Should().Be(CustomerAmcContractStatus.Active);
        contract.StartDateUtc.Should().Be(Now);
        contract.EndDateUtc.Should().Be(Now.AddMonths(12));
        contract.AssetLabel.Should().Be("Living room split AC");
        contract.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<AmcContractPurchasedEvent>();
    }

    [Fact]
    public void Purchase_requires_a_non_blank_asset_label()
    {
        var plan = BuildPlan();

        var act = () => new CustomerAmcContract(Guid.NewGuid(), Guid.NewGuid(), plan, "   ", null, Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RedeemVisit_decrements_entitlement_and_raises_the_redeemed_event()
    {
        var plan = BuildPlan(visitsIncluded: 4);
        var contract = Purchase(plan);
        contract.ClearDomainEvents();
        var bookingId = Guid.NewGuid();

        contract.RedeemVisit(bookingId, Now.AddDays(10));

        contract.VisitsRemaining.Should().Be(3);
        contract.Status.Should().Be(CustomerAmcContractStatus.Active);
        var redeemed = contract.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<AmcVisitRedeemedEvent>().Subject;
        redeemed.BookingId.Should().Be(bookingId);
        redeemed.VisitsRemaining.Should().Be(3);
    }

    [Fact]
    public void RedeemVisit_moves_to_exhausted_and_raises_both_events_when_the_last_visit_is_consumed()
    {
        var plan = BuildPlan(visitsIncluded: 1);
        var contract = Purchase(plan);
        contract.ClearDomainEvents();

        contract.RedeemVisit(Guid.NewGuid(), Now.AddDays(10));

        contract.VisitsRemaining.Should().Be(0);
        contract.Status.Should().Be(CustomerAmcContractStatus.Exhausted);
        contract.DomainEvents.Should().HaveCount(2);
        contract.DomainEvents.OfType<AmcVisitRedeemedEvent>().Should().ContainSingle();
        contract.DomainEvents.OfType<AmcContractExhaustedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void RedeemVisit_throws_when_the_contract_is_not_active()
    {
        var plan = BuildPlan();
        var contract = Purchase(plan);
        contract.Cancel(Now.AddDays(1));

        var act = () => contract.RedeemVisit(Guid.NewGuid(), Now.AddDays(2));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RedeemVisit_throws_once_the_term_has_ended()
    {
        var plan = BuildPlan(termMonths: 1);
        var contract = Purchase(plan);

        var act = () => contract.RedeemVisit(Guid.NewGuid(), Now.AddMonths(1).AddDays(1));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CanRedeem_is_false_once_entitlement_is_exhausted_or_the_term_has_ended_or_the_contract_is_not_active()
    {
        var activePlan = BuildPlan(visitsIncluded: 1, termMonths: 1);
        var contract = Purchase(activePlan);

        contract.CanRedeem(Now.AddDays(1)).Should().BeTrue();

        contract.RedeemVisit(Guid.NewGuid(), Now.AddDays(1));
        contract.CanRedeem(Now.AddDays(2)).Should().BeFalse("no entitlement remains");

        var cancellable = Purchase(BuildPlan(termMonths: 1));
        cancellable.Cancel(Now.AddDays(1));
        cancellable.CanRedeem(Now.AddDays(2)).Should().BeFalse("the contract is no longer active");

        var expiredTerm = Purchase(BuildPlan(termMonths: 1));
        expiredTerm.CanRedeem(Now.AddMonths(2)).Should().BeFalse("the term has ended");
    }

    [Fact]
    public void Expire_moves_an_active_contract_whose_term_has_passed_to_expired()
    {
        var plan = BuildPlan(termMonths: 1);
        var contract = Purchase(plan);

        contract.Expire(Now.AddMonths(1).AddDays(1));

        contract.Status.Should().Be(CustomerAmcContractStatus.Expired);
    }

    [Fact]
    public void Expire_throws_when_the_term_has_not_yet_ended()
    {
        var plan = BuildPlan(termMonths: 1);
        var contract = Purchase(plan);

        var act = () => contract.Expire(Now.AddDays(1));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Expire_is_a_no_op_on_a_contract_already_exhausted_so_exhaustion_is_not_overwritten_by_expiry()
    {
        var plan = BuildPlan(visitsIncluded: 1, termMonths: 1);
        var contract = Purchase(plan);
        contract.RedeemVisit(Guid.NewGuid(), Now.AddDays(1));
        contract.Status.Should().Be(CustomerAmcContractStatus.Exhausted);

        contract.Expire(Now.AddMonths(2));

        contract.Status.Should().Be(CustomerAmcContractStatus.Exhausted, "exhaustion is the more informative terminal outcome for the renewal report");
    }

    [Fact]
    public void Cancel_is_immediate_and_terminal()
    {
        var plan = BuildPlan();
        var contract = Purchase(plan);

        contract.Cancel(Now.AddDays(5));

        contract.Status.Should().Be(CustomerAmcContractStatus.Cancelled);
        contract.CancelledAtUtc.Should().Be(Now.AddDays(5));
    }

    [Theory]
    [InlineData(CustomerAmcContractStatus.Cancelled)]
    [InlineData(CustomerAmcContractStatus.Expired)]
    [InlineData(CustomerAmcContractStatus.Exhausted)]
    public void Cancel_throws_when_the_contract_is_already_terminal(CustomerAmcContractStatus terminalStatus)
    {
        var plan = BuildPlan(visitsIncluded: 1, termMonths: 1);
        var contract = Purchase(plan);

        switch (terminalStatus)
        {
            case CustomerAmcContractStatus.Cancelled:
                contract.Cancel(Now.AddDays(1));
                break;
            case CustomerAmcContractStatus.Expired:
                contract.Expire(Now.AddMonths(2));
                break;
            case CustomerAmcContractStatus.Exhausted:
                contract.RedeemVisit(Guid.NewGuid(), Now.AddDays(1));
                break;
        }

        var act = () => contract.Cancel(Now.AddDays(10));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkExpiringSoonNotified_fires_once_per_term_end_date()
    {
        var plan = BuildPlan(termMonths: 12);
        var contract = Purchase(plan);

        contract.NeedsExpiringSoonNotification.Should().BeTrue();

        contract.MarkExpiringSoonNotified(Now.AddMonths(11));

        contract.ExpiringSoonNotifiedForEndDateUtc.Should().Be(contract.EndDateUtc);
        contract.NeedsExpiringSoonNotification.Should().BeFalse("a reminder was already sent for this exact end date");
        contract.DomainEvents.OfType<AmcContractExpiringSoonEvent>().Should().ContainSingle();
    }
}
