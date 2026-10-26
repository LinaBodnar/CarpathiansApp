using Carpathians.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Carpathians.Dal.Configurations;

public class RouteDetailConfiguration : IEntityTypeConfiguration<RouteDetail>
{
    public void Configure(EntityTypeBuilder<RouteDetail> builder)
    {
        builder.ToTable("RouteDetails", t =>
            t.HasCheckConstraint("CK_RouteDetails_MaxAltitudeM", "[MaxAltitudeM] IS NULL OR [MaxAltitudeM] > 0"));

        builder.HasKey(d => d.RouteId);

        builder.Property(d => d.RouteId)
            .ValueGeneratedNever();

        builder.Property(d => d.Description)
            .IsRequired();

        builder.Property(d => d.Season)
            .HasMaxLength(100);

        builder.Property(d => d.StartPoint)
            .HasMaxLength(200);

        builder.Property(d => d.Requirements)
            .HasMaxLength(1000);
    }
}
