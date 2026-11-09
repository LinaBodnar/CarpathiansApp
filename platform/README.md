# Carpathians.Platform — мікросервісна платформа на .NET Aspire

Практичне заняття №05: інтеграція сервісів з Лаб. 02–04 в єдину платформу — **AppHost** (оркестрація),
**ServiceDefaults** (Serilog, OpenTelemetry, CorrelationId, health checks), **API Gateway** на YARP
і **Aggregator** (Aggregator Pattern).

Практичне заняття №06: **кешування** (IMemoryCache L1 + Redis L2), **gRPC-API** мікросервісів,
**Aggregator на gRPC** і обсервабіліті gRPC-трафіку та кешу — розділ [8](#8-grpc-і-кешування-лаб-06).

---

## 1. Архітектура

```
                         клієнт (curl / Swagger / Postman)
                                      │  http://localhost:5000
                                      ▼
                         ┌──────────────────────────┐
                         │  gateway (YARP)          │  CorrelationId, логування маршрутизації
                         └───┬───────┬───────┬──────┘
                             │       │       │        └──────────────┐
                             ▼       ▼       ▼                       ▼
                     booking-api catalog-api reviews-api ◄═gRPC═ aggregator
                         │           │           │        (Task.WhenAll, HTTP/2)
                         ▼           ▼           ▼            │
                   SQL Server   SQL Server    MongoDB         │
                  BookingContext CatalogContext ReviewContext │
                         ▲           ▲           ▲            │
                         └─── Redis (L2-кеш, спільний) ───────┘   + IMemoryCache (L1) у кожному сервісі
```

| Компонент | Проєкт | Призначення |
|---|---|---|
| AppHost | `platform/Carpathians.Platform.AppHost` | запускає контейнери SQL Server і MongoDB, сервіси, Aggregator і Gateway однією командою; service discovery; без бізнес-логіки |
| ServiceDefaults | `platform/Carpathians.Platform.ServiceDefaults` | спільні налаштування всіх сервісів: Serilog, OpenTelemetry, CorrelationId, health checks, service discovery, resilience HttpClient |
| ApiGateway | `platform/Carpathians.Platform.ApiGateway` | єдина точка входу (порт 5000): маршрутизація YARP за логічними іменами, CorrelationId, логи й метрики трафіку |
| Aggregator | `platform/Carpathians.Platform.Aggregator` | картка бронювання з даних трьох сервісів, паралельні виклики, часткові відмови |
| booking-api | `booking-service/Carpathians.Booking.Api` (Лаб. 02) | бронювання, виїзди, клієнти — SQL Server, ADO.NET + Dapper |
| catalog-api | `catalog-service/Carpathians.Catalog.Api` (Лаб. 03) | маршрути, регіони, точки — SQL Server, EF Core |
| reviews-api | `reviews-service/Carpathians.Reviews.WebApi` (Лаб. 04) | відгуки, обговорення, рейтинги — MongoDB, Clean Architecture |

Залежності односпрямовані: AppHost посилається на всі проєкти; кожен сервіс, Gateway і Aggregator
посилаються лише на ServiceDefaults; сервіси нічого не знають про AppHost і один про одного.
Сервіси з попередніх робіт залишилися у своїх теках, у рішенні `platform/Carpathians.Platform.sln`
вони зібрані в папку **Services**.

---

## 2. Запуск

Потрібно: **.NET 8 SDK**, **Docker Desktop** (запущений), Rider / Visual Studio або `dotnet` CLI,
`sqlcmd` для скриптів БД бронювання.

```powershell
# 1. Пароль SQL Server — секрет AppHost (один раз)
dotnet user-secrets set "Parameters:sql-password" "YourStrong!Passw0rd" --project platform/Carpathians.Platform.AppHost

# 2. Запуск усієї платформи
dotnet run --project platform/Carpathians.Platform.AppHost
```

Посилання на **Aspire Dashboard** (з токеном входу) виводиться в консоль AppHost. Якщо пароль не задано,
Dashboard попросить ввести його під час запуску.

3. Коли ресурс `sql` у статусі Running, застосуйте скрипти БД бронювання (порт контейнера — 14333):

```powershell
sqlcmd -S localhost,14333 -U sa -P "YourStrong!Passw0rd" -C -I -i booking-db/schema.sql
sqlcmd -S localhost,14333 -U sa -P "YourStrong!Passw0rd" -C -I -i booking-db/procedures.sql
sqlcmd -S localhost,14333 -U sa -P "YourStrong!Passw0rd" -C -I -i booking-db/seed.sql
```

Каталог створює схему міграціями EF Core і наповнює її сам; Відгуки створюють індекси й початкові дані
під час старту. Дані зберігаються у Docker volumes `carpathians-sql-data` і `carpathians-mongo-data`
і переживають перезапуск AppHost.

Порядок запуску задано ланцюжками `WaitFor`: бази даних → сервіси (готовність за `/health`) →
Aggregator → Gateway.

---

## 3. AppHost

| Ресурс | Тип | Деталі |
|---|---|---|
| `sql-password` | параметр-секрет | user-secrets AppHost або введення в Dashboard |
| `sql` | контейнер SQL Server | порт 14333, volume `carpathians-sql-data` |
| `BookingDb`, `CatalogDb` | бази на `sql` | `BookingContext`, `CatalogContext` |
| `mongo` | контейнер MongoDB | volume `carpathians-mongo-data` |
| `ReviewsDb` | база на `mongo` | `ReviewContext` |
| `booking-api`, `catalog-api`, `reviews-api` | проєкти | `WithReference(<база>)`, `WaitFor(<база>)`, health check `/health` |
| `aggregator` | проєкт | посилання на три сервіси, `WaitFor` усіх трьох |
| `gateway` | проєкт | `WithHttpEndpoint(port: 5000)`, посилання на сервіси й Aggregator, `WaitFor(aggregator)` |

Імена баз збігаються з іменами рядків підключення, які сервіси вже читали (`ConnectionStrings:BookingDb`,
`ConnectionStrings:CatalogDb`), тож Aspire передає їх без змін у коді. Для Відгуків рядок
`ConnectionStrings:ReviewsDb` перекладається в секцію `MongoDb` у `Program.cs`. Резервні рядки
підключення в `appsettings.json` сервісів залишено для запуску без Aspire.

---

## 4. ServiceDefaults

Кожен сервіс викликає `builder.AddServiceDefaults()`, `app.UseCorrelationId()` і `app.MapDefaultEndpoints()`.

**Serilog** — основний провайдер логування:
- кожен запис — JSON-об'єкт (`CompactJsonFormatter`) у консолі (вкладка Console logs Dashboard);
- enrichers: `ServiceName` (логічне ім'я ресурсу, наприклад `booking-api`), `Environment`, `MachineName`,
  `ThreadId`, `CorrelationId` (LogContext); `TraceId` і `SpanId` береться з поточного span;
- `writeToProviders: true` передає записи провайдеру OpenTelemetry → вкладка **Structured logs** Dashboard
  (пошук за властивостями, наприклад `BookingId = 1` або `CorrelationId = ...`);
- рівень Debug у Development, Information у Production; шумні категорії (Microsoft, EF Core,
  HttpClient, Polly) — Warning.

**OpenTelemetry**:
- трасування: ASP.NET Core (вхідні запити), HttpClient (вихідні виклики Gateway й Aggregator),
  SqlClient (Dapper у Лаб. 02 і EF Core у Лаб. 03), MongoDB (`DiagnosticsActivityEventSubscriber`
  у Лаб. 04), власні spans з джерел `Carpathians.*`;
- метрики: ASP.NET Core, HttpClient, runtime, власні метрики Gateway;
- атрибути ресурсу: `deployment.environment`, `service.logical_name`;
- sampling: 100 % у Development, `OpenTelemetry:TracesSamplingRatio` (за замовчуванням 0.25) у Production;
- OTLP-експорт у Dashboard (адресу передає AppHost), батчевий.

**CorrelationId** (`Correlation/`):
- `CorrelationIdMiddleware` бере `X-Correlation-Id` із запиту або генерує GUID (і логує це),
  записує його в заголовки запиту й відповіді, у LogContext і в тег span `correlation.id`;
  логує межі запиту (початок і кінець зі статусом і тривалістю);
- `CorrelationIdHandler` додає той самий ідентифікатор у вихідні запити всіх HttpClient.

CorrelationId — бізнес-ідентифікатор звернення, який створює застосунок (його можна назвати в підтримці);
TraceId — технічний ідентифікатор трасування, який генерує OpenTelemetry.

**Health checks:** `/health` — усі перевірки (у т. ч. MongoDB для Відгуків), `/alive` — лише «процес живий».

---

## 5. API Gateway (YARP)

Маршрути описано в `ApiGateway/appsettings.json` (секція `ReverseProxy`); адреси кластерів —
логічні імена (`http://booking-api`), реальні адреси підставляє service discovery
(`AddServiceDiscoveryDestinationResolver`).

| Шлях | Кластер | Order |
|---|---|---|
| `/api/aggregator/**` | aggregator | -10 |
| `/api/routes/{id}/rating`, `/api/routes/{id}/discussions`, `PUT /api/routes/{id}/name`, `/api/routes/top-rated` | reviews-api | -5 |
| `/api/reviews/**`, `/api/discussions/**` | reviews-api | 0 |
| `/api/routes/**`, `/api/regions/**`, `/api/points/**` | catalog-api (RoundRobin) | 0 |
| `/api/bookings/**`, `/api/customers/**`, `/api/departures/**` | booking-api | 0 |

Менше значення `Order` — вищий пріоритет: без нього `/api/routes/1/rating` потрапив би в Каталог.

Політики: таймаут запиту до сервісу (`ActivityTimeout` 30 с, для Aggregator 60 с), обмеження розміру
тіла запиту (`MaxRequestBodySize`), метадані маршрутів (`Team`, `Version`).

Gateway не просто проксує: `CorrelationIdMiddleware` генерує ідентифікатор до маршрутизації, а
`GatewayLoggingMiddleware` логує кожен запит (метод, шлях, IP клієнта, маршрут, кластер, адреса сервісу,
статус, тривалість, розмір) і помилки недоступних сервісів (`ForwarderError`: timeout, connection refused).
Метрики `gateway.requests`, `gateway.errors`, `gateway.request.duration` (за маршрутом, кластером, статусом)
дають кількість запитів за секунду, середній час відповіді й частку помилок.

---

## 6. Aggregator

| Ендпоінт | Дані |
|---|---|
| `GET /api/aggregator/bookings/{id}/full` | бронювання й клієнт (booking-api) + виїзди (booking-api) + маршрути (catalog-api) + рейтинг і 3 свіжі відгуки (reviews-api) |
| `GET /api/aggregator/routes/{routeId}/overview` | маршрут (catalog-api) + рейтинг і відгуки (reviews-api) |

Порядок викликів картки бронювання:
1. бронювання — обов'язкове: немає → **404**, booking-api недоступний → **503** (ProblemDetails);
2. клієнт і виїзди позицій — паралельно (`Task.WhenAll`);
3. маршрути з catalog-api, рейтинги й відгуки з reviews-api — усі одночасно (`Task.WhenAll`);
4. композиція `FullBookingDto`: `booking`, `customer`, `trips[]` (знімок назви маршруту з бронювання
   + актуальні дані з Каталогу + рейтинг + відгуки), `warnings`, `isComplete`.

Недоступність catalog-api чи reviews-api не «валить» запит: відповідь **200** з доступними даними й
попередженням `"<service> is unavailable"`. Перевіряється узгодженість: беруться лише виїзди позицій
цього бронювання й маршрути з тим самим `RouteId`. `CancellationToken` передається в усі виклики.
Typed HttpClients (`BookingClient`, `CatalogClient`, `ReviewsClient`) звертаються за адресою
`https+http://<ім'я ресурсу>`; кожен виклик логується («Calling catalog-api …» → «responded in N ms»).
Resilience агрегатора: одна повторна спроба, 5 с на спробу, 10 с загалом. JSON — camelCase без null-полів.

> З Лаб. 06 Aggregator викликає мікросервіси через **gRPC** (розділ [8.3](#83-grpc)), а відповіді кешує в Redis.
> Реалізація через HTTP збережена як `HttpAggregationService` — для порівняння затримок (розділ [8.5](#85-порівняння-http-і-grpc)).

---

## 7. Перевірка

```bash
curl -i http://localhost:5000/api/bookings/1
curl -i "http://localhost:5000/api/routes?page=1&pageSize=5"
curl -i http://localhost:5000/api/routes/1/rating
curl -i "http://localhost:5000/api/reviews?routeId=1"
curl -i http://localhost:5000/api/aggregator/bookings/1/full
curl -i -H "X-Correlation-Id: demo-0001" http://localhost:5000/api/aggregator/bookings/1/full
```

Готові запити — `ApiGateway/Carpathians.Platform.ApiGateway.http` і `Aggregator/Carpathians.Platform.Aggregator.http`.

| Що перевірити | Як |
|---|---|
| усі ресурси Running | Dashboard → Resources: `sql`, `mongo`, бази, три сервіси, `aggregator`, `gateway` |
| CorrelationId | у відповіді Gateway є `X-Correlation-Id`; Structured logs → фільтр `CorrelationId = <значення>` показує логи gateway, aggregator і сервісів |
| повний trace | Traces → запит `GET /api/aggregator/bookings/{id}/full`: gateway → aggregator → booking-api (SQL) → паралельні catalog-api (SQL) і reviews-api (MongoDB) |
| часткова відповідь | Dashboard → `reviews-api` → Stop; повторити агрегувальний запит → 200, `"isComplete": false`, `warnings: ["reviews-api is unavailable"]`; у логах aggregator — Warning з деталями; Start → відповідь знову повна |
| Gateway ≠ простий проксі | той самий запит напряму на адресу `booking-api` з Dashboard: немає логу маршрутизації Gateway, CorrelationId генерує сам сервіс |
| зміни в сервісах | `Program.cs` (AddServiceDefaults, UseCorrelationId, MapDefaultEndpoints); у Відгуках — підписка MongoDB на трасування, у Бронюванні — логування бізнес-подій і власні spans |

---

## 8. gRPC і кешування (Лаб. 06)

### 8.1. Нові елементи

| Елемент | Де | Що робить |
|---|---|---|
| Redis | `AppHost.cs`: `AddRedis("redis").WithDataVolume("carpathians-redis-data")` | контейнер Redis; `WithReference(redis).WaitFor(redis)` для трьох сервісів і Aggregator — рядок підключення `ConnectionStrings:redis` |
| L1-кеш | `ServiceDefaults/Caching/CachingExtensions.cs` | `AddMemoryCache`: `SizeLimit` (Size запису = КБ JSON), `CompactionPercentage`, `TrackStatistics`; TTL і пріоритети — у політиках сервісів |
| L2-кеш | `Program.cs` кожного сервісу: `builder.AddRedisDistributedCache("redis")` | `IDistributedCache` поверх Redis (Aspire: повторні підключення, логування, трасування) |
| Дворівневий кеш | `ServiceDefaults/Caching/CacheService.cs` | cache-aside L1 → L2 → БД, заповнення рівнів на зворотному шляху, захист від cache stampede, GZip для значень > 16 КБ, запобіжник для Redis |
| Декоратори з кешем | `*/Caching/Cached*.cs` у кожному сервісі | бізнес-логіка Лаб. 02–04 не змінюється; REST і gRPC працюють через ті самі інтерфейси, тож кеш діє для обох протоколів |
| Прогрівання | `catalog-service/.../Caching/CatalogCacheWarmupService.cs` | `BackgroundService`: регіони й популярні маршрути — під час старту і кожні 10 хв |
| gRPC-контракти | `*/Protos/*.proto`, спільні типи — `platform/Protos/common/types.proto` | `GrpcServices="Server"` у сервісах, ті самі файли з `GrpcServices="Client"` в Aggregator |
| gRPC-сервіси | `*/Grpc/*GrpcService.cs` | адаптери над BLL/MediatR; `MapGrpcService<T>()`, `AddGrpc(...)` з interceptor-ами |
| gRPC Aggregator | `Aggregator/Services/AggregationService.cs` | `AddGrpcClient<T>(https://<сервіс>)`, паралельні виклики, `RpcException` → Warnings, кеш агрегатів у Redis |
| Обсервабіліті | `ServiceDefaults/Grpc/GrpcServerLoggingInterceptor.cs`, `Aggregator/Grpc/GrpcClientLoggingInterceptor.cs`, `ServiceDefaults/Caching/CacheMetrics.cs` | spans і логи gRPC, метрики кешу (Meter `Carpathians.Cache`) |

### 8.2. Кешування

**Cache-aside з запасним шляхом.** `ICacheService.GetOrCreateAsync(key, factory, policy)`:
1. L1 (`IMemoryCache`) — є запис → відповідь без мережі й БД;
2. L2 (Redis) — є запис → кладеться в L1 і повертається;
3. база даних → результат записується в Redis і в L1.

Одночасні промахи того самого ключа не йдуть у базу паралельно: до джерела звертається лише один запит,
решта беруть уже заповнений кеш. `null` (немає даних) не кешується; винятки «не знайдено» прокидаються далі.

**Ключі** мають передбачувану схему з префіксом сервісу (ізоляція в спільному Redis):

| Сервіс | Ключі | L1 TTL | L2 TTL | Інвалідація |
|---|---|---|---|---|
| Каталог | `catalog:route:{id}` | 2 хв (sliding 1 хв) | 30 хв | зміна маршруту, його деталей, спорядження, точок; зміна точки на маршруті; перейменування регіону |
| | `catalog:routes:v{версія}:page:…`, `:all`, `:popular-challenging` | 1 хв | 5 хв | ключ-версія `catalog:routes:version` — одна інвалідація скидає всі списки |
| | `catalog:regions:all`, `catalog:region:{id}` | 5 хв | 2 год | створення/зміна/видалення регіону |
| Бронювання | `booking:departure:{id}`, `booking:departures:all` | 30 с | 5 хв / 2 хв | зміна виїзду; створення і скасування бронювання (вільні місця) |
| | `booking:customer:{id}` | 30 с | 2 хв | зміна/видалення клієнта (персональні дані — короткий TTL) |
| | `booking:booking:{id}` | 15 с | 1 хв | підтвердження, оплата, скасування |
| Відгуки | `reviews:route:{id}:rating`, `reviews:route:{id}:v{версія}:list:…` | 1 хв / 30 с | 10 хв / 3 хв | будь-який запис відгуку маршруту (декоратор репозиторію) |
| | `reviews:routes:top-rated:v{версія}:…` | 1 хв | 10 хв | будь-який запис відгуку |
| Aggregator | `aggregator:booking:{id}:full`, `aggregator:route:{id}:overview` | — | 30 с | сервіси видаляють ключ при зміні бронювання, маршруту чи відгуків |

- **L1 завжди коротший за L2** (перевіряється в `CachePolicy.Validate`): зміна, яку інші екземпляри вже бачать
  у Redis, не «застрягне» в пам'яті одного екземпляра довше за L1 TTL. Оновлення L1 через Redis pub/sub не використовується.
- **Absolute expiration** — довідники (регіони); **sliding + absolute** — часто запитувані картки маршрутів.
- **Інвалідація синхронна з записом**: декоратор видаляє ключі одразу після `SaveChanges`, до відповіді клієнту.
- **Агрегований кеш має найкоротший TTL (30 с)** і не зберігає часткових відповідей (з `warnings`).
- **Redis недоступний** → запобіжник на 30 с вимикає L2: сервіс відповідає з L1 і бази (повільніше), health check —
  `Degraded`, а не `Unhealthy`; старт сервісу від Redis не залежить (`AbortOnConnectFail = false`).
- **Статистика кешу**: `GET /health/cache` кожного сервісу (записи і розмір L1, частка попадань L1/L2, стан Redis).

### 8.3. gRPC

**Контракти.** `package carpathians.{сервіс}.v1`, коментарі до сервісів і полів потрапляють у згенерований код,
необов'язкові значення — wrapper-типи (`google.protobuf.StringValue` → `string?`), дати — `Timestamp`,
гроші й кілометри — спільний `DecimalValue` (units + nanos) замість `double`. Номери полів після публікації не змінюються:
саме вони, а не імена, визначають бінарний формат (див. перевірку в 8.6).

| Сервіс | RPC |
|---|---|
| `carpathians.booking.v1.BookingGrpc` | `GetBooking`, `GetCustomer`, `GetDeparturesByIds` |
| `carpathians.catalog.v1.CatalogGrpc` | `GetRoute`, `GetRoutesByIds`, `ListRegions` |
| `carpathians.reviews.v1.ReviewsGrpc` | `GetRouteRating`, `GetRatingsForRoutes`, `GetLatestReviews` |

**HTTP/2.** Сервіси запускаються з профілем `https` (`launchProfileName: "https"` в AppHost), Kestrel —
`Http1AndHttp2`: REST через Gateway і gRPC на тому самому HTTPS-endpoint, HTTP/2 узгоджується через ALPN.

**Помилки.** Некоректний запит → `INVALID_ARGUMENT` (перевірка в методі або FluentValidation);
доменні винятки перетворює interceptor сервісу: «не знайдено» → `NOT_FOUND`, бізнес-правило → `FAILED_PRECONDITION`,
конфлікт → `ALREADY_EXISTS`/`ABORTED`, недоступна MongoDB → `UNAVAILABLE`; неочікувана помилка → `INTERNAL` без деталей.

**Aggregator.** gRPC-клієнти створює фабрика HttpClient, тому для них діють налаштування ServiceDefaults:
service discovery (`https://catalog-api`), resilience і `CorrelationIdHandler` (заголовок `X-Correlation-Id`
стає метаданими gRPC-виклику). Картка бронювання: бронювання (обов'язкове) → клієнт і виїзди паралельно →
маршрути, рейтинги й відгуки паралельно (пакетні RPC замість виклику на кожен елемент). Кожен виклик має
deadline 5 с і `CancellationToken`. `RpcException` необов'язкового сервісу (`UNAVAILABLE`, `DEADLINE_EXCEEDED`, ...)
→ попередження у `warnings`, `NOT_FOUND` → «даних немає». Для зовнішніх клієнтів Aggregator лишається HTTP-ендпоінтом;
заголовок `X-Cache: HIT/MISS` показує, чи відповідь узята з Redis.

### 8.4. Обсервабіліті

| Що | Де видно |
|---|---|
| gRPC-spans | Traces: `gateway` → `aggregator` (`AggregateFullBooking`) → span клієнта `carpathians.catalog.v1.CatalogGrpc/GetRoutesByIds` (rpc.system, rpc.service, rpc.method, rpc.grpc.status_code, cache.result) → HTTP/2 POST → span сервера в `catalog-api` (ті самі rpc.* + correlation.id) → SQL / MongoDB / Redis |
| логи gRPC | Structured logs, `Protocol = grpc`: `gRPC {GrpcMethod} from {Peer}: {GrpcStatus} in {ElapsedMs} ms, cache {CacheResult}`; помилки — Warning/Error зі stack trace |
| логи кешу | `Cache hit {CacheKey} at {CacheLevel}...`, `Cache miss ...`, `Cache invalidated {CacheKeys}, reason {Reason}`, витіснення, недоступність Redis |
| метрики кешу | Metrics → Meter `Carpathians.Cache`: `cache.hits`, `cache.misses` (теги `cache.level` = l1/l2, `cache.transport` = http/grpc/background), `cache.hit_ratio`, `cache.memory.entries`, `cache.memory.size`, `cache.evictions`, `cache.invalidations`, `cache.operation.duration`, `cache.distributed.available` |
| CorrelationId | у метаданих кожного gRPC-виклику (`x-correlation-id`), в атрибуті `correlation.id` span сервера й у логах усіх сервісів |
| ознака попадання в кеш | trailer `x-cache` у відповіді gRPC (`l1`, `l2`, `source` або `l1=2,source=1` для пакетних методів) |

### 8.5. Порівняння HTTP і gRPC

`GET http://localhost:5000/api/aggregator/benchmark/bookings/1?iterations=50` — та сама картка бронювання
збирається поперемінно через HTTP REST (реалізація Лаб. 05, `HttpAggregationService`) і через gRPC,
без кешу агрегованих відповідей, після прогріву. Відповідь: середнє, медіана, p95, мінімум і максимум (мс)
для кожного транспорту та `grpcSpeedup` = HTTP avg / gRPC avg. Кожен виклик — окремий trace, тож результати
звіряються в Traces (`AggregateFullBooking (HTTP)` проти `AggregateFullBooking`).

Чому gRPC швидший: бінарний Protobuf замість JSON, одне постійне HTTP/2-з'єднання з мультиплексуванням
паралельних викликів замість окремих HTTP/1.1-з'єднань, пакетні RPC (`GetRoutesByIds`, `GetRatingsForRoutes`,
`GetDeparturesByIds`) замість запиту на кожен маршрут чи виїзд.

### 8.6. Перевірка

| Що перевірити | Як |
|---|---|
| Redis Running | Dashboard → Resources: `redis` у статусі Running |
| повторний запит без SQL | двічі `GET /api/routes/1` → у trace другого запиту немає span SQL (catalog-api відповів з L1) |
| PUT → свіжі дані | `PUT /api/routes/1` → наступний `GET /api/routes/1` повертає оновлені дані; у логах `Cache invalidated ... RouteUpdated` |
| дані з Redis | перезапустити `catalog-api` (L1 порожній) → `GET /api/routes/1` без SQL, у логах `Cache hit catalog:route:1 at l2` |
| Redis зупинено | Dashboard → `redis` → Stop: запити працюють (з бази), `/health/cache` → `Degraded`, у логах Warning про недоступний L2; Start → кеш знову працює |
| NOT_FOUND | Postman → gRPC → `https://localhost:<https-порт catalog-api>`, server reflection, `GetRoute {"id": 99999}` → `NOT_FOUND` |
| gRPC-spans в одному trace | `GET /api/aggregator/bookings/1/full` → Traces: span клієнта Aggregator і span сервера сервісу — батько і нащадок |
| кеш агрегатів | повторний запит → `X-Cache: HIT`, у trace немає gRPC-викликів |
| часткова відповідь | Dashboard → `catalog-api` → Stop → `isComplete: false`, `warnings: ["catalog-api is unavailable"]` |
| метрики | Metrics → `catalog-api` → `cache.hits` > 0 |
| HTTP vs gRPC | `GET /api/aggregator/benchmark/bookings/1?iterations=50` |
| сумісність Protobuf | Aggregator і catalog-api збирають код з одного `catalog.proto`; для перевірки скопіювати його в Aggregator, змінити в копії номер поля `name = 4` на `name = 14` і вказати копію в `Carpathians.Platform.Aggregator.csproj` → Aggregator отримує порожню назву маршруту (поле з невідомим номером ігнорується); повернути як було |

Готові запити — `Aggregator/Carpathians.Platform.Aggregator.http`.

