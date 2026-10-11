IF DB_ID(N'BookingContext') IS NULL
    CREATE DATABASE BookingContext;
GO

USE BookingContext;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF OBJECT_ID(N'dbo.Customers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Customers (
        Id          BIGINT IDENTITY(1,1) NOT NULL,
        Email       NVARCHAR(256) NOT NULL,
        FullName    NVARCHAR(200) NOT NULL,
        Phone       NVARCHAR(30)  NULL,
        CreatedAt   DATETIME2     NOT NULL CONSTRAINT DF_Customers_CreatedAt DEFAULT SYSUTCDATETIME(),
        CreatedBy   NVARCHAR(100) NULL,
        UpdatedAt   DATETIME2     NULL,
        UpdatedBy   NVARCHAR(100) NULL,
        IsDeleted   BIT           NOT NULL CONSTRAINT DF_Customers_IsDeleted DEFAULT 0,
        RowVersion  ROWVERSION,
        CONSTRAINT PK_Customers PRIMARY KEY (Id),
        CONSTRAINT UQ_Customers_Email UNIQUE (Email),
        CONSTRAINT CK_Customers_Email CHECK (Email LIKE N'%_@_%._%')
    );
END
GO

IF OBJECT_ID(N'dbo.RouteRefs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RouteRefs (
        Id        BIGINT        NOT NULL,   
        Name      NVARCHAR(200) NOT NULL,
        UpdatedAt DATETIME2     NOT NULL CONSTRAINT DF_RouteRefs_UpdatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_RouteRefs PRIMARY KEY (Id)
    );
END
GO

