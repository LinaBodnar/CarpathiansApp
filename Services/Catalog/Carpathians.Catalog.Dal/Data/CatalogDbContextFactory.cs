using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Carpathians.Dal.Data;

/// <summary>
/// Фабрика для dotnet ef (migrations add): контекст без запуску Catalog.Api і без AppHost.
/// Рядок підключення потрібен лише для генерації міграцій; застосовує їх сервіс під час старту.
/// </summary>
public class CatalogDbContextFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseSqlServer("Server=localhost,14333;Database=CatalogDb;Integrated Security=false;TrustServerCertificate=true")
            .Options;
        return new CatalogDbContext(options);
    }
}
