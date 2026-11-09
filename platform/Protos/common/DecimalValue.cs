namespace Carpathians.Grpc.Common;

/// <summary>
/// Перетворення <see cref="DecimalValue"/> (повідомлення з types.proto) у <see cref="decimal"/> і назад.
/// Файл підключається в кожен проєкт, що генерує код з types.proto (Compile Link у .csproj).
/// </summary>
public sealed partial class DecimalValue
{
    private const decimal NanoFactor = 1_000_000_000m;

    public DecimalValue(long units, int nanos)
    {
        Units = units;
        Nanos = nanos;
    }

    /// <summary>Значення без «хвостових» нулів (12.5, а не 12.500000000) — для коректного JSON.</summary>
    public decimal ToDecimal() => (Units + Nanos / NanoFactor) / 1.0000000000000000000000000000m;

    public static DecimalValue FromDecimal(decimal value)
    {
        var units = decimal.ToInt64(decimal.Truncate(value));
        var nanos = decimal.ToInt32(decimal.Round((value - units) * NanoFactor));
        return new DecimalValue(units, nanos);
    }

    public static implicit operator decimal(DecimalValue value) => value.ToDecimal();

    public static implicit operator DecimalValue(decimal value) => FromDecimal(value);
}
