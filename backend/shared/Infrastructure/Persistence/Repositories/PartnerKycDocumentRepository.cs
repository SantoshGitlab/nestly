using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class PartnerKycDocumentRepository : IPartnerKycDocumentRepository
{
    private readonly NestlyDbContext _context;

    public PartnerKycDocumentRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(PartnerKycDocument entity)
    {
        await _context.Set<PartnerKycDocument>().AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(PartnerKycDocument entity)
    {
        _context.Set<PartnerKycDocument>().Update(entity);
        await _context.SaveChangesAsync();
    }

    public Task<PartnerKycDocument?> GetByIdAsync(Guid id) =>
        _context.Set<PartnerKycDocument>().FirstOrDefaultAsync(x => x.Id == id);

    public async Task<IReadOnlyList<PartnerKycDocument>> GetByPartnerAsync(Guid partnerId) =>
        await _context.Set<PartnerKycDocument>()
            .Where(x => x.PartnerId == partnerId)
            .OrderByDescending(x => x.SubmittedAt)
            .ToListAsync();
}
