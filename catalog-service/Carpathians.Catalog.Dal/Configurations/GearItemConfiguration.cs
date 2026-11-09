using Carpathians.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Carpathians.Dal.Configurations;

public class GearItemConfiguration : IEntityTypeConfiguration<GearItem>
{
    public void Configure(EntityTypeBuilder<GearItem> builder)
    {
        builder.ToTable("GearItems");

        builder.HasKey(g => g.Id);

        builder.Property(g => g.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(g => g.Notes)
            .HasMaxLength(300);

        builder.HasIndex(g => new { g.RouteId, g.Name })
            .IsUnique()
            .HasDatabaseName("UQ_GearItems_Route_Name");
    }
}
