using Carpathians.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Carpathians.Dal.Configurations;

public class GalleryImageConfiguration : IEntityTypeConfiguration<GalleryImage>
{
    public void Configure(EntityTypeBuilder<GalleryImage> builder)
    {
        builder.ToTable("GalleryImages", t =>
            t.HasCheckConstraint("CK_GalleryImages_SortOrder", "[SortOrder] >= 0"));

        builder.HasKey(i => i.Id);

        builder.Property(i => i.FileName)
            .IsRequired()
            .HasMaxLength(260);

        builder.Property(i => i.Url)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(i => i.Caption)
            .HasMaxLength(300);

        builder.Property(i => i.UploadedAt)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(i => new { i.PointId, i.FileName })
            .IsUnique()
            .HasDatabaseName("UQ_GalleryImages_Point_FileName");

        builder.HasIndex(i => new { i.PointId, i.SortOrder })
            .HasDatabaseName("IX_GalleryImages_Point_Sort")
            .IncludeProperties(i => new { i.Url, i.Caption });
    }
}
