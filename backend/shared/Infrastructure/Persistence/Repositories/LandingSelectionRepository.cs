using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class LandingSelectionRepository : ILandingSelectionRepository
{
    private readonly NestlyDbContext _context;

    public LandingSelectionRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<LandingSelection>> ListAllAsync() =>
        await _context.Set<LandingSelection>()
            .AsNoTracking()
            .OrderBy(s => s.SectionType)
            .ThenBy(s => s.SortOrder)
            .ToListAsync();

    public async Task<IReadOnlyList<LandingSelection>> ListBySectionAsync(LandingSectionType sectionType) =>
        await _context.Set<LandingSelection>()
            .AsNoTracking()
            .Where(s => s.SectionType == sectionType)
            .OrderBy(s => s.SortOrder)
            .ToListAsync();

    public Task ReplaceSectionAsync(LandingSectionType sectionType, IReadOnlyList<LandingSelection> replacements) =>
        ReplaceAsync(s => s.SectionType == sectionType, replacements);

    public Task ReplaceCategorySectionAsync(Guid categoryId, IReadOnlyList<LandingSelection> replacements) =>
        ReplaceAsync(
            s => s.SectionType == LandingSectionType.CategorySection && s.CategoryId == categoryId,
            replacements);

    /// <summary>
    /// Delete-then-insert inside one transaction: a partial write would leave
    /// the home page showing a half-updated section, so the old rows only
    /// disappear if the new ones land.
    /// </summary>
    private async Task ReplaceAsync(
        System.Linq.Expressions.Expression<Func<LandingSelection, bool>> scope,
        IReadOnlyList<LandingSelection> replacements)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        var existing = await _context.Set<LandingSelection>().Where(scope).ToListAsync();
        _context.Set<LandingSelection>().RemoveRange(existing);

        if (replacements.Count > 0)
        {
            await _context.Set<LandingSelection>().AddRangeAsync(replacements);
        }

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
