USE CatalogContext;
GO
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
GO

-- 1. Regions (Довідники з фіксованими Id)
IF NOT EXISTS (SELECT 1 FROM dbo.Regions WHERE Name = N'Чорногора')
    INSERT INTO dbo.Regions (Name, Description) VALUES (N'Чорногора', N'Найвищий хребет Українських Карпат, де розташована гора Говерла.');
IF NOT EXISTS (SELECT 1 FROM dbo.Regions WHERE Name = N'Горгани')
    INSERT INTO dbo.Regions (Name, Description) VALUES (N'Горгани', N'Хребти з кам''яними осипищами та реліктовими лісами.');
IF NOT EXISTS (SELECT 1 FROM dbo.Regions WHERE Name = N'Свидовець')
    INSERT INTO dbo.Regions (Name, Description) VALUES (N'Свидовець', N'Полонинський хребет із відкритими гірськими краєвидами.');
GO

-- 2. Points (Ключові локації)
INSERT dbo.Points (Name, Latitude, Longitude, AltitudeM, PointType)
SELECT v.Name, v.Latitude, v.Longitude, v.AltitudeM, v.PointType
FROM (VALUES
    (N'Заросляк',          CAST(48.1660 AS DECIMAL(9,6)), CAST(24.5460 AS DECIMAL(9,6)), CAST(NULL AS INT), N'Trailhead'),
    (N'Ребра',             CAST(48.1630 AS DECIMAL(9,6)), CAST(24.5200 AS DECIMAL(9,6)), CAST(NULL AS INT), N'Viewpoint'),
    (N'Озеро Несамовите',  CAST(48.1550 AS DECIMAL(9,6)), CAST(24.5100 AS DECIMAL(9,6)), 1750,             N'Lake'),
    (N'Говерла',           CAST(48.1601 AS DECIMAL(9,6)), CAST(24.5004 AS DECIMAL(9,6)), 2061,             N'Peak'),
    (N'Петрос',            CAST(48.1750 AS DECIMAL(9,6)), CAST(24.4400 AS DECIMAL(9,6)), 2020,             N'Peak')
) AS v(Name, Latitude, Longitude, AltitudeM, PointType)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Points p WHERE p.Name = v.Name);
GO

-- 3. GalleryImages (Фотографії точок)
INSERT dbo.GalleryImages (PointId, FileName, Url, Caption, SortOrder)
SELECT p.Id, v.FileName, v.Url, v.Caption, v.SortOrder
FROM (VALUES
    (N'Заросляк',         N'zaroslyak-1.jpg',  N'/images/points/zaroslyak-1.jpg',  N'Початок маршруту',      1),
    (N'Заросляк',         N'zaroslyak-2.jpg',  N'/images/points/zaroslyak-2.jpg',  N'Парковка і стежка',     2),
    (N'Ребра',            N'rebra-1.jpg',      N'/images/points/rebra-1.jpg',      N'Вид на хребет',         1),
    (N'Озеро Несамовите', N'nesamovyte-1.jpg', N'/images/points/nesamovyte-1.jpg', N'Озеро влітку',          1),
    (N'Говерла',          N'hoverla-1.jpg',    N'/images/points/hoverla-1.jpg',    N'Вершина Говерли',       1),
    (N'Говерла',          N'hoverla-2.jpg',    N'/images/points/hoverla-2.jpg',    N'Панорама з вершини',    2),
    (N'Петрос',           N'petros-1.jpg',     N'/images/points/petros-1.jpg',     N'Вершина Петроса',       1)
) AS v(PointName, FileName, Url, Caption, SortOrder)
JOIN dbo.Points p ON p.Name = v.PointName
WHERE NOT EXISTS (SELECT 1 FROM dbo.GalleryImages g WHERE g.PointId = p.Id AND g.FileName = v.FileName);
GO

-- 4. Routes (Маршрути)
INSERT dbo.Routes (RegionId, Name, Slug, Difficulty, DistanceKm, DurationHours, ElevationGainM, CreatedBy)
SELECT rg.Id, v.Name, v.Slug, v.Difficulty, v.DistanceKm, v.DurationHours, v.ElevationGainM, N'seed'
FROM (VALUES
    (1, N'Чорногора', N'Заросляк – Говерла',                N'zaroslyak-hoverla',          N'Moderate', CAST(12.0 AS DECIMAL(6,2)), CAST(6.5 AS DECIMAL(5,1)), 1000),
    (2, N'Чорногора', N'Заросляк – Несамовите – Говерла',   N'zaroslyak-nesamovyte-hoverla', N'Moderate', CAST(15.0 AS DECIMAL(6,2)), CAST(8.0 AS DECIMAL(5,1)), 1150),
    (3, N'Чорногора', N'Заросляк – Говерла – Петрос',       N'zaroslyak-hoverla-petros',   N'Hard',     CAST(20.0 AS DECIMAL(6,2)), CAST(10.0 AS DECIMAL(5,1)), 1300)
) AS v(Seq, RegionName, Name, Slug, Difficulty, DistanceKm, DurationHours, ElevationGainM)
JOIN dbo.Regions rg ON rg.Name = v.RegionName
WHERE NOT EXISTS (SELECT 1 FROM dbo.Routes r WHERE r.Slug = v.Slug)
ORDER BY v.Seq;
GO

