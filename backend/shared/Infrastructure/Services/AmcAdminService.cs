using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Application.Abstractions.Auditing;
using Nestly.Application.Amc;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// See <see cref="IAmcAdminService"/>. Plan CRUD mirrors
/// <see cref="SubscriptionPlanManagementService"/> exactly (duplicate-name
/// rejection, audit log entry per mutation). Contract search/report reads
/// <see cref="NestlyDbContext"/> directly for the customer-name join, the
/// same cross-aggregate read convention <c>CustomerAmcContractRepository.SearchAsync</c>
/// already uses.
/// </summary>
public class AmcAdminService : IAmcAdminService
{
    private readonly IAmcPlanRepository _planRepository;
    private readonly ICustomerAmcContractRepository _contractRepository;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly NestlyDbContext _context;
    private readonly TimeProvider _timeProvider;

    public AmcAdminService(
        IAmcPlanRepository planRepository,
        ICustomerAmcContractRepository contractRepository,
        IAuditLogWriter auditLogWriter,
        NestlyDbContext context,
        TimeProvider timeProvider)
    {
        _planRepository = planRepository;
        _contractRepository = contractRepository;
        _auditLogWriter = auditLogWriter;
        _context = context;
        _timeProvider = timeProvider;
    }

    // ---- Plan catalog CRUD ----

    public async Task<IReadOnlyList<AmcPlanAdminResponse>> ListAllPlansAsync()
    {
        var plans = await _planRepository.ListAllAsync();
        if (plans.Count == 0)
        {
            return Array.Empty<AmcPlanAdminResponse>();
        }

        var categoryNames = await CategoryNamesAsync(plans.Select(p => p.CategoryId));
        IReadOnlyList<AmcPlanAdminResponse> response = plans
            .Select(p => ToPlanResponse(p, CategoryNameOrFallback(categoryNames, p.CategoryId)))
            .ToList();

        return response;
    }

    public async Task<Result<AmcPlanAdminResponse>> GetPlanByIdAsync(Guid id)
    {
        var plan = await _planRepository.GetByIdAsync(id);
        if (plan is null)
        {
            return Error.NotFound("AmcPlan.NotFound", "The specified AMC plan does not exist.");
        }

        var categoryNames = await CategoryNamesAsync([plan.CategoryId]);
        return ToPlanResponse(plan, CategoryNameOrFallback(categoryNames, plan.CategoryId));
    }

    public async Task<Result<AmcPlanAdminResponse>> CreatePlanAsync(AmcPlanCreateRequest request)
    {
        if (await _planRepository.NameExistsAsync(request.Name.Trim()))
        {
            return Error.Conflict("AmcPlan.NameAlreadyExists", "An AMC plan with this name already exists.");
        }

        var plan = new AmcPlan(
            Guid.NewGuid(),
            request.CategoryId,
            request.Name,
            request.Description,
            request.Price,
            request.TermMonths,
            request.VisitsIncluded);

        await _auditLogWriter.WriteAsync(new AuditEntry("AmcPlan", plan.Id.ToString(), "Created"));
        await _planRepository.AddAsync(plan);

        var categoryNames = await CategoryNamesAsync([plan.CategoryId]);
        return ToPlanResponse(plan, CategoryNameOrFallback(categoryNames, plan.CategoryId));
    }

    public async Task<Result<AmcPlanAdminResponse>> UpdatePlanAsync(Guid id, AmcPlanUpdateRequest request, Guid adminUserId)
    {
        var plan = await _planRepository.GetByIdAsync(id);
        if (plan is null)
        {
            return Error.NotFound("AmcPlan.NotFound", "The specified AMC plan does not exist.");
        }

        plan.Update(
            request.CategoryId,
            request.Name,
            request.Description,
            request.Price,
            request.TermMonths,
            request.VisitsIncluded,
            adminUserId);

        await _auditLogWriter.WriteAsync(new AuditEntry("AmcPlan", plan.Id.ToString(), "Updated"));
        await _planRepository.UpdateAsync(plan);

        var categoryNames = await CategoryNamesAsync([plan.CategoryId]);
        return ToPlanResponse(plan, CategoryNameOrFallback(categoryNames, plan.CategoryId));
    }

    public async Task<Result> ActivatePlanAsync(Guid id, Guid adminUserId)
    {
        var plan = await _planRepository.GetByIdAsync(id);
        if (plan is null)
        {
            return Result.Failure(Error.NotFound("AmcPlan.NotFound", "The specified AMC plan does not exist."));
        }

        plan.Activate(adminUserId);
        await _auditLogWriter.WriteAsync(new AuditEntry("AmcPlan", plan.Id.ToString(), "Activated"));
        await _planRepository.UpdateAsync(plan);
        return Result.Success();
    }

    public async Task<Result> DeactivatePlanAsync(Guid id, Guid adminUserId)
    {
        var plan = await _planRepository.GetByIdAsync(id);
        if (plan is null)
        {
            return Result.Failure(Error.NotFound("AmcPlan.NotFound", "The specified AMC plan does not exist."));
        }

        plan.Deactivate(adminUserId);
        await _auditLogWriter.WriteAsync(new AuditEntry("AmcPlan", plan.Id.ToString(), "Deactivated"));
        await _planRepository.UpdateAsync(plan);
        return Result.Success();
    }

    // ---- Contract visibility ----

