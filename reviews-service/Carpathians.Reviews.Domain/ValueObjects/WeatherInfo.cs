using Carpathians.Reviews.Domain.Common;
using Carpathians.Reviews.Domain.Exceptions;

namespace Carpathians.Reviews.Domain.ValueObjects;

/// <summary>Погода під час походу: <c>{ tempC, wind, conditions }</c>. Є лише в частині відгуків.</summary>
public sealed class WeatherInfo : ValueObject
{
    public double? TempC { get; }
    public string? Wind { get; }
    public string? Conditions { get; }

    public WeatherInfo(double? tempC, string? wind, string? conditions)
    {
        if (tempC is < -60 or > 60)
            throw new DomainException("Temperature must be between -60 and 60 °C");
        if (tempC is null && string.IsNullOrWhiteSpace(wind) && string.IsNullOrWhiteSpace(conditions))
            throw new DomainException("Weather must contain at least one value");

        TempC = tempC;
        Wind = string.IsNullOrWhiteSpace(wind) ? null : wind.Trim();
        Conditions = string.IsNullOrWhiteSpace(conditions) ? null : conditions.Trim();
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return TempC;
        yield return Wind;
        yield return Conditions;
    }
}
