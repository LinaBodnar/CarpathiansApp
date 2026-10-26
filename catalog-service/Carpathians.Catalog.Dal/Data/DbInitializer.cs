using Carpathians.Dal.Data;
using Carpathians.Domain.Entities;
using Carpathians.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Carpathians.Dal.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(CatalogDbContext context, ILogger logger, CancellationToken ct = default)
    {
        // 1. Довідники: Regions (якщо не було створено міграцією)
        if (!await context.Regions.AnyAsync(ct))
        {
            logger.LogInformation("Seeding Regions...");
            var regions = new[]
            {
                new Region("Чорногора", "Найвищий хребет Українських Карпат, де розташована гора Говерла."),
                new Region("Горгани", "Хребти з кам'яними осипищами та реліктовими лісами."),
                new Region("Свидовець", "Полонинський хребет із відкритими гірськими краєвидами.")
            };
            await context.Regions.AddRangeAsync(regions, ct);
            await context.SaveChangesAsync(ct);
        }

        // 2. Points (точки на карті)
        if (!await context.Points.IgnoreQueryFilters().AnyAsync(ct))
        {
            logger.LogInformation("Seeding Points...");
            var points = new[]
            {
                new MapPoint("Заросляк", 48.1660m, 24.5460m, PointType.Trailhead, null),
                new MapPoint("Ребра", 48.1630m, 24.5200m, PointType.Viewpoint, null),
                new MapPoint("Озеро Несамовите", 48.1550m, 24.5100m, PointType.Lake, 1750),
                new MapPoint("Говерла", 48.1601m, 24.5004m, PointType.Peak, 2061),
                new MapPoint("Петрос", 48.1750m, 24.4400m, PointType.Peak, 2020)
            };

            points[0].AddImage("zaroslyak-1.jpg", "/images/points/zaroslyak-1.jpg", "Початок маршруту", 1);
            points[0].AddImage("zaroslyak-2.jpg", "/images/points/zaroslyak-2.jpg", "Парковка і стежка", 2);
            points[1].AddImage("rebra-1.jpg", "/images/points/rebra-1.jpg", "Вид на хребет", 1);
            points[2].AddImage("nesamovyte-1.jpg", "/images/points/nesamovyte-1.jpg", "Озеро влітку", 1);
            points[3].AddImage("hoverla-1.jpg", "/images/points/hoverla-1.jpg", "Вершина Говерли", 1);
            points[3].AddImage("hoverla-2.jpg", "/images/points/hoverla-2.jpg", "Панорама з вершини", 2);
            points[4].AddImage("petros-1.jpg", "/images/points/petros-1.jpg", "Вершина Петроса", 1);

            await context.Points.AddRangeAsync(points, ct);
            await context.SaveChangesAsync(ct);
        }

        // 3. Routes та залежні сутності (RouteDetails, GearItems, RoutePoints)
        if (!await context.Routes.IgnoreQueryFilters().AnyAsync(ct))
        {
            logger.LogInformation("Seeding Routes with Details, Gear and Points...");

            var chornohora = await context.Regions.FirstAsync(r => r.Name == "Чорногора", ct);
            var pointsMap = await context.Points.ToDictionaryAsync(p => p.Name, ct);

            // Маршрут 1
            var r1 = new TrailRoute(
                chornohora.Id,
                "Заросляк – Говерла",
                "zaroslyak-hoverla",
                Difficulty.Moderate,
                12.0m,
                6.5m,
                1000,
                "seed");

            r1.SetDetail(
                "Класичний підйом на найвищу вершину Українських Карпат від бази Заросляк через Ребра.",
                "Червень – вересень",
                2061,
                "Заросляк",
                "Базова фізична підготовка, трекінгове взуття.");

            r1.AddGearItem("Трекінгове взуття", true, "Із протектором, що тримає на мокрому камінні");
            r1.AddGearItem("Дощовик", true, "Погода в горах змінюється швидко");
            r1.AddGearItem("Тепла кофта", true, null);
            r1.AddGearItem("Трекінгові палиці", false, "Полегшують спуск");

            r1.AddPoint(pointsMap["Заросляк"], 1);
            r1.AddPoint(pointsMap["Ребра"], 2);
            r1.AddPoint(pointsMap["Говерла"], 3);

            // Маршрут 2
            var r2 = new TrailRoute(
                chornohora.Id,
                "Заросляк – Несамовите – Говерла",
                "zaroslyak-nesamovyte-hoverla",
                Difficulty.Moderate,
                15.0m,
                8.0m,
                1150,
                "seed");

            r2.SetDetail(
                "Подовжений маршрут із заходом до озера Несамовите перед підйомом на Говерлу.",
                "Червень – вересень",
                2061,
                "Заросляк",
                "Гарна витривалість, одяг на випадок холоду та вітру.");

            r2.AddGearItem("Трекінгове взуття", true, "Із фіксацією гомілкостопу");
            r2.AddGearItem("Запас води 1.5 л", true, "Джерела є не всюди");
            r2.AddGearItem("Пальник і казанок", false, "Для обіду біля озера");

            r2.AddPoint(pointsMap["Заросляк"], 1);
            r2.AddPoint(pointsMap["Озеро Несамовите"], 2);
            r2.AddPoint(pointsMap["Ребра"], 3);
            r2.AddPoint(pointsMap["Говерла"], 4);

            // Маршрут 3
            var r3 = new TrailRoute(
                chornohora.Id,
                "Заросляк – Говерла – Петрос",
                "zaroslyak-hoverla-petros",
                Difficulty.Hard,
                20.0m,
                10.0m,
                1300,
                "seed");

            r3.SetDetail(
                "Гребеневий перехід від Говерли до Петроса, довгий день у горах.",
                "Липень – вересень",
                2061,
                "Заросляк",
                "Досвід походів, чітке планування часу та погоди.");

            r3.AddGearItem("Трекінгове взуття", true, null);
            r3.AddGearItem("Ліхтарик", true, "Можливий фініш у сутінках");
            r3.AddGearItem("Аптечка", true, "Еластичний бинт, знеболювальне");

            r3.AddPoint(pointsMap["Заросляк"], 1);
            r3.AddPoint(pointsMap["Ребра"], 2);
            r3.AddPoint(pointsMap["Говерла"], 3);
            r3.AddPoint(pointsMap["Петрос"], 4);

            await context.Routes.AddRangeAsync([r1, r2, r3], ct);
            await context.SaveChangesAsync(ct);
        }

        logger.LogInformation("Database seeding completed successfully.");
    }
}
