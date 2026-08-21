using Microsoft.EntityFrameworkCore;
using Nestly.Application.ProviderReferral;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class ProviderReferralProgramConfigRepository : IProviderReferralProgramConfigRepository
{
    private readonly NestlyDbContext _context;

    public ProviderReferralProgramConfigRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public Task<ProviderReferralProgramConfig?> GetAsync() =>
        _context.ProviderReferralProgramConfigs.FirstOrDefaultAsync();

    public async Task UpdateAsync(ProviderReferralProgramConfig config)
    {
        _context.ProviderReferralProgramConfigs.Update(config);
        await _context.SaveChangesAsync();
    }
}
