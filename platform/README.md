# Carpathians.Platform — мікросервісна платформа на .NET Aspire

Практичне заняття №05: інтеграція сервісів з Лаб. 02–04 в єдину платформу — **AppHost** (оркестрація),
**ServiceDefaults** (Serilog, OpenTelemetry, CorrelationId, health checks), **API Gateway** на YARP
і **Aggregator** (Aggregator Pattern).

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
                     booking-api catalog-api reviews-api      aggregator
                         │           │           │        (Task.WhenAll до трьох сервісів)
                         ▼           ▼           ▼
                   SQL Server   SQL Server    MongoDB
                  BookingContext CatalogContext ReviewContext
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
