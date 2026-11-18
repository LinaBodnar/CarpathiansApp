using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Carpathians.Catalog.Dal.Migrations
{
    /// <inheritdoc />
    public partial class AddRoutePublication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPublished",
                table: "Routes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAt",
                table: "Routes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublishedBy",
                table: "Routes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPublished",
                table: "Routes");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "Routes");

            migrationBuilder.DropColumn(
                name: "PublishedBy",
                table: "Routes");
        }
    }
}
