using Microsoft.EntityFrameworkCore;
using Nestly.Application.Referral;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class ReferralProgramConfigRepository : IReferralProgramConfigRepository
{
    private readonly NestlyDbContext _context;

    public ReferralProgramConfigRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public Task<ReferralProgramConfig?> GetAsync() =>
        _context.ReferralProgramConfigs.FirstOrDefaultAsync();

    public async Task UpdateAsync(ReferralProgramConfig config)
    {
        _context.ReferralProgramConfigs.Update(config);
        await _context.SaveChangesAsync();
    }
}
