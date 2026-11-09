using Carpathians.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Carpathians.Dal.Configurations;

public class RegionConfiguration : IEntityTypeConfiguration<Region>
{
    public void Configure(EntityTypeBuilder<Region> builder)
    {
        builder.ToTable("Regions");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(r => r.Description)
            .HasMaxLength(500);

        builder.HasIndex(r => r.Name)
            .IsUnique()
            .HasDatabaseName("UQ_Regions_Name");

        builder.HasData(
            new { Id = 1, Name = "Чорногора", Description = "Найвищий хребет Українських Карпат, де розташована гора Говерла." },
            new { Id = 2, Name = "Горгани", Description = "Хребти з кам'яними осипищами та реліктовими лісами." },
            new { Id = 3, Name = "Свидовець", Description = "Полонинський хребет із відкритими гірськими краєвидами." }
        );
    }
}
