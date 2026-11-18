using Carpathians.Domain.Entities;
using Carpathians.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Carpathians.Dal.Configurations;

public class TrailRouteConfiguration : IEntityTypeConfiguration<TrailRoute>
{
    public void Configure(EntityTypeBuilder<TrailRoute> builder)
    {
        builder.ToTable("Routes", t =>
        {
            t.HasCheckConstraint("CK_Routes_Difficulty", "[Difficulty] IN ('Easy','Moderate','Hard')");
            t.HasCheckConstraint("CK_Routes_DistanceKm", "[DistanceKm] > 0");
            t.HasCheckConstraint("CK_Routes_DurationHours", "[DurationHours] > 0");
            t.HasCheckConstraint("CK_Routes_ElevationGainM", "[ElevationGainM] >= 0");
        });

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(r => r.Slug)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(r => r.Difficulty)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(r => r.DistanceKm)
            .HasColumnType("decimal(6,2)");

        builder.Property(r => r.DurationHours)
            .HasColumnType("decimal(5,1)");

        builder.Property(r => r.ElevationGainM)
            .IsRequired();

        builder.Property(r => r.CreatedAt)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Property(r => r.CreatedBy)
            .HasMaxLength(100);

        builder.Property(r => r.UpdatedBy)
            .HasMaxLength(100);

        builder.Property(r => r.RowVersion)
            .IsRowVersion();

        builder.HasIndex(r => r.Slug)
            .IsUnique()
            .HasDatabaseName("UQ_Routes_Slug");

        builder.HasIndex(r => new { r.RegionId, r.Difficulty })
            .HasDatabaseName("IX_Routes_Region_Difficulty")
            .IncludeProperties(r => new { r.Name, r.DistanceKm, r.DurationHours })
            .HasFilter("[IsDeleted] = 0");

        builder.HasQueryFilter(r => !r.IsDeleted);

        builder.HasOne(r => r.Region)
            .WithMany(reg => reg.Routes)
            .HasForeignKey(r => r.RegionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Detail)
            .WithOne()
            .HasForeignKey<RouteDetail>(d => d.RouteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(r => r.GearItems)
            .WithOne()
            .HasForeignKey(g => g.RouteId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
