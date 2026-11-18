-- Лаб. 08: інтеграція з Каталогом через події (RabbitMQ).
-- Скрипт ідемпотентний: повторний запуск нічого не змінює. Застосовується під час старту booking-api
-- (BookingSchemaMigrator) і входить у db/p1/schema.sql для розгортання з нуля.

-- Таблиця оброблених подій: ідемпотентність споживача. EventId записується в ТІЙ САМІЙ транзакції,
-- що й бізнес-зміни; порушення PRIMARY KEY (помилка 2627) означає «подію вже оброблено».
IF OBJECT_ID(N'dbo.ProcessedEvents', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProcessedEvents (
        EventId     UNIQUEIDENTIFIER NOT NULL,
        EventType   NVARCHAR(200)    NOT NULL,
        ProcessedAt DATETIME2        NOT NULL CONSTRAINT DF_ProcessedEvents_ProcessedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_ProcessedEvents PRIMARY KEY (EventId)
    );
END
GO

-- RouteRefs — копія довідника маршрутів Каталогу (політика дублювання, README розділ 9).
-- CatalogChangedAt — час події Каталогу, яку застосовано останньою: подія, що прийшла пізніше,
-- але сталася раніше (out-of-order), не перезапише новіші дані.
IF OBJECT_ID(N'dbo.RouteRefs', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.RouteRefs', N'CatalogChangedAt') IS NULL
    ALTER TABLE dbo.RouteRefs ADD CatalogChangedAt DATETIME2 NULL;
GO

-- IsActive = 0 — маршрут видалено в Каталозі. Рядок лишається: на нього посилаються виїзди й бронювання.
IF OBJECT_ID(N'dbo.RouteRefs', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.RouteRefs', N'IsActive') IS NULL
    ALTER TABLE dbo.RouteRefs ADD IsActive BIT NOT NULL CONSTRAINT DF_RouteRefs_IsActive DEFAULT 1;
GO
