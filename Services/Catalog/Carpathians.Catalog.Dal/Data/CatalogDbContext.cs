using Carpathians.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Carpathians.Dal.Data;

public class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<Region> Regions => Set<Region>();
    public DbSet<TrailRoute> Routes => Set<TrailRoute>();
    public DbSet<RouteDetail> RouteDetails => Set<RouteDetail>();
    public DbSet<MapPoint> Points => Set<MapPoint>();
    public DbSet<RoutePoint> RoutePoints => Set<RoutePoint>();
    public DbSet<GalleryImage> GalleryImages => Set<GalleryImage>();
    public DbSet<GearItem> GearItems => Set<GearItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);
    }
}
