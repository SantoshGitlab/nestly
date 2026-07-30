using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// Top-level geography master entry (SRS 12.9.1). Cities belong to a state.
/// </summary>
public class State : Entity<Guid>
{
    public string Name { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    protected State() { }

    public State(Guid id, string name, string code) : base(id)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Code = code ?? throw new ArgumentNullException(nameof(code));
        IsActive = true;
    }

    public void Rename(string name) => Name = name ?? throw new ArgumentNullException(nameof(name));
    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