    public async Task<Result<AmcContractAdminSearchResponse>> SearchContractsAsync(
        CustomerAmcContractStatus? status, string? customerSearch, int page, int pageSize)
    {
        var (items, totalCount) = await _contractRepository.SearchAsync(status, customerSearch, page, pageSize);
        if (items.Count == 0)
        {
            return new AmcContractAdminSearchResponse(Array.Empty<AmcContractAdminListItemResponse>(), totalCount, page, pageSize);
        }

        var customerNames = await CustomerNamesAsync(items.Select(c => c.CustomerId));
        var response = items.Select(c => ToAdminListItem(c, CustomerNameOrFallback(customerNames, c.CustomerId))).ToList();
        return new AmcContractAdminSearchResponse(response, totalCount, page, pageSize);
    }

    public async Task<Result<AmcContractAdminListItemResponse>> GetContractByIdAsync(Guid id)
    {
        var contract = await _contractRepository.GetByIdAsync(id);
        if (contract is null)
        {
            return Error.NotFound("Amc.ContractNotFound", "The specified AMC contract does not exist.");
        }

        var customerNames = await CustomerNamesAsync([contract.CustomerId]);
        return ToAdminListItem(contract, CustomerNameOrFallback(customerNames, contract.CustomerId));
    }

    public async Task<Result<AmcRenewalReportResponse>> GetRenewalReportAsync(DateTime? fromUtc, DateTime? toUtc)
    {
        var horizonFromUtc = fromUtc ?? _timeProvider.GetUtcNow().UtcDateTime;
        var horizonToUtc = toUtc ?? horizonFromUtc.AddDays(30);

        if (horizonToUtc < horizonFromUtc)
        {
            return Error.Validation("Amc.InvalidDateRange", "The 'to' horizon cannot be before the 'from' horizon.");
        }

        var allContracts = await _contractRepository.ListAllForReportAsync();
        var byStatus = ZeroFill(
            allContracts.GroupBy(c => c.Status).Select(g => new AmcContractStatusCount(g.Key, g.Count())).ToList(),
            Enum.GetValues<CustomerAmcContractStatus>(),
            r => r.Status,
            s => new AmcContractStatusCount(s, 0));

        var expiringOrExhausted = await _contractRepository.ListExpiringOrExhaustedAsync(horizonFromUtc, horizonToUtc);
        var customerNames = await CustomerNamesAsync(expiringOrExhausted.Select(c => c.CustomerId));
        var expiringOrExhaustedResponses = expiringOrExhausted
            .Select(c => ToAdminListItem(c, CustomerNameOrFallback(customerNames, c.CustomerId)))
            .ToList();

        return new AmcRenewalReportResponse(
            allContracts.Count,
            byStatus,
            horizonFromUtc,
            horizonToUtc,
            expiringOrExhausted.Count(c => c.Status == CustomerAmcContractStatus.Active),
            expiringOrExhausted.Count(c => c.Status == CustomerAmcContractStatus.Exhausted),
            expiringOrExhaustedResponses);
    }

    // ---- Helpers ----

    private async Task<Dictionary<Guid, string>> CategoryNamesAsync(IEnumerable<Guid> categoryIds)
    {
        var ids = categoryIds.Distinct().ToList();
        return await _context.Set<Category>()
            .Where(c => ids.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name);
    }

    private async Task<Dictionary<Guid, string>> CustomerNamesAsync(IEnumerable<Guid> customerIds)
    {
        var ids = customerIds.Distinct().ToList();
        return await _context.Set<Customer>()
            .Where(c => ids.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name);
    }

    private static string CategoryNameOrFallback(IReadOnlyDictionary<Guid, string> categoryNames, Guid categoryId) =>
        categoryNames.TryGetValue(categoryId, out var name) ? name : string.Empty;

    private static string CustomerNameOrFallback(IReadOnlyDictionary<Guid, string> customerNames, Guid customerId) =>
        customerNames.TryGetValue(customerId, out var name) ? name : string.Empty;

    private static AmcPlanAdminResponse ToPlanResponse(AmcPlan plan, string categoryName) => new(
        plan.Id,
        plan.CategoryId,
        categoryName,
        plan.Name,
        plan.Description,
        plan.Price,
        plan.TermMonths,
        plan.VisitsIncluded,
        plan.IsActive,
        plan.CreatedAtUtc,
        plan.UpdatedAtUtc);

    private static AmcContractAdminListItemResponse ToAdminListItem(CustomerAmcContract contract, string customerName) => new(
        contract.Id,
        contract.CustomerId,
        customerName,
        contract.PlanNameSnapshot,
        contract.AssetLabel,
        contract.Status,
        contract.StartDateUtc,
        contract.EndDateUtc,
        contract.VisitsIncludedSnapshot,
        contract.VisitsRemaining,
        contract.CreatedAtUtc);

    /// <summary>Adds a zero row for every enum member the grouped query returned nothing for, mirroring <c>RecurringBookingPlanAdminService.ZeroFill</c> - a report that silently omits a status reads as "not measured" rather than "there were none".</summary>
    private static IReadOnlyList<TRow> ZeroFill<TRow, TKey>(
        IReadOnlyList<TRow> rows,
        TKey[] allKeys,
        Func<TRow, TKey> keyOf,
        Func<TKey, TRow> emptyRow)
        where TKey : struct
    {
        var byKey = rows.ToDictionary(keyOf);
        return allKeys.Select(key => byKey.TryGetValue(key, out var row) ? row : emptyRow(key)).ToList();
    }
}
