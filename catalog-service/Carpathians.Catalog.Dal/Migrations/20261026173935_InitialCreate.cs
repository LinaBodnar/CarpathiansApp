using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Carpathians.Catalog.Dal.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Points",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Latitude = table.Column<decimal>(type: "decimal(9,6)", nullable: false),
                    Longitude = table.Column<decimal>(type: "decimal(9,6)", nullable: false),
                    AltitudeM = table.Column<int>(type: "int", nullable: true),
                    PointType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Points", x => x.Id);
                    table.CheckConstraint("CK_Points_AltitudeM", "[AltitudeM] IS NULL OR [AltitudeM] >= 0");
                    table.CheckConstraint("CK_Points_Latitude", "[Latitude] BETWEEN -90 AND 90");
                    table.CheckConstraint("CK_Points_Longitude", "[Longitude] BETWEEN -180 AND 180");
                    table.CheckConstraint("CK_Points_PointType", "[PointType] IN ('Trailhead','Peak','Lake','Shelter','Viewpoint')");
                });

            migrationBuilder.CreateTable(
                name: "Regions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Regions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GalleryImages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PointId = table.Column<long>(type: "bigint", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Caption = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GalleryImages", x => x.Id);
                    table.CheckConstraint("CK_GalleryImages_SortOrder", "[SortOrder] >= 0");
                    table.ForeignKey(
                        name: "FK_GalleryImages_Points_PointId",
                        column: x => x.PointId,
                        principalTable: "Points",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Routes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RegionId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Difficulty = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DistanceKm = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    DurationHours = table.Column<decimal>(type: "decimal(5,1)", nullable: false),
                    ElevationGainM = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Routes", x => x.Id);
                    table.CheckConstraint("CK_Routes_Difficulty", "[Difficulty] IN ('Easy','Moderate','Hard')");
                    table.CheckConstraint("CK_Routes_DistanceKm", "[DistanceKm] > 0");
                    table.CheckConstraint("CK_Routes_DurationHours", "[DurationHours] > 0");
                    table.CheckConstraint("CK_Routes_ElevationGainM", "[ElevationGainM] >= 0");
                    table.ForeignKey(
                        name: "FK_Routes_Regions_RegionId",
                        column: x => x.RegionId,
                        principalTable: "Regions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GearItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RouteId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GearItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GearItems_Routes_RouteId",
                        column: x => x.RouteId,
                        principalTable: "Routes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RouteDetails",
                columns: table => new
                {
                    RouteId = table.Column<long>(type: "bigint", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Season = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MaxAltitudeM = table.Column<int>(type: "int", nullable: true),
                    StartPoint = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Requirements = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RouteDetails", x => x.RouteId);
                    table.CheckConstraint("CK_RouteDetails_MaxAltitudeM", "[MaxAltitudeM] IS NULL OR [MaxAltitudeM] > 0");
                    table.ForeignKey(
                        name: "FK_RouteDetails_Routes_RouteId",
                        column: x => x.RouteId,
                        principalTable: "Routes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RoutePoints",
                columns: table => new
                {
                    RouteId = table.Column<long>(type: "bigint", nullable: false),
                    PointId = table.Column<long>(type: "bigint", nullable: false),
                    SequenceNo = table.Column<int>(type: "int", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoutePoints", x => new { x.RouteId, x.PointId });
                    table.CheckConstraint("CK_RoutePoints_SequenceNo", "[SequenceNo] > 0");
                    table.ForeignKey(
                        name: "FK_RoutePoints_Points_PointId",
                        column: x => x.PointId,
                        principalTable: "Points",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoutePoints_Routes_RouteId",
                        column: x => x.RouteId,
                        principalTable: "Routes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Regions",
                columns: new[] { "Id", "Description", "Name" },
                values: new object[,]
                {
                    { 1, "Найвищий хребет Українських Карпат, де розташована гора Говерла.", "Чорногора" },
                    { 2, "Хребти з кам'яними осипищами та реліктовими лісами.", "Горгани" },
                    { 3, "Полонинський хребет із відкритими гірськими краєвидами.", "Свидовець" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_GalleryImages_Point_Sort",
                table: "GalleryImages",
                columns: new[] { "PointId", "SortOrder" })
                .Annotation("SqlServer:Include", new[] { "Url", "Caption" });

            migrationBuilder.CreateIndex(
                name: "UQ_GalleryImages_Point_FileName",
                table: "GalleryImages",
                columns: new[] { "PointId", "FileName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_GearItems_Route_Name",
                table: "GearItems",
                columns: new[] { "RouteId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Points_Coordinates",
                table: "Points",
                columns: new[] { "Latitude", "Longitude" },
                filter: "[IsDeleted] = 0")
                .Annotation("SqlServer:Include", new[] { "Name", "PointType" });

            migrationBuilder.CreateIndex(
                name: "UQ_Points_Name",
                table: "Points",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Regions_Name",
                table: "Regions",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoutePoints_PointId",
                table: "RoutePoints",
                column: "PointId")
                .Annotation("SqlServer:Include", new[] { "RouteId", "SequenceNo" });

            migrationBuilder.CreateIndex(
                name: "UQ_RoutePoints_Route_Sequence",
                table: "RoutePoints",
                columns: new[] { "RouteId", "SequenceNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Routes_Region_Difficulty",
                table: "Routes",
                columns: new[] { "RegionId", "Difficulty" },
                filter: "[IsDeleted] = 0")
                .Annotation("SqlServer:Include", new[] { "Name", "DistanceKm", "DurationHours" });

            migrationBuilder.CreateIndex(
                name: "UQ_Routes_Slug",
                table: "Routes",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GalleryImages");

            migrationBuilder.DropTable(
                name: "GearItems");

            migrationBuilder.DropTable(
                name: "RouteDetails");

            migrationBuilder.DropTable(
                name: "RoutePoints");

            migrationBuilder.DropTable(
                name: "Points");

            migrationBuilder.DropTable(
                name: "Routes");

            migrationBuilder.DropTable(
                name: "Regions");
        }
    }
}
