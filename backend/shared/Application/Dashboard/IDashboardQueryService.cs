using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Dashboard;

/// <summary>Computes the admin dashboard's KPI widgets (SRS 12.3, task 99) for a caller-supplied date range/city/category filter.</summary>
public interface IDashboardQueryService
{
    Task<Result<DashboardKpiResponse>> GetKpisAsync(DashboardFilterRequest filter);
}
