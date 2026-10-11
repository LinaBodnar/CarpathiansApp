IF DB_ID(N'CatalogContext') IS NULL
    CREATE DATABASE CatalogContext;
GO

USE CatalogContext;
GO
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF OBJECT_ID(N'dbo.Regions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Regions (
        Id          INT IDENTITY(1,1) NOT NULL,
        Name        NVARCHAR(100) NOT NULL,
        Description NVARCHAR(500) NULL,
        CONSTRAINT PK_Regions PRIMARY KEY (Id),
        CONSTRAINT UQ_Regions_Name UNIQUE (Name)
    );
END
GO

IF OBJECT_ID(N'dbo.Routes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Routes (
        Id             BIGINT IDENTITY(1,1) NOT NULL,
        RegionId       INT           NOT NULL,
        Name           NVARCHAR(200) NOT NULL,
        Slug           NVARCHAR(200) NOT NULL,
        Difficulty     NVARCHAR(20)  NOT NULL CONSTRAINT DF_Routes_Difficulty DEFAULT 'Moderate',
        DistanceKm     DECIMAL(6,2)  NOT NULL,
        DurationHours  DECIMAL(5,1)  NOT NULL,
        ElevationGainM INT           NOT NULL CONSTRAINT DF_Routes_Elevation DEFAULT 0,
        CreatedAt      DATETIME2     NOT NULL CONSTRAINT DF_Routes_CreatedAt DEFAULT SYSUTCDATETIME(),
        CreatedBy      NVARCHAR(100) NULL,
        UpdatedAt      DATETIME2     NULL,
        UpdatedBy      NVARCHAR(100) NULL,
        IsDeleted      BIT           NOT NULL CONSTRAINT DF_Routes_IsDeleted DEFAULT 0,
        RowVersion     ROWVERSION,   
        CONSTRAINT PK_Routes PRIMARY KEY (Id),
        CONSTRAINT FK_Routes_Regions FOREIGN KEY (RegionId) REFERENCES dbo.Regions(Id),
        CONSTRAINT UQ_Routes_Slug UNIQUE (Slug),   
        CONSTRAINT CK_Routes_Difficulty CHECK (Difficulty IN ('Easy', 'Moderate', 'Hard')),
        CONSTRAINT CK_Routes_DistanceKm CHECK (DistanceKm > 0),
        CONSTRAINT CK_Routes_DurationHours CHECK (DurationHours > 0),
        CONSTRAINT CK_Routes_ElevationGainM CHECK (ElevationGainM >= 0)
    );

    -- Запит: "маршрути регіону певної складності" (фільтр у каталозі).
    -- Фільтрований покриваючий індекс: лише не видалені маршрути, без key lookup.
    CREATE INDEX IX_Routes_Region_Difficulty
        ON dbo.Routes (RegionId, Difficulty)
        INCLUDE (Name, DistanceKm, DurationHours)
        WHERE IsDeleted = 0;
END
GO

