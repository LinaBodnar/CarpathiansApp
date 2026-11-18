// Orchestrator платформи Carpathians: лише конфігурація ресурсів, без бізнес-логіки.
// Порядок запуску: бази даних і Redis -> мікросервіси -> Aggregator -> Gateway (ланцюжки WaitFor).

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

// ---------- Розподілений кеш (Лаб. 06) ----------

// Redis — кеш рівня L2, спільний для всіх екземплярів сервісів і Aggregator.
// Volume зберігає дані між перезапусками. Сервіси отримують рядок підключення
// як ConnectionStrings:redis — прописувати адресу в appsettings не потрібно.
var redis = builder.AddRedis("redis")
    .WithDataVolume("carpathians-redis-data");

// ---------- Мікросервіси (ім'я ресурсу = логічне ім'я для service discovery) ----------
// Профіль "https": gRPC працює поверх HTTP/2, а Kestrel узгоджує HTTP/2 через ALPN лише з TLS.
// Тож кожен сервіс завжди має HTTPS-endpoint (Http1AndHttp2): REST через Gateway + gRPC для Aggregator.

var booking = builder.AddProject<Projects.Carpathians_Booking_Api>("booking-api", launchProfileName: "https")
    .WithReference(bookingDb)
    .WaitFor(bookingDb)
    .WithReference(redis)
    .WaitFor(redis)
    .WithHttpHealthCheck("/health");

var catalog = builder.AddProject<Projects.Carpathians_Catalog_Api>("catalog-api", launchProfileName: "https")
    .WithReference(catalogDb)
    .WaitFor(catalogDb)
    .WithReference(redis)
    .WaitFor(redis)
    .WithHttpHealthCheck("/health");

var reviews = builder.AddProject<Projects.Carpathians_Reviews_WebApi>("reviews-api", launchProfileName: "https")
    .WithReference(reviewsDb)
    .WaitFor(reviewsDb)
    .WithReference(redis)
    .WaitFor(redis)
    .WithHttpHealthCheck("/health");

// ---------- Aggregator: паралельні gRPC-виклики мікросервісів + кеш агрегованих відповідей у Redis ----------

var aggregator = builder.AddProject<Projects.Carpathians_Platform_Aggregator>("aggregator")
    .WithReference(booking)
    .WithReference(catalog)
    .WithReference(reviews)
    .WaitFor(booking)
    .WaitFor(catalog)
    .WaitFor(reviews)
    .WithReference(redis)
    .WaitFor(redis)
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
