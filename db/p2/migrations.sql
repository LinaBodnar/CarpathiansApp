IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [Points] (
    [Id] bigint NOT NULL IDENTITY,
    [Name] nvarchar(200) NOT NULL,
    [Latitude] decimal(9,6) NOT NULL,
    [Longitude] decimal(9,6) NOT NULL,
    [AltitudeM] int NULL,
    [PointType] nvarchar(30) NOT NULL,
    [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    [UpdatedAt] datetime2 NULL,
    [IsDeleted] bit NOT NULL,
    CONSTRAINT [PK_Points] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_Points_AltitudeM] CHECK ([AltitudeM] IS NULL OR [AltitudeM] >= 0),
    CONSTRAINT [CK_Points_Latitude] CHECK ([Latitude] BETWEEN -90 AND 90),
    CONSTRAINT [CK_Points_Longitude] CHECK ([Longitude] BETWEEN -180 AND 180),
    CONSTRAINT [CK_Points_PointType] CHECK ([PointType] IN ('Trailhead','Peak','Lake','Shelter','Viewpoint'))
);
GO

CREATE TABLE [Regions] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(100) NOT NULL,
    [Description] nvarchar(500) NULL,
    CONSTRAINT [PK_Regions] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [GalleryImages] (
    [Id] bigint NOT NULL IDENTITY,
    [PointId] bigint NOT NULL,
    [FileName] nvarchar(260) NOT NULL,
    [Url] nvarchar(500) NOT NULL,
    [Caption] nvarchar(300) NULL,
    [SortOrder] int NOT NULL,
    [UploadedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_GalleryImages] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_GalleryImages_SortOrder] CHECK ([SortOrder] >= 0),
    CONSTRAINT [FK_GalleryImages_Points_PointId] FOREIGN KEY ([PointId]) REFERENCES [Points] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [Routes] (
    [Id] bigint NOT NULL IDENTITY,
    [RegionId] int NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Slug] nvarchar(200) NOT NULL,
    [Difficulty] nvarchar(20) NOT NULL,
    [DistanceKm] decimal(6,2) NOT NULL,
    [DurationHours] decimal(5,1) NOT NULL,
    [ElevationGainM] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    [CreatedBy] nvarchar(100) NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] nvarchar(100) NULL,
    [IsDeleted] bit NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_Routes] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_Routes_Difficulty] CHECK ([Difficulty] IN ('Easy','Moderate','Hard')),
    CONSTRAINT [CK_Routes_DistanceKm] CHECK ([DistanceKm] > 0),
    CONSTRAINT [CK_Routes_DurationHours] CHECK ([DurationHours] > 0),
    CONSTRAINT [CK_Routes_ElevationGainM] CHECK ([ElevationGainM] >= 0),
    CONSTRAINT [FK_Routes_Regions_RegionId] FOREIGN KEY ([RegionId]) REFERENCES [Regions] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [GearItems] (
    [Id] bigint NOT NULL IDENTITY,
    [RouteId] bigint NOT NULL,
    [Name] nvarchar(150) NOT NULL,
    [IsRequired] bit NOT NULL,
    [Notes] nvarchar(300) NULL,
    CONSTRAINT [PK_GearItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_GearItems_Routes_RouteId] FOREIGN KEY ([RouteId]) REFERENCES [Routes] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [RouteDetails] (
    [RouteId] bigint NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [Season] nvarchar(100) NULL,
    [MaxAltitudeM] int NULL,
    [StartPoint] nvarchar(200) NULL,
    [Requirements] nvarchar(1000) NULL,
    CONSTRAINT [PK_RouteDetails] PRIMARY KEY ([RouteId]),
    CONSTRAINT [CK_RouteDetails_MaxAltitudeM] CHECK ([MaxAltitudeM] IS NULL OR [MaxAltitudeM] > 0),
    CONSTRAINT [FK_RouteDetails_Routes_RouteId] FOREIGN KEY ([RouteId]) REFERENCES [Routes] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [RoutePoints] (
    [RouteId] bigint NOT NULL,
    [PointId] bigint NOT NULL,
    [SequenceNo] int NOT NULL,
    [AddedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_RoutePoints] PRIMARY KEY ([RouteId], [PointId]),
    CONSTRAINT [CK_RoutePoints_SequenceNo] CHECK ([SequenceNo] > 0),
    CONSTRAINT [FK_RoutePoints_Points_PointId] FOREIGN KEY ([PointId]) REFERENCES [Points] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_RoutePoints_Routes_RouteId] FOREIGN KEY ([RouteId]) REFERENCES [Routes] ([Id]) ON DELETE CASCADE
);
GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Description', N'Name') AND [object_id] = OBJECT_ID(N'[Regions]'))
    SET IDENTITY_INSERT [Regions] ON;
INSERT INTO [Regions] ([Id], [Description], [Name])
VALUES (1, N'Найвищий хребет Українських Карпат, де розташована гора Говерла.', N'Чорногора'),
(2, N'Хребти з кам''яними осипищами та реліктовими лісами.', N'Горгани'),
(3, N'Полонинський хребет із відкритими гірськими краєвидами.', N'Свидовець');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Description', N'Name') AND [object_id] = OBJECT_ID(N'[Regions]'))
    SET IDENTITY_INSERT [Regions] OFF;
GO

CREATE INDEX [IX_GalleryImages_Point_Sort] ON [GalleryImages] ([PointId], [SortOrder]) INCLUDE ([Url], [Caption]);
GO

CREATE UNIQUE INDEX [UQ_GalleryImages_Point_FileName] ON [GalleryImages] ([PointId], [FileName]);
GO

CREATE UNIQUE INDEX [UQ_GearItems_Route_Name] ON [GearItems] ([RouteId], [Name]);
GO

CREATE INDEX [IX_Points_Coordinates] ON [Points] ([Latitude], [Longitude]) INCLUDE ([Name], [PointType]) WHERE [IsDeleted] = 0;
GO

CREATE UNIQUE INDEX [UQ_Points_Name] ON [Points] ([Name]);
GO

CREATE UNIQUE INDEX [UQ_Regions_Name] ON [Regions] ([Name]);
GO

CREATE INDEX [IX_RoutePoints_PointId] ON [RoutePoints] ([PointId]) INCLUDE ([RouteId], [SequenceNo]);
GO

CREATE UNIQUE INDEX [UQ_RoutePoints_Route_Sequence] ON [RoutePoints] ([RouteId], [SequenceNo]);
GO

CREATE INDEX [IX_Routes_Region_Difficulty] ON [Routes] ([RegionId], [Difficulty]) INCLUDE ([Name], [DistanceKm], [DurationHours]) WHERE [IsDeleted] = 0;
GO

CREATE UNIQUE INDEX [UQ_Routes_Slug] ON [Routes] ([Slug]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261026173935_InitialCreate', N'8.0.33');
GO

COMMIT;
GO

