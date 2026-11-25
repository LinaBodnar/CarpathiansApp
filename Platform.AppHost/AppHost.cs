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

// ---------- Duende IdentityServer (Лаб. 09, частина 2): OAuth 2.0 / OpenID Connect ----------

// Окремий SQL Server для IdentityServer: конфігурація (clients, scopes, resources), operational data (grants, keys)
// і користувачі ASP.NET Identity. Паролі генеруються один раз і зберігаються в user-secrets AppHost (persist).
var identityServerSqlPassword = builder.AddParameter("identityserver-sql-password",
    new GenerateParameterDefault { MinLength = 20, Special = false, MinLower = 2, MinUpper = 2, MinNumeric = 2 },
    secret: true, persist: true);
var identityServerDb = builder.AddSqlServer("identityserver-sql", identityServerSqlPassword)
    .WithDataVolume("carpathians-identityserver-sql-data")
    .AddDatabase("IdentityServerDb");

// Секрет клієнта aggregator_service (Client Credentials) — той самий отримують IdentityServer і Aggregator.
var aggregatorClientSecret = builder.AddParameter("aggregator-client-secret",
    new GenerateParameterDefault { MinLength = 32, Special = false }, secret: true, persist: true);

// Пароль тестових користувачів alice (admin), bob (manager), carol (user), dave (guest) — у Dashboard: Parameters.
var testUsersPassword = builder.AddParameter("identityserver-test-password",
    new GenerateParameterDefault { MinLength = 16, Special = false, MinLower = 2, MinUpper = 2, MinNumeric = 2 },
    secret: true, persist: true);

var identityServer = builder.AddProject<Projects.Carpathians_Platform_IdentityServer>("identityserver", launchProfileName: "https")
    .WithReference(identityServerDb)
    .WaitFor(identityServerDb)
    .WithEnvironment("Clients__AggregatorSecret", aggregatorClientSecret)
    .WithEnvironment("TestUsers__Password", testUsersPassword)
    .WithHttpHealthCheck("/health");

// Issuer токенів IdentityServer = його HTTPS-адреса; сервіси завантажують ключі з discovery document.
var identityServerAuthority = identityServer.GetEndpoint("https");

// ---------- Keycloak (Лаб. 09, частина 3): Identity Provider з Authorization Services ----------

// Траєкторія А: контейнер Keycloak + PostgreSQL (дані realm — у базі keycloakdb на volume). Realm, ролі,
// resource servers, політики, permissions, клієнтів і тестових користувачів створює Pulumi-проєкт infra/keycloak.
// Admin Console — http://localhost:8080 (admin / пароль із Dashboard: Parameters -> keycloak-admin-password).
var keycloakDbPassword = builder.AddParameter("keycloak-db-password",
    new GenerateParameterDefault { MinLength = 16, Special = false },
    secret: true, persist: true);

var keycloakPostgres = builder.AddPostgres("keycloak-postgres", password: keycloakDbPassword)
    .WithDataVolume("carpathians-keycloak-postgres-data");
var keycloakDb = keycloakPostgres.AddDatabase("keycloakdb");

var keycloakAdminPassword = builder.AddParameter("keycloak-admin-password",
    new GenerateParameterDefault { MinLength = 16, Special = false, MinLower = 2, MinUpper = 2, MinNumeric = 2 },
    secret: true, persist: true);

var keycloak = builder.AddContainer("keycloak", "quay.io/keycloak/keycloak", "26.4")
    .WithArgs("start-dev")
    .WithEnvironment("KC_BOOTSTRAP_ADMIN_USERNAME", "admin")
    .WithEnvironment("KC_BOOTSTRAP_ADMIN_PASSWORD", keycloakAdminPassword)
    .WithEnvironment("KC_HOSTNAME", "http://localhost:8080")      // фіксований issuer токенів
    .WithEnvironment("KC_HEALTH_ENABLED", "true")
    // база Keycloak — PostgreSQL (контейнери бачать один одного за іменем ресурсу)
    .WithEnvironment("KC_DB", "postgres")
    .WithEnvironment("KC_DB_URL", "jdbc:postgresql://keycloak-postgres:5432/keycloakdb")
    .WithEnvironment("KC_DB_USERNAME", "postgres")
    .WithEnvironment("KC_DB_PASSWORD", keycloakDbPassword)
    .WaitFor(keycloakDb)
    .WithHttpEndpoint(port: 8080, targetPort: 8080, name: "http")  // фіксований порт: Pulumi і Postman
    .WithHttpEndpoint(targetPort: 9000, name: "management")        // /health/ready
    .WithVolume("carpathians-keycloak-data", "/opt/keycloak/data")
    .WithHttpHealthCheck("/health/ready", endpointName: "management");

