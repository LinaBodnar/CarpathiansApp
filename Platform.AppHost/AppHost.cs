using Aspire.Hosting.ApplicationModel;

// Orchestrator платформи Carpathians: лише конфігурація ресурсів, без бізнес-логіки.
// Порядок запуску: бази даних, Redis і RabbitMQ -> мікросервіси -> Aggregator -> Gateway (ланцюжки WaitFor).

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
var identityDb = sql.AddDatabase("IdentityDb");                     // Лаб. 09 — ASP.NET Identity (окрема база Auth Service)

var mongo = builder.AddMongoDB("mongo")
    .WithDataVolume("carpathians-mongo-data");
var reviewsDb = mongo.AddDatabase("ReviewsDb", "ReviewContext");    // Лаб. 04 — MongoDB

// ---------- Розподілений кеш (Лаб. 06) ----------

// Redis — кеш рівня L2, спільний для всіх екземплярів сервісів і Aggregator.
// Volume зберігає дані між перезапусками. Сервіси отримують рядок підключення
// як ConnectionStrings:redis — прописувати адресу в appsettings не потрібно.
var redis = builder.AddRedis("redis")
    .WithDataVolume("carpathians-redis-data");

// ---------- Брокер повідомлень (Лаб. 08) ----------

// RabbitMQ у Docker-контейнері; облікові дані генерує Aspire (параметри messaging-username/-password).
// Volume зберігає durable-черги й persistent-повідомлення між перезапусками.
// Management UI — http://localhost:15672 (exchanges, черги, bindings, DLQ; ручна публікація повідомлень).
// Сервіси отримують рядок підключення як ConnectionStrings:messaging.
var messaging = builder.AddRabbitMQ("messaging")
    .WithDataVolume("carpathians-rabbitmq-data")
    .WithManagementPlugin(port: 15672);

// ---------- Автентифікація (Лаб. 09) ----------

// Ключ підпису JWT Auth Service: генерується один раз і зберігається в user-secrets AppHost (persist),
// у репозиторії його немає. Той самий ключ отримують Auth Service (підпис), Gateway і мікросервіси (перевірка).
var jwtSecret = builder.AddParameter("jwt-secret",
    new GenerateParameterDefault { MinLength = 64, Special = false }, secret: true, persist: true);

// Пароль адміністратора, якого Auth Service створює під час старту (admin / admin@carpathians.local).
// Згенерований пароль видно в Dashboard: Parameters -> auth-admin-password.
var adminPassword = builder.AddParameter("auth-admin-password",
    new GenerateParameterDefault { MinLength = 16, Special = false, MinLower = 2, MinUpper = 2, MinNumeric = 2 },
    secret: true, persist: true);

// Auth Service: ASP.NET Identity + JWT (access 15 хв, refresh 7 днів з ротацією), база IdentityDb.
var auth = builder.AddProject<Projects.Carpathians_Auth_Api>("auth-api", launchProfileName: "https")
    .WithReference(identityDb)
    .WaitFor(identityDb)
    .WithEnvironment("Jwt__SecretKey", jwtSecret)
    .WithEnvironment("Auth__Admin__Password", adminPassword)
    .WithHttpHealthCheck("/health");

// ---------- Мікросервіси (ім'я ресурсу = логічне ім'я для service discovery) ----------
// Jwt__SecretKey (Лаб. 09): кожен сервіс перевіряє JWT сам, без звернення до Auth Service.
// WithHttpHealthCheck("/health") (Лаб. 07): Dashboard показує стан кожного ресурсу (Healthy / Degraded / Unhealthy)
// за health checks сервісу (БД, Redis, gRPC-залежності), а WaitFor чекає, поки залежність стане здоровою,
// а не лише запуститься. Деталі перевірок — JSON на /health, /health/ready; liveness — /health/live.
// Профіль "https": gRPC працює поверх HTTP/2, а Kestrel узгоджує HTTP/2 через ALPN лише з TLS.
// Тож кожен сервіс завжди має HTTPS-endpoint (Http1AndHttp2): REST через Gateway + gRPC для Aggregator.

var booking = builder.AddProject<Projects.Carpathians_Booking_Api>("booking-api", launchProfileName: "https")
    .WithReference(bookingDb)
    .WaitFor(bookingDb)
    .WithReference(redis)
    .WaitFor(redis)
    .WithReference(messaging)       // споживає події Каталогу: довідник RouteRefs
    .WaitFor(messaging)
    .WithEnvironment("Jwt__SecretKey", jwtSecret)
    .WithHttpHealthCheck("/health");

var catalog = builder.AddProject<Projects.Carpathians_Catalog_Api>("catalog-api", launchProfileName: "https")
    .WithReference(catalogDb)
    .WaitFor(catalogDb)
    .WithReference(redis)
    .WaitFor(redis)
    .WithReference(messaging)       // публікує події про маршрути (catalog-events)
    .WaitFor(messaging)
    .WithEnvironment("Jwt__SecretKey", jwtSecret)
    .WithHttpHealthCheck("/health");

var reviews = builder.AddProject<Projects.Carpathians_Reviews_WebApi>("reviews-api", launchProfileName: "https")
    .WithReference(reviewsDb)
    .WaitFor(reviewsDb)
    .WithReference(redis)
    .WaitFor(redis)
    .WithReference(messaging)       // споживає події Каталогу: routeName у відгуках і обговореннях
    .WaitFor(messaging)
    .WithEnvironment("Jwt__SecretKey", jwtSecret)
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
    .WithEnvironment("Jwt__SecretKey", jwtSecret)
    .WithHttpHealthCheck("/health");

// ---------- API Gateway: єдина точка входу, фіксований порт 5000 ----------

builder.AddProject<Projects.Carpathians_Platform_ApiGateway>("gateway")
    .WithHttpEndpoint(port: 5000, name: "public")
    .WithExternalHttpEndpoints()
    .WithReference(booking)
    .WithReference(catalog)
    .WithReference(reviews)
    .WithReference(auth)
    .WithReference(aggregator)
    .WaitFor(auth)
    .WaitFor(aggregator)
    .WithEnvironment("Jwt__SecretKey", jwtSecret)
    .WithHttpHealthCheck("/health");

builder.Build().Run();
