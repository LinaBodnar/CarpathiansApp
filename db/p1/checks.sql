USE BookingContext;
SET NOCOUNT ON;
GO

SELECT name AS TableName FROM sys.tables ORDER BY name;

SELECT fk.name                              AS FkName,
       OBJECT_NAME(fk.parent_object_id)     AS FromTable,
       OBJECT_NAME(fk.referenced_object_id) AS ToTable,
       fk.delete_referential_action_desc    AS OnDelete   
FROM sys.foreign_keys fk
ORDER BY FromTable, FkName;

SELECT OBJECT_NAME(object_id) AS TableName, name AS IndexName,
       is_primary_key, is_unique, has_filter, filter_definition
FROM sys.indexes
WHERE name IS NOT NULL AND OBJECT_SCHEMA_NAME(object_id) = 'dbo'
ORDER BY TableName, IndexName;
GO

BEGIN TRANSACTION;

INSERT dbo.RouteRefs (Id, Name) VALUES (1, N'Заросляк - Говерла');
INSERT dbo.Customers (Email, FullName) VALUES (N'test@example.com', N'Тест Тестенко');
DECLARE @CustomerId BIGINT = SCOPE_IDENTITY();

INSERT dbo.Departures (RouteId, DepartureDate, Price, SeatsTotal, SeatsAvailable)
VALUES (1, '2026-12-01', 1500, 10, 10);
DECLARE @Dep1 BIGINT = SCOPE_IDENTITY();

INSERT dbo.Departures (RouteId, DepartureDate, Price, SeatsTotal, SeatsAvailable)
VALUES (1, '2026-12-08', 1600, 10, 10);
DECLARE @Dep2 BIGINT = SCOPE_IDENTITY();

INSERT dbo.Bookings (CustomerId) VALUES (@CustomerId);   
DECLARE @BookingId BIGINT = SCOPE_IDENTITY();

INSERT dbo.BookingDetails (BookingId, ContactPhone, MeetingPoint)
VALUES (@BookingId, N'+380501234567', N'Заросляк');
INSERT dbo.BookingItems (BookingId, DepartureId, RouteName, UnitPrice, Seats)
VALUES (@BookingId, @Dep1, N'Заросляк - Говерла', 1500, 2);

SELECT Id, Status, BookingDate FROM dbo.Bookings WHERE Id = @BookingId;   

BEGIN TRY
    INSERT dbo.Departures (RouteId, DepartureDate, Price, SeatsTotal, SeatsAvailable)
    VALUES (1, '2026-12-15', -5, 10, 10);
    PRINT N'ПРОВАЛ: від''ємна ціна пройшла';
END TRY BEGIN CATCH PRINT N'OK (CHECK ціни): ' + ERROR_MESSAGE(); END CATCH;

BEGIN TRY
    INSERT dbo.Departures (RouteId, DepartureDate, Price, SeatsTotal, SeatsAvailable)
    VALUES (1, '2026-12-16', 100, 10, 11);
    PRINT N'ПРОВАЛ: SeatsAvailable > SeatsTotal пройшло';
END TRY BEGIN CATCH PRINT N'OK (CHECK місць): ' + ERROR_MESSAGE(); END CATCH;

BEGIN TRY
    INSERT dbo.Departures (RouteId, DepartureDate, Price, SeatsTotal, SeatsAvailable)
    VALUES (1, '2026-12-01', 100, 10, 10);
    PRINT N'ПРОВАЛ: дубль виїзду пройшов';
END TRY BEGIN CATCH PRINT N'OK (UNIQUE виїзду): ' + ERROR_MESSAGE(); END CATCH;

BEGIN TRY
    INSERT dbo.Customers (Email, FullName) VALUES (N'test@example.com', N'Інший');
    PRINT N'ПРОВАЛ: дубль email пройшов';
END TRY BEGIN CATCH PRINT N'OK (UNIQUE email): ' + ERROR_MESSAGE(); END CATCH;

BEGIN TRY
    INSERT dbo.Bookings (CustomerId, Status) VALUES (@CustomerId, 'Shipped');
    PRINT N'ПРОВАЛ: статус Shipped пройшов';
END TRY BEGIN CATCH PRINT N'OK (CHECK статусу): ' + ERROR_MESSAGE(); END CATCH;

BEGIN TRY
    INSERT dbo.BookingItems (BookingId, DepartureId, RouteName, UnitPrice, Seats)
    VALUES (@BookingId, @Dep2, N'Заросляк - Говерла', 1600, 0);
    PRINT N'ПРОВАЛ: Seats = 0 пройшло';
END TRY BEGIN CATCH PRINT N'OK (CHECK Seats): ' + ERROR_MESSAGE(); END CATCH;

BEGIN TRY
    INSERT dbo.Bookings (CustomerId) VALUES (999999);
    PRINT N'ПРОВАЛ: неіснуючий клієнт пройшов';
END TRY BEGIN CATCH PRINT N'OK (FK клієнта): ' + ERROR_MESSAGE(); END CATCH;

BEGIN TRY
    INSERT dbo.BookingDetails (BookingId, ContactPhone, MeetingPoint)
    VALUES (@BookingId, N'+380509999999', N'Інше місце');
    PRINT N'ПРОВАЛ: 1:1 порушено';
END TRY BEGIN CATCH PRINT N'OK (1:1 BookingDetails): ' + ERROR_MESSAGE(); END CATCH;

BEGIN TRY
    DELETE dbo.Customers WHERE Id = @CustomerId;
    PRINT N'ПРОВАЛ: клієнта з бронюваннями видалено';
END TRY BEGIN CATCH PRINT N'OK (немає каскаду від клієнта): ' + ERROR_MESSAGE(); END CATCH;

DELETE dbo.Bookings WHERE Id = @BookingId;
SELECT (SELECT COUNT(*) FROM dbo.BookingDetails WHERE BookingId = @BookingId) AS DetailsLeft,   
       (SELECT COUNT(*) FROM dbo.BookingItems   WHERE BookingId = @BookingId) AS ItemsLeft;     

ROLLBACK TRANSACTION;
PRINT N'Готово: тестові дані відкочено.';
GO
