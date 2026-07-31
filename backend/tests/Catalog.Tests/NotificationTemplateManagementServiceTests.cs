using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Nestly.Application.Abstractions.Auditing;
using Nestly.Application.Notifications;
using Nestly.Domain;
using Nestly.Infrastructure.Auditing;
using Nestly.Infrastructure.Persistence;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>Covers tasks 126a-d: admin notification template CRUD, preview and change audit (SRS 12.17).</summary>
public sealed class NotificationTemplateManagementServiceTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public NotificationTemplateManagementServiceTests(TestDatabase db) => _db = db;

    private static NotificationTemplateManagementService CreateService(NestlyDbContext context, IMemoryCache? cache = null) =>
        new(
            new NotificationTemplateRepository(context),
            new AuditLogWriter(context, new StubAuditContextProvider()),
            new StubAuditContextProvider(),
            cache ?? new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public async Task CreateAsync_persists_a_template_and_writes_an_audit_entry()
    {
        using var context = _db.CreateContext();
        var service = CreateService(context);

        var result = await service.CreateAsync(new NotificationTemplateCreateRequest(
            NotificationEventType.Welcome, NotificationChannel.Sms, "welcome_sms", null, "Hi {{CustomerName}}!"));

        result.IsSuccess.Should().BeTrue();
        result.Value.IsActive.Should().BeTrue();
        result.Value.TemplateKey.Should().Be("welcome_sms");

        context.Set<AuditLog>().Should().Contain(
            a => a.EntityName == "NotificationTemplate" && a.EntityId == result.Value.Id.ToString() && a.Action == "Created");
    }

    [Fact]
    public async Task CreateAsync_rejects_a_duplicate_event_and_channel_combination()
    {
        using var context = _db.CreateContext();
        var service = CreateService(context);
        var request = new NotificationTemplateCreateRequest(NotificationEventType.PaymentSuccess, NotificationChannel.Email, "payment_success_email", "Payment received", "Body");
        (await service.CreateAsync(request)).IsSuccess.Should().BeTrue();

        var result = await service.CreateAsync(request with { TemplateKey = "payment_success_email_v2" });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("NotificationTemplate.AlreadyExists");
    }

    [Theory]
    [InlineData(NotificationChannel.Sms, "Should not have a subject")]
    [InlineData(NotificationChannel.Email, null)]
    public async Task CreateAsync_rejects_a_subject_that_violates_the_channels_rule(NotificationChannel channel, string? subject)
    {
        using var context = _db.CreateContext();
        var service = CreateService(context);

        var result = await service.CreateAsync(new NotificationTemplateCreateRequest(
            NotificationEventType.RefundProcessed, channel, "refund_processed_" + channel, subject, "Body"));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("NotificationTemplate.InvalidSubject");
    }

    [Fact]
    public async Task UpdateAsync_persists_new_content_and_audits_old_and_new_values()
    {
        using var context = _db.CreateContext();
        var service = CreateService(context);
        var created = (await service.CreateAsync(new NotificationTemplateCreateRequest(
            NotificationEventType.BookingCancelled, NotificationChannel.Sms, "booking_cancelled_sms", null, "Old body"))).Value;

        var updated = await service.UpdateAsync(created.Id, new NotificationTemplateUpdateRequest(null, "New body {{BookingId}}"));

        updated.IsSuccess.Should().BeTrue();
        updated.Value.Body.Should().Be("New body {{BookingId}}");

        context.Set<AuditLog>().Should().Contain(a =>
            a.EntityId == created.Id.ToString() && a.Action == "Updated" &&
            a.OldValues!.Contains("Old body") && a.NewValues!.Contains("New body"));
    }

    [Fact]
    public async Task DeactivateAsync_then_ActivateAsync_toggles_is_active_and_audits_each_step()
    {
        using var context = _db.CreateContext();
        var service = CreateService(context);
        var created = (await service.CreateAsync(new NotificationTemplateCreateRequest(
            NotificationEventType.BookingRescheduled, NotificationChannel.Email, "booking_rescheduled_email", "Rescheduled", "Body"))).Value;

        (await service.DeactivateAsync(created.Id)).IsSuccess.Should().BeTrue();
        (await service.GetByIdAsync(created.Id)).Value.IsActive.Should().BeFalse();

        (await service.ActivateAsync(created.Id)).IsSuccess.Should().BeTrue();
        (await service.GetByIdAsync(created.Id)).Value.IsActive.Should().BeTrue();

        context.Set<AuditLog>().Should().Contain(a => a.EntityId == created.Id.ToString() && a.Action == "Deactivated");
        context.Set<AuditLog>().Should().Contain(a => a.EntityId == created.Id.ToString() && a.Action == "Activated");
    }

    [Fact]
    public async Task Deactivating_a_template_makes_the_renderer_stop_supporting_it_once_the_cache_is_invalidated()
    {
        using var context = _db.CreateContext();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var repository = new NotificationTemplateRepository(context);
        var service = CreateService(context, cache);
        var renderer = new NotificationTemplateRenderer(repository, cache);

        var created = (await service.CreateAsync(new NotificationTemplateCreateRequest(
            NotificationEventType.SupportTicketUpdate, NotificationChannel.Sms, "support_ticket_update_sms", null, "Ticket update"))).Value;

        (await renderer.SupportsChannelAsync(NotificationEventType.SupportTicketUpdate, NotificationChannel.Sms)).Should().BeTrue();

        await service.DeactivateAsync(created.Id);

        // Same IMemoryCache instance the service invalidates after every
        // write (see NotificationTemplateManagementService.InvalidateCache) -
        // the renderer must see the deactivation on its very next read rather
        // than serving the stale cached entry for up to CacheDuration.
        (await renderer.SupportsChannelAsync(NotificationEventType.SupportTicketUpdate, NotificationChannel.Sms)).Should().BeFalse();
    }

    [Fact]
    public async Task PreviewAsync_substitutes_sample_variables_without_persisting_anything()
    {
        using var context = _db.CreateContext();
        var service = CreateService(context);
        var created = (await service.CreateAsync(new NotificationTemplateCreateRequest(
            NotificationEventType.Welcome, NotificationChannel.Email, "welcome_email", "Welcome {{CustomerName}}", "Hi {{CustomerName}}, enjoy!"))).Value;

        var preview = await service.PreviewAsync(created.Id, new NotificationTemplatePreviewRequest(
            new Dictionary<string, string> { ["CustomerName"] = "Asha" }));

        preview.IsSuccess.Should().BeTrue();
        preview.Value.Subject.Should().Be("Welcome Asha");
        preview.Value.Body.Should().Be("Hi Asha, enjoy!");

        (await service.GetByIdAsync(created.Id)).Value.Body.Should().Be("Hi {{CustomerName}}, enjoy!", "preview must never mutate the stored template");
    }

    [Fact]
    public void PreviewAdHoc_substitutes_sample_variables_against_unsaved_draft_content()
    {
        using var context = _db.CreateContext();
        var service = CreateService(context);

        var preview = service.PreviewAdHoc(new NotificationTemplateAdHocPreviewRequest(
            NotificationChannel.Sms, null, "Hi {{CustomerName}}, your code is {{Code}}.",
            new Dictionary<string, string> { ["CustomerName"] = "Asha", ["Code"] = "123456" }));

        preview.Subject.Should().BeNull();
        preview.Body.Should().Be("Hi Asha, your code is 123456.");
    }

    [Fact]
    public async Task Operating_on_an_unknown_template_returns_not_found()
    {
        using var context = _db.CreateContext();
        var service = CreateService(context);

        (await service.GetByIdAsync(Guid.NewGuid())).Error.Code.Should().Be("NotificationTemplate.NotFound");
        (await service.ActivateAsync(Guid.NewGuid())).Error.Code.Should().Be("NotificationTemplate.NotFound");
        (await service.PreviewAsync(Guid.NewGuid(), new NotificationTemplatePreviewRequest(new Dictionary<string, string>()))).Error.Code.Should().Be("NotificationTemplate.NotFound");
    }

    private sealed class StubAuditContextProvider : IAuditContextProvider
    {
        public AuditContext GetCurrent() =>
            new(AuditActorType.AdminUser, Guid.NewGuid(), IpAddress: "127.0.0.1", CorrelationId: "test-correlation-id");
    }
}
