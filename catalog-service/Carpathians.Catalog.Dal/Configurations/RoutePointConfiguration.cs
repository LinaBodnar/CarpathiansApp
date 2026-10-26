using Carpathians.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Carpathians.Dal.Configurations;

public class RoutePointConfiguration : IEntityTypeConfiguration<RoutePoint>
{
    public void Configure(EntityTypeBuilder<RoutePoint> builder)
    {
        builder.ToTable("RoutePoints", t =>
            t.HasCheckConstraint("CK_RoutePoints_SequenceNo", "[SequenceNo] > 0"));

        builder.HasKey(rp => new { rp.RouteId, rp.PointId });

        builder.Property(rp => rp.AddedAt)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne(rp => rp.Route)
            .WithMany(r => r.RoutePoints)
            .HasForeignKey(rp => rp.RouteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(rp => rp.Point)
            .WithMany(p => p.RoutePoints)
            .HasForeignKey(rp => rp.PointId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(rp => new { rp.RouteId, rp.SequenceNo })
            .IsUnique()
            .HasDatabaseName("UQ_RoutePoints_Route_Sequence");

        builder.HasIndex(rp => rp.PointId)
            .HasDatabaseName("IX_RoutePoints_PointId")
            .IncludeProperties(rp => new { rp.RouteId, rp.SequenceNo });
    }
}
