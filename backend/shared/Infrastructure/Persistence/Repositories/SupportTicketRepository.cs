using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Application.Support;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class SupportTicketRepository : ISupportTicketRepository
{
    private readonly NestlyDbContext _context;

    public SupportTicketRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(SupportTicket ticket)
    {
        await _context.SupportTickets.AddAsync(ticket);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(SupportTicket ticket)
    {
        // Same guard as BookingRepository.UpdateAsync: only attach+mark-
        // modified when not already tracked by this context. A same-context
        // AddComment() call appends a brand-new SupportTicketComment row -
        // see NewOwnedChildEntityInterceptor for why that needs its own,
        // centralized correction rather than being handled here.
        if (_context.Entry(ticket).State == EntityState.Detached)
        {
            _context.SupportTickets.Update(ticket);
        }

        await _context.SaveChangesAsync();
    }

    public Task<SupportTicket?> GetByIdAsync(Guid id) =>
        _context.SupportTickets
            .Include(t => t.Comments)
            .FirstOrDefaultAsync(t => t.Id == id);

    public async Task<IReadOnlyList<SupportTicket>> ListByCustomerAsync(Guid customerId) =>
        await _context.SupportTickets
            .Where(t => t.CustomerId == customerId)
            .OrderByDescending(t => t.CreatedAtUtc)
            .ToListAsync();

    public async Task<IReadOnlyList<SupportTicket>> ListByBookingAsync(Guid bookingId) =>
        await _context.SupportTickets
            .Where(t => t.BookingId == bookingId)
            .OrderByDescending(t => t.CreatedAtUtc)
            .ToListAsync();

    public async Task<AdminSupportTicketRow?> GetAdminRowByIdAsync(Guid id)
    {
        var ticket = await _context.SupportTickets
            .Include(t => t.Comments)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (ticket is null)
        {
            return null;
        }

        string customerName = await _context.Set<Customer>()
            .Where(c => c.Id == ticket.CustomerId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync() ?? string.Empty;

        string? assignedAdminName = null;
        if (ticket.AssignedAdminUserId is { } adminId)
        {
            assignedAdminName = await _context.Set<AdminUser>()
                .Where(a => a.Id == adminId)
                .Select(a => a.FullName)
                .FirstOrDefaultAsync();
        }

        return new AdminSupportTicketRow(ticket, customerName, assignedAdminName);
    }

    public async Task<AdminSupportTicketSearchResult> SearchAsync(AdminSupportTicketCriteria criteria, int page, int pageSize)
    {
        var filtered = ApplyFilters(_context.SupportTickets, criteria);

        // Same reasoning as ReviewRepository.SearchAsync: count/page the plain
        // ticket query first, join display names only over the already-paged
        // rows.
        int totalCount = await filtered.CountAsync();

        var pagedTickets = filtered
            .OrderByDescending(t => t.CreatedAtUtc)
            .ApplyPaging(page, pageSize);

        var rows = await JoinNames(pagedTickets).ToListAsync();

        // SQL joins do not guarantee preserving the driving query's row order
        // - re-sorting the already-paged (small) in-memory list is cheap and
        // guarantees the newest-first order callers expect.
        var ordered = rows.OrderByDescending(r => r.Ticket.CreatedAtUtc).ToList();

        return new AdminSupportTicketSearchResult(ordered, totalCount);
    }

    /// <summary>Every filter is applied directly against <see cref="SupportTicket"/> columns (SRS 12.14.1), before any join or projection - same ordering rationale as <c>ReviewRepository.ApplyFilters</c>.</summary>
    private static IQueryable<SupportTicket> ApplyFilters(IQueryable<SupportTicket> query, AdminSupportTicketCriteria criteria)
    {
        if (criteria.CustomerId.HasValue)
        {
            query = query.Where(t => t.CustomerId == criteria.CustomerId.Value);
        }

        if (criteria.BookingId.HasValue)
        {
            query = query.Where(t => t.BookingId == criteria.BookingId.Value);
        }

        if (criteria.Category.HasValue)
        {
            query = query.Where(t => t.Category == criteria.Category.Value);
        }

        if (criteria.Priority.HasValue)
        {
            query = query.Where(t => t.Priority == criteria.Priority.Value);
        }

        if (criteria.Status.HasValue)
        {
            query = query.Where(t => t.Status == criteria.Status.Value);
        }

        if (criteria.AssignedAdminUserId.HasValue)
        {
            query = query.Where(t => t.AssignedAdminUserId == criteria.AssignedAdminUserId.Value);
        }

        if (criteria.Unassigned.HasValue)
        {
            query = criteria.Unassigned.Value
                ? query.Where(t => t.AssignedAdminUserId == null)
                : query.Where(t => t.AssignedAdminUserId != null);
        }

        if (criteria.FromUtc.HasValue)
        {
            query = query.Where(t => t.CreatedAtUtc >= criteria.FromUtc.Value);
        }

        if (criteria.ToUtc.HasValue)
        {
            query = query.Where(t => t.CreatedAtUtc <= criteria.ToUtc.Value);
        }

        return query;
    }

    /// <summary>Joins the already-filtered tickets with the customer/assignee display names the admin list/detail screens show (task 120f) - a left join on assignee since <see cref="SupportTicket.AssignedAdminUserId"/> is nullable.</summary>
    private IQueryable<AdminSupportTicketRow> JoinNames(IQueryable<SupportTicket> tickets)
    {
        var query =
            from ticket in tickets
            join customer in _context.Set<Customer>() on ticket.CustomerId equals customer.Id
            join admin in _context.Set<AdminUser>() on ticket.AssignedAdminUserId equals admin.Id into adminJoin
            from admin in adminJoin.DefaultIfEmpty()
            select new AdminSupportTicketRow(ticket, customer.Name, admin != null ? admin.FullName : null);

        return query;
    }
}
