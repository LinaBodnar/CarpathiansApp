namespace Carpathians.Reviews.Infrastructure.Seeding;

/// <summary>Ідемпотентне наповнення колекції початковими даними.</summary>
public interface IDataSeeder
{
    /// <summary>Порядок виконання: обговорення → відгуки (посилаються на обговорення) → рейтинги.</summary>
    int Order { get; }

    Task SeedAsync(CancellationToken ct = default);
}
