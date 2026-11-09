using Carpathians.Reviews.Domain.ValueObjects;

namespace Carpathians.Reviews.Infrastructure.Seeding;

/// <summary>Спільні тестові дані — ті самі, що в reviews-db/seed.sql (Лаб. 01).</summary>
internal static class SeedData
{
    public static readonly Author Olena = new("1", "Олена");
    public static readonly Author Andrii = new("2", "Андрій");
    public static readonly Author Maria = new("3", "Марія");

    public const long Route1 = 1;
    public const long Route2 = 2;
    public const long Route3 = 3;

    public const string Route1Name = "Заросляк – Говерла";
    public const string Route2Name = "Заросляк – Несамовите – Говерла";
    public const string Route3Name = "Заросляк – Говерла – Петрос";

    public const string Discussion1Title = "Чи потрібні палиці на Говерлу?";
    public const string Discussion2Title = "Скільки часу йти до озера Несамовите?";
}
