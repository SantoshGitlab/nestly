using Nesty;

namespace Nesty.Domain;

public class Service : Entity<Guid>
{
    public Guid CategoryId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public bool IsActive { get; private set; }

    protected Service() { }

    public Service(Guid id, Guid categoryId, string name, string description, decimal price) : base(id)
    {
        CategoryId = categoryId;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Description = description ?? string.Empty;
        Price = price > 0 ? price : throw new ArgumentOutOfRangeException(nameof(price));
        IsActive = true;
    }

    public void SetName(string name) => Name = name ?? throw new ArgumentNullException(nameof(name));
    public void SetDescription(string d) => Description = d ?? string.Empty;
    public void SetPrice(decimal price) => Price = price > 0 ? price : throw new ArgumentOutOfRangeException(nameof(price));
    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