IF OBJECT_ID(N'dbo.RouteDetails', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RouteDetails (
        RouteId      BIGINT         NOT NULL,
        Description  NVARCHAR(MAX)  NOT NULL,
        Season       NVARCHAR(100)  NULL,
        MaxAltitudeM INT            NULL,
        StartPoint   NVARCHAR(200)  NULL,
        Requirements NVARCHAR(1000) NULL,
        CONSTRAINT PK_RouteDetails PRIMARY KEY (RouteId),
        CONSTRAINT FK_RouteDetails_Routes FOREIGN KEY (RouteId)
            REFERENCES dbo.Routes(Id) ON DELETE CASCADE,
        CONSTRAINT CK_RouteDetails_MaxAltitudeM CHECK (MaxAltitudeM IS NULL OR MaxAltitudeM > 0)
    );
END
GO

IF OBJECT_ID(N'dbo.Points', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Points (
        Id        BIGINT IDENTITY(1,1) NOT NULL,
        Name      NVARCHAR(200) NOT NULL,
        Latitude  DECIMAL(9,6)  NOT NULL,
        Longitude DECIMAL(9,6)  NOT NULL,
        AltitudeM INT           NULL,
        PointType NVARCHAR(30)  NOT NULL CONSTRAINT DF_Points_PointType DEFAULT 'Viewpoint',
        CreatedAt DATETIME2     NOT NULL CONSTRAINT DF_Points_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2     NULL,
        IsDeleted BIT           NOT NULL CONSTRAINT DF_Points_IsDeleted DEFAULT 0,
        CONSTRAINT PK_Points PRIMARY KEY (Id),
        CONSTRAINT UQ_Points_Name UNIQUE (Name),
        CONSTRAINT CK_Points_Latitude  CHECK (Latitude  BETWEEN -90  AND 90),
        CONSTRAINT CK_Points_Longitude CHECK (Longitude BETWEEN -180 AND 180),
        CONSTRAINT CK_Points_AltitudeM CHECK (AltitudeM IS NULL OR AltitudeM >= 0),
        CONSTRAINT CK_Points_PointType CHECK (PointType IN ('Trailhead', 'Peak', 'Lake', 'Shelter', 'Viewpoint'))
    );

    -- Запит: "точки в прямокутнику на карті" (BETWEEN за широтою і довготою).
    CREATE INDEX IX_Points_Coordinates
        ON dbo.Points (Latitude, Longitude)
        INCLUDE (Name, PointType)
        WHERE IsDeleted = 0;
END
GO

IF OBJECT_ID(N'dbo.RoutePoints', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RoutePoints (
        RouteId    BIGINT    NOT NULL,
        PointId    BIGINT    NOT NULL,
        SequenceNo INT       NOT NULL,   
        AddedAt    DATETIME2 NOT NULL CONSTRAINT DF_RoutePoints_AddedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_RoutePoints PRIMARY KEY (RouteId, PointId),
        CONSTRAINT FK_RoutePoints_Routes FOREIGN KEY (RouteId)
            REFERENCES dbo.Routes(Id) ON DELETE CASCADE,
        CONSTRAINT FK_RoutePoints_Points FOREIGN KEY (PointId)
            REFERENCES dbo.Points(Id),   
        CONSTRAINT UQ_RoutePoints_Route_Sequence UNIQUE (RouteId, SequenceNo),
        CONSTRAINT CK_RoutePoints_SequenceNo CHECK (SequenceNo > 0)
    );

    -- Запит: "маршрути, що проходять через точку" (зворотний напрямок M:N;
    -- PK починається з RouteId, тому для PointId потрібен окремий індекс).
    CREATE INDEX IX_RoutePoints_PointId ON dbo.RoutePoints (PointId) INCLUDE (RouteId, SequenceNo);
END
GO

IF OBJECT_ID(N'dbo.GalleryImages', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.GalleryImages (
        Id         BIGINT IDENTITY(1,1) NOT NULL,
        PointId    BIGINT        NOT NULL,
        FileName   NVARCHAR(260) NOT NULL,
        Url        NVARCHAR(500) NOT NULL,
        Caption    NVARCHAR(300) NULL,
        SortOrder  INT           NOT NULL CONSTRAINT DF_GalleryImages_SortOrder DEFAULT 0,
        UploadedAt DATETIME2     NOT NULL CONSTRAINT DF_GalleryImages_UploadedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_GalleryImages PRIMARY KEY (Id),
        CONSTRAINT FK_GalleryImages_Points FOREIGN KEY (PointId)
            REFERENCES dbo.Points(Id) ON DELETE CASCADE,
        CONSTRAINT UQ_GalleryImages_Point_FileName UNIQUE (PointId, FileName),
        CONSTRAINT CK_GalleryImages_SortOrder CHECK (SortOrder >= 0)
    );

    -- Запит: "галерея точки в заданому порядку".
    CREATE INDEX IX_GalleryImages_Point_Sort ON dbo.GalleryImages (PointId, SortOrder) INCLUDE (Url, Caption);
END
GO

IF OBJECT_ID(N'dbo.GearItems', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.GearItems (
        Id         BIGINT IDENTITY(1,1) NOT NULL,
        RouteId    BIGINT        NOT NULL,
        Name       NVARCHAR(150) NOT NULL,
        IsRequired BIT           NOT NULL CONSTRAINT DF_GearItems_IsRequired DEFAULT 1,
        Notes      NVARCHAR(300) NULL,
        CONSTRAINT PK_GearItems PRIMARY KEY (Id),
        CONSTRAINT FK_GearItems_Routes FOREIGN KEY (RouteId)
            REFERENCES dbo.Routes(Id) ON DELETE CASCADE,
        CONSTRAINT UQ_GearItems_Route_Name UNIQUE (RouteId, Name)
    );
END
GO
  
GO