// Issuer токенів realm DevRealmDrivers (KC_HOSTNAME + /realms/<realm>).
const string keycloakAuthority = "http://localhost:8080/realms/DevRealmDrivers";
var keycloakEndpoint = keycloak.GetEndpoint("http");      // посилання для сервісів: services__keycloak__http__0

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
    .WithEnvironment("IdentityServer__Authority", identityServerAuthority)
    .WithEnvironment("IdentityServer__Audience", "booking-api")
    .WithEnvironment("Keycloak__Authority", keycloakAuthority)
    .WithReference(keycloakEndpoint)
    .WaitFor(keycloak)
    .WithEnvironment("Keycloak__Audience", "booking-api")
    .WithHttpHealthCheck("/health");

var catalog = builder.AddProject<Projects.Carpathians_Catalog_Api>("catalog-api", launchProfileName: "https")
    .WithReference(catalogDb)
    .WaitFor(catalogDb)
    .WithReference(redis)
    .WaitFor(redis)
    .WithReference(messaging)       // публікує події про маршрути (catalog-events)
    .WaitFor(messaging)
    .WithEnvironment("Jwt__SecretKey", jwtSecret)
    .WithEnvironment("IdentityServer__Authority", identityServerAuthority)
    .WithEnvironment("IdentityServer__Audience", "catalog-api")
    .WithEnvironment("Keycloak__Authority", keycloakAuthority)
    .WithReference(keycloakEndpoint)
    .WaitFor(keycloak)
    .WithEnvironment("Keycloak__Audience", "catalog-api")
    .WithHttpHealthCheck("/health");

var reviews = builder.AddProject<Projects.Carpathians_Reviews_WebApi>("reviews-api", launchProfileName: "https")
    .WithReference(reviewsDb)
    .WaitFor(reviewsDb)
    .WithReference(redis)
    .WaitFor(redis)
    .WithReference(messaging)       // споживає події Каталогу: routeName у відгуках і обговореннях
    .WaitFor(messaging)
    .WithEnvironment("Jwt__SecretKey", jwtSecret)
    .WithEnvironment("IdentityServer__Authority", identityServerAuthority)
    .WithEnvironment("IdentityServer__Audience", "reviews-api")
    .WithEnvironment("Keycloak__Authority", keycloakAuthority)
    .WithReference(keycloakEndpoint)
    .WaitFor(keycloak)
    .WithHttpHealthCheck("/health");

// Платежі (Лаб. 09): демо-сервіс для перевірки доступу — усі endpoints лише для admin.
var payments = builder.AddProject<Projects.Carpathians_Payments_Api>("payments-api", launchProfileName: "https")
    .WithEnvironment("Jwt__SecretKey", jwtSecret)
    .WithEnvironment("IdentityServer__Authority", identityServerAuthority)
    .WithEnvironment("IdentityServer__Audience", "payments-api")
    .WithEnvironment("Keycloak__Authority", keycloakAuthority)
    .WithReference(keycloakEndpoint)
    .WaitFor(keycloak)
    .WithEnvironment("Keycloak__Audience", "payments-api")
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
    .WithEnvironment("IdentityServer__Authority", identityServerAuthority)
    .WithEnvironment("IdentityServer__Audience", "booking-api,catalog-api,reviews-api")
    .WithEnvironment("Keycloak__Authority", keycloakAuthority)
    .WithReference(keycloakEndpoint)
    .WaitFor(keycloak)
    .WithEnvironment("Keycloak__Audience", "booking-api,catalog-api")
    .WithEnvironment("Clients__AggregatorSecret", aggregatorClientSecret)   // Client Credentials -> gRPC мікросервісів
    .WaitFor(identityServer)
    .WithHttpHealthCheck("/health");

// ---------- API Gateway: єдина точка входу, фіксований порт 5000 ----------

builder.AddProject<Projects.Carpathians_Platform_ApiGateway>("gateway")
    .WithHttpEndpoint(port: 5000, name: "public")
    .WithExternalHttpEndpoints()
    .WithReference(booking)
    .WithReference(catalog)
    .WithReference(reviews)
    .WithReference(auth)
    .WithReference(payments)
    .WithReference(aggregator)
    .WaitFor(auth)
    .WaitFor(aggregator)
    .WithEnvironment("Jwt__SecretKey", jwtSecret)
    .WithEnvironment("IdentityServer__Authority", identityServerAuthority)
    .WithEnvironment("IdentityServer__Audience", "booking-api,catalog-api,payments-api,reviews-api")
    .WithEnvironment("Keycloak__Authority", keycloakAuthority)
    .WithReference(keycloakEndpoint)
    .WaitFor(keycloak)
    .WithEnvironment("Keycloak__Audience", "booking-api,catalog-api,payments-api")
    .WithHttpHealthCheck("/health");

builder.Build().Run();
