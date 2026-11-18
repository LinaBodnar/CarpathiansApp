USE BookingContext;
GO
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
GO

INSERT dbo.RouteRefs (Id, Name)
SELECT v.Id, v.Name
FROM (VALUES
    (CAST(1 AS BIGINT), N'Заросляк – Говерла'),
    (CAST(2 AS BIGINT), N'Заросляк – Несамовите – Говерла'),
    (CAST(3 AS BIGINT), N'Заросляк – Говерла – Петрос')
) AS v(Id, Name)
WHERE NOT EXISTS (SELECT 1 FROM dbo.RouteRefs r WHERE r.Id = v.Id);
GO

INSERT dbo.Customers (Email, FullName, Phone, CreatedBy)
SELECT v.Email, v.FullName, v.Phone, N'seed'
FROM (VALUES
    (N'olena.koval@example.com',    N'Олена Коваль',    N'+380501110001'),
    (N'andrii.melnyk@example.com',  N'Андрій Мельник',  N'+380501110002'),
    (N'maria.shevchenko@example.com', N'Марія Шевченко', N'+380501110003')
) AS v(Email, FullName, Phone)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Customers c WHERE c.Email = v.Email);
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Departures)
BEGIN
    DECLARE @Today DATE = CAST(SYSUTCDATETIME() AS DATE);

    INSERT dbo.Departures (RouteId, DepartureDate, Price, SeatsTotal, SeatsAvailable, CreatedBy)
    VALUES
        (1, DATEADD(DAY, 14, @Today), 1500.00, 12, 12, N'seed'),
        (1, DATEADD(DAY, 35, @Today), 1500.00, 12, 12, N'seed'),
        (2, DATEADD(DAY, 21, @Today), 2200.00,  8,  8, N'seed'),
        (3, DATEADD(DAY, 28, @Today), 3200.00,  6,  6, N'seed');
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Bookings)
BEGIN
    DECLARE @C1 BIGINT = (SELECT Id FROM dbo.Customers WHERE Email = N'olena.koval@example.com'),
            @C2 BIGINT = (SELECT Id FROM dbo.Customers WHERE Email = N'andrii.melnyk@example.com'),
            @C3 BIGINT = (SELECT Id FROM dbo.Customers WHERE Email = N'maria.shevchenko@example.com');
    DECLARE @D1 BIGINT = (SELECT TOP 1 Id FROM dbo.Departures WHERE RouteId = 1 ORDER BY DepartureDate),
            @D2 BIGINT = (SELECT TOP 1 Id FROM dbo.Departures WHERE RouteId = 2 ORDER BY DepartureDate),
            @B1 BIGINT, @B2 BIGINT, @B3 BIGINT;

    EXEC dbo.usp_CreateBooking @CustomerId = @C1, @DepartureId = @D1, @Seats = 2,
         @ContactPhone = N'+380501110001', @MeetingPoint = N'Заросляк, парковка',
         @Comment = N'Вдвох, з палицями', @CreatedBy = N'seed', @BookingId = @B1 OUTPUT;
    EXEC dbo.usp_ChangeBookingStatus @BookingId = @B1, @NewStatus = 'AwaitingValidation', @ChangedBy = N'seed';
    EXEC dbo.usp_ChangeBookingStatus @BookingId = @B1, @NewStatus = 'SeatsConfirmed',     @ChangedBy = N'seed';
    EXEC dbo.usp_PayBooking          @BookingId = @B1, @Method = 'Card',                  @PaidBy = N'seed';
    EXEC dbo.usp_ChangeBookingStatus @BookingId = @B1, @NewStatus = 'Completed',          @ChangedBy = N'seed';

    EXEC dbo.usp_CreateBooking @CustomerId = @C2, @DepartureId = @D1, @Seats = 3,
         @ContactPhone = N'+380501110002', @MeetingPoint = N'Заросляк, парковка',
         @CreatedBy = N'seed', @BookingId = @B2 OUTPUT;

    EXEC dbo.usp_CreateBooking @CustomerId = @C3, @DepartureId = @D2, @Seats = 1,
         @ContactPhone = N'+380501110003', @MeetingPoint = N'Заросляк, база',
         @CreatedBy = N'seed', @BookingId = @B3 OUTPUT;
    EXEC dbo.usp_CancelBooking @BookingId = @B3, @ChangedBy = N'seed';
END
GO

SELECT N'Customers' AS TableName, COUNT(*) AS Rows_ FROM dbo.Customers
UNION ALL SELECT N'RouteRefs',  COUNT(*) FROM dbo.RouteRefs
UNION ALL SELECT N'Departures', COUNT(*) FROM dbo.Departures
UNION ALL SELECT N'Bookings',   COUNT(*) FROM dbo.Bookings
UNION ALL SELECT N'BookingStatusHistory', COUNT(*) FROM dbo.BookingStatusHistory;

SELECT b.Id, c.FullName, b.Status, b.TotalAmount, i.RouteName, i.Seats
FROM dbo.Bookings b
JOIN dbo.Customers c ON c.Id = b.CustomerId
JOIN dbo.BookingItems i ON i.BookingId = b.Id
ORDER BY b.Id;
GO
