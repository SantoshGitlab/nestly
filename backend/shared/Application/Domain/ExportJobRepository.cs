using Nestly.Domain;

namespace Nestly.Application;

/// <summary>Persistence for the async export queue (SRS 12.18.2, task 128d).</summary>
public interface IExportJobRepository : IRepository<ExportJob>
{
    /// <summary>Every export job a given admin has requested, newest first (the "my exports" list) - never another admin's, even for a Super Admin, since a job's <see cref="ExportJob.ResultContent"/> is exposed only to its requester.</summary>
    Task<IReadOnlyList<ExportJob>> ListByRequesterAsync(Guid requestedByAdminUserId);
}
