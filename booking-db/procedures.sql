USE BookingContext;
GO
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

CREATE OR ALTER FUNCTION dbo.fn_IsValidStatusTransition
(
    @OldStatus NVARCHAR(30),
    @NewStatus NVARCHAR(30)
)
RETURNS BIT
AS
BEGIN
    RETURN CASE
        WHEN @OldStatus = 'Submitted'          AND @NewStatus IN ('AwaitingValidation', 'Cancelled') THEN 1
        WHEN @OldStatus = 'AwaitingValidation' AND @NewStatus IN ('SeatsConfirmed', 'Cancelled')     THEN 1
        WHEN @OldStatus = 'SeatsConfirmed'     AND @NewStatus IN ('Paid', 'Cancelled')               THEN 1
        WHEN @OldStatus = 'Paid'               AND @NewStatus = 'Completed'                          THEN 1
        ELSE 0
    END;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_CreateBooking
    @CustomerId   BIGINT,
    @DepartureId  BIGINT,
    @Seats        INT,
    @ContactPhone NVARCHAR(30),
    @MeetingPoint NVARCHAR(400),
    @Comment      NVARCHAR(1000) = NULL,
    @CreatedBy    NVARCHAR(100)  = NULL,
    @BookingId    BIGINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @Seats IS NULL OR @Seats <= 0
        THROW 50005, 'Seats must be a positive number', 1;

    BEGIN TRANSACTION;

    IF NOT EXISTS (SELECT 1 FROM dbo.Customers WHERE Id = @CustomerId AND IsDeleted = 0)
        THROW 50001, 'Customer not found', 1;

    DECLARE @Price DECIMAL(18,2), @Available INT, @Date DATE, @RouteName NVARCHAR(200);

    SELECT @Price = d.Price, @Available = d.SeatsAvailable,
           @Date = d.DepartureDate, @RouteName = r.Name
    FROM dbo.Departures AS d WITH (UPDLOCK, ROWLOCK)
    JOIN dbo.RouteRefs AS r ON r.Id = d.RouteId
    WHERE d.Id = @DepartureId AND d.IsDeleted = 0;

    IF @Price IS NULL
        THROW 50001, 'Departure not found', 1;
    IF @Date < CAST(SYSUTCDATETIME() AS DATE)
        THROW 50006, 'Departure date is in the past', 1;
    IF @Available < @Seats
        THROW 50003, 'Not enough free seats', 1;

    UPDATE dbo.Departures
    SET SeatsAvailable = SeatsAvailable - @Seats,
        UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @CreatedBy
    WHERE Id = @DepartureId;

    INSERT dbo.Bookings (CustomerId, Status, TotalAmount, CreatedBy)
    VALUES (@CustomerId, 'Submitted', @Price * @Seats, @CreatedBy);
    SET @BookingId = SCOPE_IDENTITY();

    INSERT dbo.BookingDetails (BookingId, ContactPhone, MeetingPoint, Comment)
    VALUES (@BookingId, @ContactPhone, @MeetingPoint, @Comment);

    INSERT dbo.BookingItems (BookingId, DepartureId, RouteName, UnitPrice, Seats)
    VALUES (@BookingId, @DepartureId, @RouteName, @Price, @Seats);

    INSERT dbo.BookingStatusHistory (BookingId, OldStatus, NewStatus, ChangedBy)
    VALUES (@BookingId, NULL, 'Submitted', @CreatedBy);

    COMMIT TRANSACTION;

    SELECT @BookingId AS BookingId;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_GetBookingById
    @BookingId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Bookings WHERE Id = @BookingId AND IsDeleted = 0)
        THROW 50001, 'Booking not found', 1;

    SELECT b.Id, b.Status, b.BookingDate, b.TotalAmount,
           c.Id AS CustomerId, c.FullName, c.Email,
           bd.ContactPhone, bd.MeetingPoint, bd.Comment
    FROM dbo.Bookings b
    JOIN dbo.Customers c ON c.Id = b.CustomerId
    LEFT JOIN dbo.BookingDetails bd ON bd.BookingId = b.Id
    WHERE b.Id = @BookingId;

    SELECT i.DepartureId, d.DepartureDate, i.RouteName, i.UnitPrice, i.Seats
    FROM dbo.BookingItems i
    JOIN dbo.Departures d ON d.Id = i.DepartureId
    WHERE i.BookingId = @BookingId;

    SELECT Id, Amount, Method, Status, PaidAt
    FROM dbo.Payments
    WHERE BookingId = @BookingId
    ORDER BY Id;

    SELECT OldStatus, NewStatus, ChangedAt, ChangedBy
    FROM dbo.BookingStatusHistory
    WHERE BookingId = @BookingId
    ORDER BY ChangedAt, Id;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_GetCustomerBookings
    @CustomerId BIGINT,
    @Status     NVARCHAR(30) = NULL,
    @PageNumber INT = 1,
    @PageSize   INT = 10
