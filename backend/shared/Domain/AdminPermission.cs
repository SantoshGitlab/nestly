using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// A single grantable capability in the permission matrix (SRS 12.2.3 —
/// configurable at least by module/action: View / Create / Edit / Delete /
/// Export / Approve / Refund / Cancel / Reschedule / Publish). <see cref="Code"/>
/// is the stable machine-checked key (e.g. "catalog.write"); <see cref="Module"/>
/// groups permissions for display in the admin permission matrix UI.
/// </summary>
public class AdminPermission : Entity<Guid>
{
    public string Code { get; private set; } = string.Empty;
    public string Module { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    protected AdminPermission() { }

    public AdminPermission(Guid id, string code, string module, string description) : base(id)
    {
        Code = string.IsNullOrWhiteSpace(code)
            ? throw new ArgumentException("Code is required.", nameof(code))
            : code;
        Module = string.IsNullOrWhiteSpace(module)
            ? throw new ArgumentException("Module is required.", nameof(module))
            : module;
        Description = description ?? string.Empty;
        CreatedAt = DateTime.UtcNow;
    }

    public void SetDescription(string description) => Description = description ?? string.Empty;
}
