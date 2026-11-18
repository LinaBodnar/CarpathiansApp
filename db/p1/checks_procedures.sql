USE BookingContext;
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
GO

DELETE h FROM dbo.BookingStatusHistory h JOIN dbo.Bookings b ON b.Id = h.BookingId
  JOIN dbo.Customers c ON c.Id = b.CustomerId WHERE c.Email = N'proc-test@example.com';
DELETE p FROM dbo.Payments p JOIN dbo.Bookings b ON b.Id = p.BookingId
  JOIN dbo.Customers c ON c.Id = b.CustomerId WHERE c.Email = N'proc-test@example.com';
DELETE b FROM dbo.Bookings b JOIN dbo.Customers c ON c.Id = b.CustomerId
  WHERE c.Email = N'proc-test@example.com';
DELETE FROM dbo.Departures WHERE RouteId = 900001;
DELETE FROM dbo.Customers WHERE Email = N'proc-test@example.com';
DELETE FROM dbo.RouteRefs WHERE Id = 900001;
GO

DECLARE @CustomerId BIGINT, @DepId BIGINT, @B1 BIGINT, @B2 BIGINT, @B3 BIGINT;

INSERT dbo.RouteRefs (Id, Name) VALUES (900001, N'Тестовий маршрут');
INSERT dbo.Customers (Email, FullName) VALUES (N'proc-test@example.com', N'Процедур Тестенко');
SET @CustomerId = SCOPE_IDENTITY();
INSERT dbo.Departures (RouteId, DepartureDate, Price, SeatsTotal, SeatsAvailable)
VALUES (900001, DATEADD(DAY, 30, CAST(SYSUTCDATETIME() AS DATE)), 1000, 5, 5);
SET @DepId = SCOPE_IDENTITY();

EXEC dbo.usp_CreateBooking @CustomerId = @CustomerId, @DepartureId = @DepId, @Seats = 2,
     @ContactPhone = N'+380501234567', @MeetingPoint = N'Заросляк', @BookingId = @B1 OUTPUT;
SELECT N'1. після створення' AS Step, d.SeatsAvailable AS SeatsAvailable_expect_3,
       b.TotalAmount AS TotalAmount_expect_2000, b.Status AS Status_expect_Submitted
FROM dbo.Departures d, dbo.Bookings b WHERE d.Id = @DepId AND b.Id = @B1;

BEGIN TRY
    EXEC dbo.usp_CreateBooking @CustomerId = @CustomerId, @DepartureId = @DepId, @Seats = 4,
         @ContactPhone = N'+380501234567', @MeetingPoint = N'Заросляк', @BookingId = @B3 OUTPUT;
    PRINT N'ПРОВАЛ: перебронювання пройшло';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    PRINT N'OK (' + CAST(ERROR_NUMBER() AS NVARCHAR(10)) + N', очікується 50003): ' + ERROR_MESSAGE();
END CATCH;

BEGIN TRY
    EXEC dbo.usp_ChangeBookingStatus @BookingId = @B1, @NewStatus = 'Completed';
    PRINT N'ПРОВАЛ: Submitted -> Completed пройшло';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    PRINT N'OK (' + CAST(ERROR_NUMBER() AS NVARCHAR(10)) + N', очікується 50002): ' + ERROR_MESSAGE();
END CATCH;

BEGIN TRY
    EXEC dbo.usp_ChangeBookingStatus @BookingId = @B1, @NewStatus = 'Paid';
    PRINT N'ПРОВАЛ: Paid через ChangeStatus пройшло';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    PRINT N'OK (' + CAST(ERROR_NUMBER() AS NVARCHAR(10)) + N', очікується 50007): ' + ERROR_MESSAGE();
END CATCH;

BEGIN TRY
    EXEC dbo.usp_PayBooking @BookingId = @B1, @Method = 'Card';
    PRINT N'ПРОВАЛ: оплата бронювання у статусі Submitted пройшла';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    PRINT N'OK (' + CAST(ERROR_NUMBER() AS NVARCHAR(10)) + N', очікується 50002): ' + ERROR_MESSAGE();
END CATCH;

