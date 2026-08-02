using Nestly.Application.Abstractions.Auditing;
using Nestly.Application.Subscriptions;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>See <see cref="ISubscriptionPlanManagementService"/>.</summary>
public class SubscriptionPlanManagementService : ISubscriptionPlanManagementService
{
    private readonly ISubscriptionPlanRepository _planRepository;
    private readonly IAuditLogWriter _auditLogWriter;

    public SubscriptionPlanManagementService(ISubscriptionPlanRepository planRepository, IAuditLogWriter auditLogWriter)
    {
        _planRepository = planRepository;
        _auditLogWriter = auditLogWriter;
    }

    public async Task<IReadOnlyList<SubscriptionPlanAdminResponse>> ListAllAsync()
    {
        var plans = await _planRepository.ListAllAsync();
        return plans.Select(ToResponse).ToList();
    }

    public async Task<Result<SubscriptionPlanAdminResponse>> GetByIdAsync(Guid id)
    {
        var plan = await _planRepository.GetByIdAsync(id);
        return plan is null
            ? Error.NotFound("SubscriptionPlan.NotFound", "The specified subscription plan does not exist.")
            : ToResponse(plan);
    }

    public async Task<Result<SubscriptionPlanAdminResponse>> CreateAsync(SubscriptionPlanCreateRequest request)
    {
        if (await _planRepository.NameExistsAsync(request.Name))
        {
            return Error.Conflict("SubscriptionPlan.NameAlreadyExists", "A subscription plan with this name already exists.");
        }

        var plan = new SubscriptionPlan(
            Guid.NewGuid(),
            request.Name,
            request.Description,
            request.Price,
            request.BillingCycle,
            request.FreeVisitsIncluded,
            request.DiscountPercent,
            request.PrioritySlotFlag);

        await _auditLogWriter.WriteAsync(new AuditEntry("SubscriptionPlan", plan.Id.ToString(), "Created"));
        await _planRepository.AddAsync(plan);
        return ToResponse(plan);
    }

    public async Task<Result<SubscriptionPlanAdminResponse>> UpdateAsync(Guid id, SubscriptionPlanUpdateRequest request, Guid adminUserId)
    {
        var plan = await _planRepository.GetByIdAsync(id);
        if (plan is null)
        {
            return Error.NotFound("SubscriptionPlan.NotFound", "The specified subscription plan does not exist.");
        }

        plan.Update(
            request.Name,
            request.Description,
            request.Price,
            request.BillingCycle,
            request.FreeVisitsIncluded,
            request.DiscountPercent,
            request.PrioritySlotFlag,
            adminUserId);

        await _auditLogWriter.WriteAsync(new AuditEntry("SubscriptionPlan", plan.Id.ToString(), "Updated"));
        await _planRepository.UpdateAsync(plan);
        return ToResponse(plan);
    }

    public async Task<Result> ActivateAsync(Guid id, Guid adminUserId)
    {
        var plan = await _planRepository.GetByIdAsync(id);
        if (plan is null)
        {
            return Result.Failure(Error.NotFound("SubscriptionPlan.NotFound", "The specified subscription plan does not exist."));
        }

        plan.Activate(adminUserId);
        await _auditLogWriter.WriteAsync(new AuditEntry("SubscriptionPlan", plan.Id.ToString(), "Activated"));
        await _planRepository.UpdateAsync(plan);
        return Result.Success();
    }

    public async Task<Result> DeactivateAsync(Guid id, Guid adminUserId)
    {
        var plan = await _planRepository.GetByIdAsync(id);
        if (plan is null)
        {
            return Result.Failure(Error.NotFound("SubscriptionPlan.NotFound", "The specified subscription plan does not exist."));
        }

        plan.Deactivate(adminUserId);
        await _auditLogWriter.WriteAsync(new AuditEntry("SubscriptionPlan", plan.Id.ToString(), "Deactivated"));
        await _planRepository.UpdateAsync(plan);
        return Result.Success();
    }

    private static SubscriptionPlanAdminResponse ToResponse(SubscriptionPlan plan) => new(
        plan.Id,
        plan.Name,
        plan.Description,
        plan.Price,
        plan.BillingCycle,
        plan.FreeVisitsIncluded,
        plan.DiscountPercent,
        plan.PrioritySlotFlag,
        plan.IsActive,
        plan.CreatedAtUtc,
        plan.UpdatedAtUtc);
}