-- 5. RouteDetails (1:1 Деталі)
INSERT dbo.RouteDetails (RouteId, Description, Season, MaxAltitudeM, StartPoint, Requirements)
SELECT r.Id, v.Description, v.Season, v.MaxAltitudeM, v.StartPoint, v.Requirements
FROM (VALUES
    (N'zaroslyak-hoverla',
     N'Класичний підйом на найвищу вершину Українських Карпат від бази Заросляк через Ребра.',
     N'Червень – вересень', 2061, N'Заросляк', N'Базова фізична підготовка, трекінгове взуття.'),
    (N'zaroslyak-nesamovyte-hoverla',
     N'Подовжений маршрут із заходом до озера Несамовите перед підйомом на Говерлу.',
     N'Червень – вересень', 2061, N'Заросляк', N'Гарна витривалість, одяг на випадок холоду та вітру.'),
    (N'zaroslyak-hoverla-petros',
     N'Гребеневий перехід від Говерли до Петроса, довгий день у горах.',
     N'Липень – вересень', 2061, N'Заросляк', N'Досвід походів, чітке планування часу та погоди.')
) AS v(Slug, Description, Season, MaxAltitudeM, StartPoint, Requirements)
JOIN dbo.Routes r ON r.Slug = v.Slug
WHERE NOT EXISTS (SELECT 1 FROM dbo.RouteDetails d WHERE d.RouteId = r.Id);
GO

-- 6. RoutePoints (M:N Ланцюжки точок)
INSERT dbo.RoutePoints (RouteId, PointId, SequenceNo)
SELECT r.Id, p.Id, v.SequenceNo
FROM (VALUES
    (N'zaroslyak-hoverla',            N'Заросляк',         1),
    (N'zaroslyak-hoverla',            N'Ребра',            2),
    (N'zaroslyak-hoverla',            N'Говерла',          3),
    (N'zaroslyak-nesamovyte-hoverla', N'Заросляк',         1),
    (N'zaroslyak-nesamovyte-hoverla', N'Озеро Несамовите', 2),
    (N'zaroslyak-nesamovyte-hoverla', N'Ребра',            3),
    (N'zaroslyak-nesamovyte-hoverla', N'Говерла',          4),
    (N'zaroslyak-hoverla-petros',     N'Заросляк',         1),
    (N'zaroslyak-hoverla-petros',     N'Ребра',            2),
    (N'zaroslyak-hoverla-petros',     N'Говерла',          3),
    (N'zaroslyak-hoverla-petros',     N'Петрос',           4)
) AS v(Slug, PointName, SequenceNo)
JOIN dbo.Routes r ON r.Slug = v.Slug
JOIN dbo.Points p ON p.Name = v.PointName
WHERE NOT EXISTS (SELECT 1 FROM dbo.RoutePoints x WHERE x.RouteId = r.Id AND x.PointId = p.Id);
GO

-- 7. GearItems (1:N Спорядження)
INSERT dbo.GearItems (RouteId, Name, IsRequired, Notes)
SELECT r.Id, v.Name, v.IsRequired, v.Notes
FROM (VALUES
    (N'zaroslyak-hoverla',            N'Трекінгове взуття',   CAST(1 AS BIT), N'Із протектором, що тримає на мокрому камінні'),
    (N'zaroslyak-hoverla',            N'Дощовик',             CAST(1 AS BIT), N'Погода в горах змінюється швидко'),
    (N'zaroslyak-hoverla',            N'Тепла кофта',         CAST(1 AS BIT), NULL),
    (N'zaroslyak-hoverla',            N'Трекінгові палиці',   CAST(0 AS BIT), N'Полегшують спуск'),
    (N'zaroslyak-nesamovyte-hoverla', N'Трекінгове взуття',   CAST(1 AS BIT), N'Із фіксацією гомілкостопу'),
    (N'zaroslyak-nesamovyte-hoverla', N'Запас води 1.5 л',    CAST(1 AS BIT), N'Джерела є не всюди'),
    (N'zaroslyak-nesamovyte-hoverla', N'Пальник і казанок',   CAST(0 AS BIT), N'Для обіду біля озера'),
    (N'zaroslyak-hoverla-petros',     N'Трекінгове взуття',   CAST(1 AS BIT), NULL),
    (N'zaroslyak-hoverla-petros',     N'Ліхтарик',            CAST(1 AS BIT), N'Можливий фініш у сутінках'),
    (N'zaroslyak-hoverla-petros',     N'Аптечка',             CAST(1 AS BIT), N'Еластичний бинт, знеболювальне')
) AS v(Slug, Name, IsRequired, Notes)
JOIN dbo.Routes r ON r.Slug = v.Slug
WHERE NOT EXISTS (SELECT 1 FROM dbo.GearItems g WHERE g.RouteId = r.Id AND g.Name = v.Name);
GO
