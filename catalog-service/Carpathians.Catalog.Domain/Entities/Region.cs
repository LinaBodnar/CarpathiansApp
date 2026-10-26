using Carpathians.Domain.Exceptions;

namespace Carpathians.Domain.Entities;

public class Region
{
    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public List<TrailRoute> Routes { get; } = new();

    private Region() { }

    public Region(string name, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Region name is required");

        Name = name.Trim();
        Description = description?.Trim();
    }

    public void Update(string name, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Region name is required");

        Name = name.Trim();
        Description = description?.Trim();
    }
}
