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

    public async Task<IReadOnlyList<DeviceToken>> ListActiveByCustomerAsync(Guid customerId) =>
        await _context.DeviceTokens
            .Where(t => t.CustomerId == customerId && t.IsActive)
            .OrderByDescending(t => t.RegisteredAtUtc)
            .ToListAsync();
}