IF OBJECT_ID(N'dbo.Departures', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Departures (
        Id             BIGINT IDENTITY(1,1) NOT NULL,
        RouteId        BIGINT        NOT NULL,
        DepartureDate  DATE          NOT NULL,
        Price          DECIMAL(18,2) NOT NULL,
        SeatsTotal     INT           NOT NULL,
        SeatsAvailable INT           NOT NULL,
        CreatedAt      DATETIME2     NOT NULL CONSTRAINT DF_Departures_CreatedAt DEFAULT SYSUTCDATETIME(),
        CreatedBy      NVARCHAR(100) NULL,
        UpdatedAt      DATETIME2     NULL,
        UpdatedBy      NVARCHAR(100) NULL,
        IsDeleted      BIT           NOT NULL CONSTRAINT DF_Departures_IsDeleted DEFAULT 0,
        RowVersion     ROWVERSION,
        CONSTRAINT PK_Departures PRIMARY KEY (Id),
        CONSTRAINT FK_Departures_RouteRefs FOREIGN KEY (RouteId) REFERENCES dbo.RouteRefs(Id),
        CONSTRAINT UQ_Departures_Route_Date UNIQUE (RouteId, DepartureDate),
        CONSTRAINT CK_Departures_Price CHECK (Price > 0),
        CONSTRAINT CK_Departures_SeatsTotal CHECK (SeatsTotal > 0),
        CONSTRAINT CK_Departures_SeatsAvailable CHECK (SeatsAvailable >= 0 AND SeatsAvailable <= SeatsTotal)
    );

    CREATE INDEX IX_Departures_Open_ByDate
        ON dbo.Departures (DepartureDate)
        INCLUDE (RouteId, Price, SeatsAvailable)
        WHERE SeatsAvailable > 0 AND IsDeleted = 0;
END
GO

IF OBJECT_ID(N'dbo.Bookings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Bookings (
        Id          BIGINT IDENTITY(1,1) NOT NULL,
        CustomerId  BIGINT        NOT NULL,
        Status      NVARCHAR(30)  NOT NULL CONSTRAINT DF_Bookings_Status DEFAULT 'Submitted',
        BookingDate DATETIME2     NOT NULL CONSTRAINT DF_Bookings_BookingDate DEFAULT SYSUTCDATETIME(),
        TotalAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_Bookings_TotalAmount DEFAULT 0,
        CreatedAt   DATETIME2     NOT NULL CONSTRAINT DF_Bookings_CreatedAt DEFAULT SYSUTCDATETIME(),
        CreatedBy   NVARCHAR(100) NULL,
        UpdatedAt   DATETIME2     NULL,
        UpdatedBy   NVARCHAR(100) NULL,
        IsDeleted   BIT           NOT NULL CONSTRAINT DF_Bookings_IsDeleted DEFAULT 0,
        RowVersion  ROWVERSION,   -- оптимістична конкурентність
        CONSTRAINT PK_Bookings PRIMARY KEY (Id),
        CONSTRAINT FK_Bookings_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(Id),
        CONSTRAINT CK_Bookings_Status CHECK (Status IN
            ('Submitted', 'AwaitingValidation', 'SeatsConfirmed', 'Paid', 'Completed', 'Cancelled')),
        CONSTRAINT CK_Bookings_TotalAmount CHECK (TotalAmount >= 0)
    );

    CREATE INDEX IX_Bookings_Customer_Status
        ON dbo.Bookings (CustomerId, Status)
        INCLUDE (BookingDate, TotalAmount)
        WHERE IsDeleted = 0;
END
GO

IF OBJECT_ID(N'dbo.BookingDetails', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BookingDetails (
        BookingId    BIGINT         NOT NULL,
        ContactPhone NVARCHAR(30)   NOT NULL,
        MeetingPoint NVARCHAR(400)  NOT NULL,
        Comment      NVARCHAR(1000) NULL,
        CONSTRAINT PK_BookingDetails PRIMARY KEY (BookingId),
        CONSTRAINT FK_BookingDetails_Bookings FOREIGN KEY (BookingId)
            REFERENCES dbo.Bookings(Id) ON DELETE CASCADE
    );
END
GO

IF OBJECT_ID(N'dbo.BookingItems', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BookingItems (
        BookingId   BIGINT        NOT NULL,
        DepartureId BIGINT        NOT NULL,
        RouteName   NVARCHAR(200) NOT NULL,   
        UnitPrice   DECIMAL(18,2) NOT NULL,   
        Seats       INT           NOT NULL,
        CONSTRAINT PK_BookingItems PRIMARY KEY (BookingId, DepartureId),
        CONSTRAINT FK_BookingItems_Bookings FOREIGN KEY (BookingId)
            REFERENCES dbo.Bookings(Id) ON DELETE CASCADE,
        CONSTRAINT FK_BookingItems_Departures FOREIGN KEY (DepartureId)
            REFERENCES dbo.Departures(Id),
        CONSTRAINT CK_BookingItems_UnitPrice CHECK (UnitPrice > 0),
        CONSTRAINT CK_BookingItems_Seats CHECK (Seats > 0)
    );

    CREATE INDEX IX_BookingItems_DepartureId
        ON dbo.BookingItems (DepartureId)
        INCLUDE (Seats);
END
GO

IF OBJECT_ID(N'dbo.Payments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Payments (
        Id        BIGINT IDENTITY(1,1) NOT NULL,
        BookingId BIGINT        NOT NULL,
        Amount    DECIMAL(18,2) NOT NULL,
        Method    NVARCHAR(20)  NOT NULL,
        Status    NVARCHAR(20)  NOT NULL CONSTRAINT DF_Payments_Status DEFAULT 'Pending',
        PaidAt    DATETIME2     NULL,
        CreatedAt DATETIME2     NOT NULL CONSTRAINT DF_Payments_CreatedAt DEFAULT SYSUTCDATETIME(),
        CreatedBy NVARCHAR(100) NULL,
        CONSTRAINT PK_Payments PRIMARY KEY (Id),
        CONSTRAINT FK_Payments_Bookings FOREIGN KEY (BookingId) REFERENCES dbo.Bookings(Id),
        CONSTRAINT CK_Payments_Amount CHECK (Amount > 0),
        CONSTRAINT CK_Payments_Method CHECK (Method IN ('Card', 'Cash', 'Transfer')),
        CONSTRAINT CK_Payments_Status CHECK (Status IN ('Pending', 'Succeeded', 'Failed', 'Refunded'))
    );

    CREATE INDEX IX_Payments_BookingId
        ON dbo.Payments (BookingId)
        INCLUDE (Amount, Status);
END
GO

IF OBJECT_ID(N'dbo.BookingStatusHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BookingStatusHistory (
        Id         BIGINT IDENTITY(1,1) NOT NULL,
        BookingId  BIGINT        NOT NULL,
        OldStatus  NVARCHAR(30)  NULL,        
        NewStatus  NVARCHAR(30)  NOT NULL,
        ChangedAt  DATETIME2     NOT NULL CONSTRAINT DF_BSH_ChangedAt DEFAULT SYSUTCDATETIME(),
        ChangedBy  NVARCHAR(100) NULL,
        CONSTRAINT PK_BookingStatusHistory PRIMARY KEY (Id),
        CONSTRAINT FK_BSH_Bookings FOREIGN KEY (BookingId) REFERENCES dbo.Bookings(Id),
        CONSTRAINT CK_BSH_NewStatus CHECK (NewStatus IN
            ('Submitted', 'AwaitingValidation', 'SeatsConfirmed', 'Paid', 'Completed', 'Cancelled'))
    );

    CREATE INDEX IX_BSH_Booking_ChangedAt
        ON dbo.BookingStatusHistory (BookingId, ChangedAt);
END
GO

GO