AS
BEGIN
    SET NOCOUNT ON;

    IF @PageNumber IS NULL OR @PageNumber < 1
        THROW 50005, 'PageNumber must be >= 1', 1;
    IF @PageSize IS NULL OR @PageSize < 1 OR @PageSize > 100
        THROW 50005, 'PageSize must be between 1 and 100', 1;

    SELECT b.Id, b.Status, b.BookingDate, b.TotalAmount,
           x.Routes, x.TotalSeats,
           COUNT(*) OVER () AS TotalCount
    FROM dbo.Bookings b
    OUTER APPLY (
        SELECT STRING_AGG(i.RouteName, N', ') AS Routes, SUM(i.Seats) AS TotalSeats
        FROM dbo.BookingItems i
        WHERE i.BookingId = b.Id
    ) x
    WHERE b.CustomerId = @CustomerId
      AND b.IsDeleted = 0
      AND (@Status IS NULL OR b.Status = @Status)
    ORDER BY b.BookingDate DESC, b.Id DESC
    OFFSET (@PageNumber - 1) * @PageSize ROWS
    FETCH NEXT @PageSize ROWS ONLY;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_ChangeBookingStatus
    @BookingId BIGINT,
    @NewStatus NVARCHAR(30),
    @ChangedBy NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;                

    DECLARE @Old NVARCHAR(30), @Msg NVARCHAR(200);

    IF @NewStatus IN ('Paid', 'Cancelled')
        THROW 50007, 'Use usp_PayBooking or usp_CancelBooking for this status', 1;

    BEGIN TRANSACTION;

    SELECT @Old = Status
    FROM dbo.Bookings WITH (UPDLOCK)
    WHERE Id = @BookingId AND IsDeleted = 0;

    IF @Old IS NULL
        THROW 50001, 'Booking not found', 1;

    IF dbo.fn_IsValidStatusTransition(@Old, @NewStatus) = 0
    BEGIN
        SET @Msg = CONCAT(N'Transition ', @Old, N' -> ', @NewStatus, N' is not allowed');
        THROW 50002, @Msg, 1;
    END

    UPDATE dbo.Bookings
    SET Status = @NewStatus, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @ChangedBy
    WHERE Id = @BookingId;

    INSERT dbo.BookingStatusHistory (BookingId, OldStatus, NewStatus, ChangedBy)
    VALUES (@BookingId, @Old, @NewStatus, @ChangedBy);

    COMMIT TRANSACTION;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_PayBooking
    @BookingId BIGINT,
    @Method    NVARCHAR(20),
    @PaidBy    NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Old NVARCHAR(30), @Total DECIMAL(18,2), @PaymentId BIGINT, @Msg NVARCHAR(200);

    IF @Method IS NULL OR @Method NOT IN ('Card', 'Cash', 'Transfer')
        THROW 50005, 'Unknown payment method', 1;

    BEGIN TRANSACTION;

    SELECT @Old = Status, @Total = TotalAmount
    FROM dbo.Bookings WITH (UPDLOCK)
    WHERE Id = @BookingId AND IsDeleted = 0;

    IF @Old IS NULL
        THROW 50001, 'Booking not found', 1;

    IF dbo.fn_IsValidStatusTransition(@Old, 'Paid') = 0
    BEGIN
        SET @Msg = CONCAT(N'Transition ', @Old, N' -> Paid is not allowed');
        THROW 50002, @Msg, 1;
    END

    INSERT dbo.Payments (BookingId, Amount, Method, Status, PaidAt, CreatedBy)
    VALUES (@BookingId, @Total, @Method, 'Succeeded', SYSUTCDATETIME(), @PaidBy);
    SET @PaymentId = SCOPE_IDENTITY();

    UPDATE dbo.Bookings
    SET Status = 'Paid', UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @PaidBy
    WHERE Id = @BookingId;

    INSERT dbo.BookingStatusHistory (BookingId, OldStatus, NewStatus, ChangedBy)
    VALUES (@BookingId, @Old, 'Paid', @PaidBy);

    COMMIT TRANSACTION;

    SELECT @PaymentId AS PaymentId;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_CancelBooking
    @BookingId BIGINT,
    @ChangedBy NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Old NVARCHAR(30), @Msg NVARCHAR(200);

    BEGIN TRANSACTION;

    SELECT @Old = Status
    FROM dbo.Bookings WITH (UPDLOCK)
    WHERE Id = @BookingId AND IsDeleted = 0;

    IF @Old IS NULL
        THROW 50001, 'Booking not found', 1;

    IF dbo.fn_IsValidStatusTransition(@Old, 'Cancelled') = 0
    BEGIN
        SET @Msg = CONCAT(N'Transition ', @Old, N' -> Cancelled is not allowed');
        THROW 50002, @Msg, 1;
    END

    UPDATE d
    SET d.SeatsAvailable = d.SeatsAvailable + i.Seats,
        d.UpdatedAt = SYSUTCDATETIME(), d.UpdatedBy = @ChangedBy
    FROM dbo.Departures AS d
    JOIN dbo.BookingItems AS i ON i.DepartureId = d.Id
    WHERE i.BookingId = @BookingId;

    UPDATE dbo.Bookings
    SET Status = 'Cancelled', UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @ChangedBy
    WHERE Id = @BookingId;

    INSERT dbo.BookingStatusHistory (BookingId, OldStatus, NewStatus, ChangedBy)
    VALUES (@BookingId, @Old, 'Cancelled', @ChangedBy);

    COMMIT TRANSACTION;
END
GO
