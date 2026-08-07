using Microsoft.EntityFrameworkCore;
using Nestly.Application.Notifications;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class DeviceTokenRepository : IDeviceTokenRepository
{
    private readonly NestlyDbContext _context;

    public DeviceTokenRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(DeviceToken token)
    {
        await _context.DeviceTokens.AddAsync(token);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(DeviceToken token)
    {
        if (_context.Entry(token).State == EntityState.Detached)
        {
            _context.DeviceTokens.Update(token);
        }

        await _context.SaveChangesAsync();
    }

    public Task<DeviceToken?> GetByIdAsync(Guid id) =>
        _context.DeviceTokens.FirstOrDefaultAsync(t => t.Id == id);

    public Task<DeviceToken?> GetByTokenAsync(string token) =>
        _context.DeviceTokens.FirstOrDefaultAsync(t => t.Token == token);

    public async Task<IReadOnlyList<DeviceToken>> ListActiveByOwnerAsync(DeviceTokenOwner owner)
    {
        var ownerId = owner.Id;

        // Both columns are constrained, not just the one matching the caller's
        // kind: a row with both set (which the CHECK constraint forbids, but
        // which a hand-written UPDATE on a pre-constraint database could have
        // left behind) belongs to nobody as far as this query is concerned.
        // Fail closed rather than hand one owner's device to the other.
        var owned = owner.Kind switch
        {
            DeviceTokenOwnerKind.Customer => _context.DeviceTokens.Where(t => t.CustomerId == ownerId && t.ProviderId == null),
            DeviceTokenOwnerKind.Provider => _context.DeviceTokens.Where(t => t.ProviderId == ownerId && t.CustomerId == null),
            _ => throw new NotSupportedException($"Device token owner kind {owner.Kind} has no query wired up yet.")
        };

        return await owned
            .Where(t => t.IsActive)
            .OrderByDescending(t => t.RegisteredAtUtc)
            .ToListAsync();
    }
}
