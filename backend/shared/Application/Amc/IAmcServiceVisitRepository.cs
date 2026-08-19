using Nestly.Domain;

namespace Nestly.Application.Amc;

public interface IAmcServiceVisitRepository
{
    Task AddAsync(AmcServiceVisit visit);

    /// <summary>A contract's redemption history, oldest first (so "visit 1 of 4" reads naturally) - separate from loading the contract itself, mirroring <c>IRecurringBookingOccurrenceRepository</c>'s split from its plan for the same reason (see <see cref="AmcServiceVisit"/>'s doc comment).</summary>
    Task<IReadOnlyList<AmcServiceVisit>> ListByContractAsync(Guid contractId);
}
