namespace Carpathians.Reviews.Domain.Common;

/// <summary>
/// Базовий клас Value Object: об'єкти порівнюються за значеннями компонентів, а не за посиланням.
/// </summary>
public abstract class ValueObject : IEquatable<ValueObject>
{
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public bool Equals(ValueObject? other)
        => other is not null
           && GetType() == other.GetType()
           && GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());

    public override bool Equals(object? obj) => obj is ValueObject other && Equals(other);

    public override int GetHashCode()
        => GetEqualityComponents()
            .Aggregate(0, (hash, part) => HashCode.Combine(hash, part));

    public static bool operator ==(ValueObject? left, ValueObject? right)
        => left is null ? right is null : left.Equals(right);

    public static bool operator !=(ValueObject? left, ValueObject? right) => !(left == right);
}
