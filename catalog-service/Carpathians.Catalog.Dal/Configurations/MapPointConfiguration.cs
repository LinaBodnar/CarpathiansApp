using Carpathians.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Carpathians.Dal.Configurations;

public class MapPointConfiguration : IEntityTypeConfiguration<MapPoint>
{
    public void Configure(EntityTypeBuilder<MapPoint> builder)
    {
        builder.ToTable("Points", t =>
        {
            t.HasCheckConstraint("CK_Points_Latitude", "[Latitude] BETWEEN -90 AND 90");
            t.HasCheckConstraint("CK_Points_Longitude", "[Longitude] BETWEEN -180 AND 180");
            t.HasCheckConstraint("CK_Points_AltitudeM", "[AltitudeM] IS NULL OR [AltitudeM] >= 0");
            t.HasCheckConstraint("CK_Points_PointType",
                "[PointType] IN ('Trailhead','Peak','Lake','Shelter','Viewpoint')");
        });

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Latitude)
            .HasColumnType("decimal(9,6)");

        builder.Property(p => p.Longitude)
            .HasColumnType("decimal(9,6)");

        builder.Property(p => p.PointType)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(p => p.CreatedAt)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(p => p.Name)
            .IsUnique()
            .HasDatabaseName("UQ_Points_Name");

        builder.HasIndex(p => new { p.Latitude, p.Longitude })
            .HasDatabaseName("IX_Points_Coordinates")
            .IncludeProperties(p => new { p.Name, p.PointType })
            .HasFilter("[IsDeleted] = 0");

        builder.HasQueryFilter(p => !p.IsDeleted);

        builder.HasMany(p => p.GalleryImages)
            .WithOne()
            .HasForeignKey(i => i.PointId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
