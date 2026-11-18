var builder = DistributedApplication.CreateBuilder(args);

var sqlPassword = builder.AddParameter("sql-password", secret: true);

var sql = builder.AddSqlServer("sql-catalog", sqlPassword, port: 1434)
    .WithDataVolume();

var catalogDb = sql.AddDatabase("CatalogDb", "CatalogContext");

builder.AddProject<Projects.Carpathians_Catalog_Api>("catalog-api")
    .WithReference(catalogDb)
    .WaitFor(catalogDb);

builder.Build().Run();
