// Orchestrator платформи Carpathians: лише конфігурація ресурсів, без бізнес-логіки.
// Порядок запуску: бази даних -> мікросервіси -> Aggregator -> Gateway (ланцюжки WaitFor).

var builder = DistributedApplication.CreateBuilder(args);

// ---------- Бази даних (Docker-контейнери, дані зберігаються у volumes) ----------

// Пароль SQL Server — параметр-секрет: user-secrets AppHost або введення в Dashboard.
var sqlPassword = builder.AddParameter("sql-password", secret: true);

// Фіксований порт 14333 — щоб застосувати SQL-скрипти Лаб. 01 через sqlcmd.
var sql = builder.AddSqlServer("sql", sqlPassword, port: 14333)
    .WithDataVolume("carpathians-sql-data");

// Імена ресурсів збігаються з іменами рядків підключення, які сервіси вже читають
// (ConnectionStrings:BookingDb, ConnectionStrings:CatalogDb, ConnectionStrings:ReviewsDb).
var bookingDb = sql.AddDatabase("BookingDb", "BookingContext");     // Лаб. 02 — ADO.NET + Dapper
var catalogDb = sql.AddDatabase("CatalogDb", "CatalogContext");     // Лаб. 03 — EF Core

var mongo = builder.AddMongoDB("mongo")
    .WithDataVolume("carpathians-mongo-data");
var reviewsDb = mongo.AddDatabase("ReviewsDb", "ReviewContext");    // Лаб. 04 — MongoDB

// ---------- Мікросервіси (ім'я ресурсу = логічне ім'я для service discovery) ----------

var booking = builder.AddProject<Projects.Carpathians_Booking_Api>("booking-api")
    .WithReference(bookingDb)
    .WaitFor(bookingDb)
    .WithHttpHealthCheck("/health");

var catalog = builder.AddProject<Projects.Carpathians_Catalog_Api>("catalog-api")
    .WithReference(catalogDb)
    .WaitFor(catalogDb)
    .WithHttpHealthCheck("/health");

var reviews = builder.AddProject<Projects.Carpathians_Reviews_WebApi>("reviews-api")
    .WithReference(reviewsDb)
    .WaitFor(reviewsDb)
    .WithHttpHealthCheck("/health");

// ---------- Aggregator: паралельно опитує мікросервіси ----------

var aggregator = builder.AddProject<Projects.Carpathians_Platform_Aggregator>("aggregator")
    .WithReference(booking)
    .WithReference(catalog)
    .WithReference(reviews)
    .WaitFor(booking)
    .WaitFor(catalog)
    .WaitFor(reviews)
    .WithHttpHealthCheck("/health");

// ---------- API Gateway: єдина точка входу, фіксований порт 5000 ----------

builder.AddProject<Projects.Carpathians_Platform_ApiGateway>("gateway")
    .WithHttpEndpoint(port: 5000, name: "public")
    .WithExternalHttpEndpoints()
    .WithReference(booking)
    .WithReference(catalog)
    .WithReference(reviews)
    .WithReference(aggregator)
    .WaitFor(aggregator);

builder.Build().Run();
