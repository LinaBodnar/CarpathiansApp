using Carpathians.Domain.Exceptions;

namespace Carpathians.Domain.Entities;

public class GearItem
{
    public long Id { get; private set; }
    public long RouteId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsRequired { get; private set; }
    public string? Notes { get; private set; }

    private GearItem() { }

    internal GearItem(string name, bool isRequired, string? notes)
    {
        Update(name, isRequired, notes);
    }

    public void Update(string name, bool isRequired, string? notes)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Gear name is required");

        Name = name.Trim();
        IsRequired = isRequired;
        Notes = notes?.Trim();
    }
}