BEGIN TRY
    EXEC dbo.usp_ChangeBookingStatus @BookingId = @B1, @NewStatus = 'Shipped';
    PRINT N'ПРОВАЛ: статус Shipped пройшов';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    PRINT N'OK (' + CAST(ERROR_NUMBER() AS NVARCHAR(10)) + N', очікується 50002): ' + ERROR_MESSAGE();
END CATCH;

EXEC dbo.usp_ChangeBookingStatus @BookingId = @B1, @NewStatus = 'AwaitingValidation', @ChangedBy = N'test';
EXEC dbo.usp_ChangeBookingStatus @BookingId = @B1, @NewStatus = 'SeatsConfirmed',     @ChangedBy = N'test';
EXEC dbo.usp_PayBooking          @BookingId = @B1, @Method = 'Card',                  @PaidBy = N'test';
EXEC dbo.usp_ChangeBookingStatus @BookingId = @B1, @NewStatus = 'Completed',          @ChangedBy = N'test';
SELECT N'4. після повного шляху' AS Step, b.Status AS Status_expect_Completed,
       (SELECT COUNT(*) FROM dbo.BookingStatusHistory WHERE BookingId = @B1) AS HistoryRows_expect_5,
       (SELECT COUNT(*) FROM dbo.Payments WHERE BookingId = @B1 AND Status = 'Succeeded') AS Payments_expect_1
FROM dbo.Bookings b WHERE b.Id = @B1;

BEGIN TRY
    EXEC dbo.usp_CancelBooking @BookingId = @B1;
    PRINT N'ПРОВАЛ: скасування завершеного бронювання пройшло';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    PRINT N'OK (' + CAST(ERROR_NUMBER() AS NVARCHAR(10)) + N', очікується 50002): ' + ERROR_MESSAGE();
END CATCH;

EXEC dbo.usp_CreateBooking @CustomerId = @CustomerId, @DepartureId = @DepId, @Seats = 3,
     @ContactPhone = N'+380501234567', @MeetingPoint = N'Заросляк', @BookingId = @B2 OUTPUT;
SELECT N'6a. після другого бронювання' AS Step, SeatsAvailable AS SeatsAvailable_expect_0
FROM dbo.Departures WHERE Id = @DepId;
EXEC dbo.usp_CancelBooking @BookingId = @B2, @ChangedBy = N'test';
SELECT N'6b. після скасування' AS Step, d.SeatsAvailable AS SeatsAvailable_expect_3,
       b.Status AS Status_expect_Cancelled
FROM dbo.Departures d, dbo.Bookings b WHERE d.Id = @DepId AND b.Id = @B2;

EXEC dbo.usp_GetCustomerBookings @CustomerId = @CustomerId, @PageNumber = 1, @PageSize = 10;
EXEC dbo.usp_GetCustomerBookings @CustomerId = @CustomerId, @Status = 'Cancelled';

EXEC dbo.usp_GetBookingById @BookingId = @B1;

BEGIN TRY
    EXEC dbo.usp_GetBookingById @BookingId = -1;
    PRINT N'ПРОВАЛ: неіснуюче бронювання знайдено';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    PRINT N'OK (' + CAST(ERROR_NUMBER() AS NVARCHAR(10)) + N', очікується 50001): ' + ERROR_MESSAGE();
END CATCH;
GO

DELETE h FROM dbo.BookingStatusHistory h JOIN dbo.Bookings b ON b.Id = h.BookingId
  JOIN dbo.Customers c ON c.Id = b.CustomerId WHERE c.Email = N'proc-test@example.com';
DELETE p FROM dbo.Payments p JOIN dbo.Bookings b ON b.Id = p.BookingId
  JOIN dbo.Customers c ON c.Id = b.CustomerId WHERE c.Email = N'proc-test@example.com';
DELETE b FROM dbo.Bookings b JOIN dbo.Customers c ON c.Id = b.CustomerId
  WHERE c.Email = N'proc-test@example.com';
DELETE FROM dbo.Departures WHERE RouteId = 900001;
DELETE FROM dbo.Customers WHERE Email = N'proc-test@example.com';
DELETE FROM dbo.RouteRefs WHERE Id = 900001;
PRINT N'Готово: тестові дані видалено.';
GO
