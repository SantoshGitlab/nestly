using FluentAssertions;
using Nestly.Domain;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>Covers task 87b: template rendering with variables.</summary>
public sealed class NotificationTemplateRendererTests
{
    private readonly NotificationTemplateRenderer _renderer = new();

    [Fact]
    public void Render_substitutes_variables_into_the_sms_body()
    {
        var rendered = _renderer.Render(
            NotificationEventType.Welcome, NotificationChannel.Sms, new Dictionary<string, string> { ["CustomerName"] = "Asha" });

        rendered.Body.Should().Contain("Asha");
        rendered.Subject.Should().BeNull("SMS has no subject line");
        rendered.TemplateKey.Should().Be("welcome_sms");
    }

    [Fact]
    public void Render_substitutes_variables_into_both_email_subject_and_body()
    {
        var rendered = _renderer.Render(
            NotificationEventType.BookingConfirmed, NotificationChannel.Email,
            new Dictionary<string, string> { ["CustomerName"] = "Asha", ["ServiceName"] = "Deep Clean", ["SlotDate"] = "2026-08-01", ["SlotWindow"] = "Morning", ["TotalPayable"] = "999" });

        rendered.Subject.Should().Be("Your Nestly booking is confirmed");
        rendered.Body.Should().Contain("Deep Clean").And.Contain("999");
    }

    [Fact]
    public void Render_leaves_an_unresolved_placeholder_untouched_rather_than_throwing()
    {
        var rendered = _renderer.Render(NotificationEventType.Welcome, NotificationChannel.Sms, new Dictionary<string, string>());

        rendered.Body.Should().Contain("{{CustomerName}}");
    }

    [Fact]
    public void Render_throws_for_an_event_channel_combination_with_no_template()
    {
        var act = () => _renderer.Render(NotificationEventType.Welcome, (NotificationChannel)999, new Dictionary<string, string>());

        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(NotificationEventType.Welcome)]
    [InlineData(NotificationEventType.BookingConfirmed)]
    [InlineData(NotificationEventType.PaymentSuccess)]
    [InlineData(NotificationEventType.PaymentFailed)]
    [InlineData(NotificationEventType.BookingCancelled)]
    [InlineData(NotificationEventType.BookingRescheduled)]
    [InlineData(NotificationEventType.RefundProcessed)]
    [InlineData(NotificationEventType.SupportTicketUpdate)]
    public void Every_trigger_event_has_both_an_sms_and_an_email_template(NotificationEventType eventType)
    {
        _renderer.SupportsChannel(eventType, NotificationChannel.Sms).Should().BeTrue();
        _renderer.SupportsChannel(eventType, NotificationChannel.Email).Should().BeTrue();
    }
}
