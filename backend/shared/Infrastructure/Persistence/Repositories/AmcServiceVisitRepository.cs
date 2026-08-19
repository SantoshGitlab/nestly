using Microsoft.EntityFrameworkCore;
using Nestly.Application.Amc;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class AmcServiceVisitRepository : IAmcServiceVisitRepository
{
    private readonly NestlyDbContext _context;

    public AmcServiceVisitRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(AmcServiceVisit visit)
    {
        await _context.AmcServiceVisits.AddAsync(visit);
        await _context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<AmcServiceVisit>> ListByContractAsync(Guid contractId) =>
        await _context.AmcServiceVisits
            .Where(v => v.ContractId == contractId)
            .OrderBy(v => v.ConsumedAtUtc)
            .ToListAsync();
}
