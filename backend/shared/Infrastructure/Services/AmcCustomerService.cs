using Microsoft.EntityFrameworkCore;
using Nestly.Application.Amc;
using Nestly.Application.Bookings;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// See <see cref="IAmcCustomerService"/>. Reads <see cref="NestlyDbContext"/>
/// directly for the category-name lookup (docs/AMC.md), the same
/// cross-aggregate read convention <c>CustomerAmcContractRepository.SearchAsync</c>
/// already uses for customer name/mobile search - <see cref="Category"/> has
/// no dedicated read-only query service worth adding for a single name join.
/// </summary>
public class AmcCustomerService : IAmcCustomerService
{
    private readonly IAmcPlanRepository _planRepository;
    private readonly ICustomerAmcContractRepository _contractRepository;
    private readonly IAmcServiceVisitRepository _visitRepository;
    private readonly IBookingService _bookingService;
    private readonly NestlyDbContext _context;
    private readonly TimeProvider _timeProvider;

    public AmcCustomerService(
        IAmcPlanRepository planRepository,
        ICustomerAmcContractRepository contractRepository,
        IAmcServiceVisitRepository visitRepository,
        IBookingService bookingService,
        NestlyDbContext context,
        TimeProvider timeProvider)
    {
        _planRepository = planRepository;
        _contractRepository = contractRepository;
        _visitRepository = visitRepository;
        _bookingService = bookingService;
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<AmcPlanBrowseResponse>> BrowsePlansAsync(Guid? categoryId = null)
    {
        var plans = await _planRepository.ListActiveAsync();
        if (categoryId is { } filterCategoryId)
        {
            plans = plans.Where(p => p.CategoryId == filterCategoryId).ToList();
        }

        if (plans.Count == 0)
        {
            return Array.Empty<AmcPlanBrowseResponse>();
        }

        var categoryNames = await CategoryNamesAsync(plans.Select(p => p.CategoryId));
        IReadOnlyList<AmcPlanBrowseResponse> response = plans
            .Select(p => new AmcPlanBrowseResponse(
                p.Id, p.CategoryId, CategoryNameOrFallback(categoryNames, p.CategoryId),
                p.Name, p.Description, p.Price, p.TermMonths, p.VisitsIncluded))
            .ToList();

        return response;
    }

    public async Task<Result<MyAmcContractResponse>> PurchaseAsync(Guid customerId, AmcContractPurchaseRequest request)
    {
        var plan = await _planRepository.GetByIdAsync(request.PlanId);
        if (plan is null || !plan.IsActive)
        {
            return Error.NotFound("Amc.PlanNotFound", "The specified AMC plan is not available.");
        }

        // Purchase does not charge a real payment gateway order for this MVP
        // (docs/AMC.md OPEN DECISIONS #4) - paymentTransactionId is left null.
        var contract = new CustomerAmcContract(
            Guid.NewGuid(), customerId, plan, request.AssetLabel,
            paymentTransactionId: null, _timeProvider.GetUtcNow().UtcDateTime);

        await _contractRepository.AddAsync(contract);

        var categoryNames = await CategoryNamesAsync([contract.CategoryIdSnapshot]);
        return ToMyResponse(contract, CategoryNameOrFallback(categoryNames, contract.CategoryIdSnapshot), Array.Empty<AmcServiceVisit>(), _timeProvider.GetUtcNow().UtcDateTime);
    }

    public async Task<Result<IReadOnlyList<MyAmcContractResponse>>> ListMyContractsAsync(Guid customerId)
    {
        var contracts = await _contractRepository.ListByCustomerAsync(customerId);
        if (contracts.Count == 0)
        {
            IReadOnlyList<MyAmcContractResponse> empty = Array.Empty<MyAmcContractResponse>();
            return empty;
        }

        var categoryNames = await CategoryNamesAsync(contracts.Select(c => c.CategoryIdSnapshot));
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var responses = new List<MyAmcContractResponse>(contracts.Count);
        foreach (var contract in contracts)
        {
            var visits = await _visitRepository.ListByContractAsync(contract.Id);
            responses.Add(ToMyResponse(contract, CategoryNameOrFallback(categoryNames, contract.CategoryIdSnapshot), visits, nowUtc));
        }

        IReadOnlyList<MyAmcContractResponse> result = responses;
        return result;
    }

    public async Task<Result<MyAmcContractResponse>> GetMyContractAsync(Guid customerId, Guid contractId)
    {
        var contract = await _contractRepository.GetByIdWithVisitsAsync(contractId);
        if (contract is null || contract.CustomerId != customerId)
        {
            return Error.NotFound("Amc.ContractNotFound", "The specified AMC contract does not exist.");
        }

        var visits = await _visitRepository.ListByContractAsync(contract.Id);
        var categoryNames = await CategoryNamesAsync([contract.CategoryIdSnapshot]);
        return ToMyResponse(contract, CategoryNameOrFallback(categoryNames, contract.CategoryIdSnapshot), visits, _timeProvider.GetUtcNow().UtcDateTime);
    }

    public async Task<Result> CancelAsync(Guid customerId, Guid contractId)
    {
        var contract = await _contractRepository.GetByIdAsync(contractId);
        if (contract is null || contract.CustomerId != customerId)
        {
            return Result.Failure(Error.NotFound("Amc.ContractNotFound", "The specified AMC contract does not exist."));
        }

        try
        {
            contract.Cancel(_timeProvider.GetUtcNow().UtcDateTime);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(Error.Business("Amc.CannotCancel", ex.Message));
        }

        await _contractRepository.UpdateAsync(contract);
        return Result.Success();
    }

    public async Task<Result<BookingDetailResponse>> RedeemVisitAsync(Guid customerId, Guid contractId, BookingSummaryRequest request)
    {
        var contract = await _contractRepository.GetByIdAsync(contractId);
        if (contract is null || contract.CustomerId != customerId)
        {
            return Error.NotFound("Amc.ContractNotFound", "The specified AMC contract does not exist.");
        }

        if (!contract.CanRedeem(_timeProvider.GetUtcNow().UtcDateTime))
        {
            return Error.Business("Amc.CannotRedeem", "This contract has no entitlement remaining, or its term has ended or it is not active.");
        }

        // The booking is created zero-priced and linked back to the contract;
        // entitlement is NOT decremented here - see docs/AMC.md's "on
        // completion, not creation" rule, handled by
        // AmcVisitOnBookingCompletionHandler once the booking reaches Completed.
        return await _bookingService.CreateAsync(customerId, request, recurringBookingPlanId: null, amcContractId: contract.Id);
    }

    private async Task<Dictionary<Guid, string>> CategoryNamesAsync(IEnumerable<Guid> categoryIds)
    {
        var ids = categoryIds.Distinct().ToList();
        return await _context.Set<Category>()
            .Where(c => ids.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name);
    }

    private static string CategoryNameOrFallback(IReadOnlyDictionary<Guid, string> categoryNames, Guid categoryId) =>
        categoryNames.TryGetValue(categoryId, out var name) ? name : string.Empty;

    private static MyAmcContractResponse ToMyResponse(CustomerAmcContract contract, string categoryName, IReadOnlyList<AmcServiceVisit> visits, DateTime nowUtc) => new(
        contract.Id,
        contract.PlanNameSnapshot,
        categoryName,
        contract.PriceSnapshot,
        contract.TermMonthsSnapshot,
        contract.VisitsIncludedSnapshot,
        contract.AssetLabel,
        contract.Status,
        contract.StartDateUtc,
        contract.EndDateUtc,
        contract.VisitsRemaining,
        contract.CanRedeem(nowUtc),
        contract.CreatedAtUtc,
        contract.CancelledAtUtc,
        visits.Select(v => new AmcServiceVisitResponse(v.Id, v.BookingId, v.ConsumedAtUtc)).ToList());
}
