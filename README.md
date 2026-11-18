# Carpathians - бази даних для мікросервісної архітектури

Практичне заняття №01: проєктування баз даних для трьох незалежних контекстів (SQL Server, SQL Server + EF Core, MongoDB).

## 1. Предметна область

Carpathians - веб-платформа для любителів активного відпочинку та гірського туризму. Нею користуються туристи й мандрівники: переглядають карпатські маршрути та точки на карті, добирають спорядження, бронюють місця на походи й залишають відгуки. Мета системи - зробити вибір гірських турів, бронювання та спілкування учасників зручним і надійним завдяки мікросервісній архітектурі. 

## 2. Контексти (Bounded Contexts)

| Контекст | БД і технологія | Сутності | Зв'язки |
|---|---|---|---|
| **Бронювання** (Проєкт №1) | `BookingContext`, SQL Server, ADO.NET + Dapper | Customers, RouteRefs, Departures, Bookings, BookingDetails, BookingItems, Payments, BookingStatusHistory | 1:1 Bookings-BookingDetails; 1:N Customers-Bookings; M:N Bookings-Departures через BookingItems |
| **Каталог маршрутів** (Проєкт №2) | `CatalogContext`, SQL Server, EF Core Code First | Regions, Routes, RouteDetails, Points, RoutePoints, GalleryImages, GearItems | 1:N Regions-Routes, Points-GalleryImages, Routes-GearItems; 1:1 Routes-RouteDetails; M:N Routes-Points через RoutePoints |
| **Відгуки та обговорення** (Проєкт №3) | `ReviewContext`, MongoDB | reviews, discussions, discussionMessages, ratings | embedding (author, comments), referencing (routeId, discussionId), гібрид (discussions.lastMessages + discussionMessages) |

Кожна сутність належить рівно одному контексту. Каталог володіє маршрутами й точками, Бронювання - клієнтами, виїздами та платежами, Відгуки - відгуками й обговореннями. `RouteRefs` у БД Бронювання - лише копія довідника маршрутів, власник - Каталог.

## 3. Структура репозиторію

```
<корінь репозиторію>/
├── README.md
├── Platform.slnx                       # одне рішення на всю платформу
├── Platform.AppHost/                   # оркестрація Aspire (AppHost.cs) — Проєкт №5
├── Platform.ServiceDefaults/           # Extensions.cs: Serilog, OpenTelemetry, resilience, health endpoints, Meter-и; кеш
├── Platform.ApiGateway/                # YARP — єдина точка входу
├── Platform.Aggregator/                # gRPC-/HTTP-клієнти, агрегація, кеш агрегатів, Resilience/, Metrics/, HealthChecks/
├── Protos/common/                      # спільні типи gRPC-контрактів (DecimalValue)
├── Services/
│   ├── Booking/                        # Проєкт №1 — ADO.NET + Dapper (Лаб. 02)
│   │   ├── Carpathians.Booking.Api/    # контролери, Protos/, Grpc/, Caching/, Metrics/, HealthChecks/, Program.cs
│   │   ├── Carpathians.Booking.Bll/
│   │   ├── Carpathians.Booking.Dal/
│   │   ├── Carpathians.Booking.Domain/
│   │   └── Carpathians.Booking.Tests/
│   ├── Catalog/                        # Проєкт №2 — EF Core (Лаб. 03)
│   │   ├── Carpathians.Catalog.Api/    # та сама будова, що й Booking.Api, + Dockerfile
│   │   ├── Carpathians.Catalog.Bll/
│   │   ├── Carpathians.Catalog.Dal/    # DbContext, Configurations/, Migrations/
│   │   ├── Carpathians.Catalog.Domain/
│   │   └── Carpathians.Catalog.Tests/
│   └── Reviews/                        # Проєкт №3 — Clean Architecture + CQRS + MongoDB (Лаб. 04)
│       ├── Carpathians.Reviews.Domain/
│       ├── Carpathians.Reviews.Application/
│       ├── Carpathians.Reviews.Infrastructure/
│       ├── Carpathians.Reviews.WebApi/ # контролери, Grpc/, Caching/, Metrics/, HealthChecks/, Program.cs
│       └── Carpathians.Reviews.Tests/
├── db/
│   ├── p1/                             # BookingContext (SQL Server)
│   │   ├── schema.sql                  # DDL: таблиці, ключі, CHECK, індекси
│   │   ├── procedures.sql              # 6 процедур + функція переходів статусів
│   │   ├── seed.sql                    # тестові дані (ідемпотентний)
│   │   ├── checks.sql                  # перевірка обмежень схеми
│   │   └── checks_procedures.sql       # перевірка процедур
│   ├── p2/                             # CatalogContext (SQL Server)
│   │   ├── schema.sql                  # схема з міграцій EF Core (Лаб. 03)
│   │   ├── seed.sql
│   │   ├── migrations.sql              # ідемпотентний скрипт міграцій
│   │   └── design/                     # проєктна схема й дані Лаб. 01
│   └── p3/                             # ReviewContext (MongoDB)
│       ├── collections.js              # колекції, JSON Schema, індекси
│       ├── seed.js                     # тестові дані (ідемпотентний)
│       └── queries.js                  # запити й aggregation pipeline
└── docs/
    ├── platform.md                     # платформа: Aspire, gRPC, кеш, resilience, health checks
    ├── erd-booking.dbml                # ERD Проєкту №1 (dbdiagram.io)
    ├── erd-catalog.dbml                # ERD Проєкту №2 (dbdiagram.io)
    ├── erd-catalog.md
    └── erd-catalog.mermaid
```

Сервіси з Лаб. 02–04 зберігають власні AppHost-проєкти (`Services/*/Carpathians.*.AppHost`) для автономного запуску;
вся платформа запускається з `Platform.AppHost`.

## 4. Що потрібно встановити

Для успішного розгортання та тестування проєкту тобі знадобляться:

- **SQL Server** (Developer або Express) і SSMS **або** SQL Server у Docker + VS Code з розширенням «SQL Server (mssql)»[cite: 1];
- `sqlcmd` (за бажанням; можна виконувати скрипти в SSMS)[cite: 1];
- **MongoDB** (локально через офіційний інсталятор або в Docker) і утиліта `mongosh`[cite: 1].

### Швидкий запуск інфраструктури через Docker (опціонально)

Якщо ти використовуєш Docker, можна підняти сервери однією командою[cite: 1]:

**SQL Server у Docker:**[cite: 1]
```bash
docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=YourStrong!Passw0rd" \
  -p 1433:1433 --name sql-carpathians -d [mcr.microsoft.com/mssql/server:2022-latest](https://mcr.microsoft.com/mssql/server:2022-latest)
```

MongoDB у Docker:[cite: 1]
```bash
docker run -d --name mongo-carpathians -p 27017:27017 mongo:7
```

## 5. Розгортання з нуля

### 5.1. Бронювання (BookingContext)

Порядок важливий. Виконуйте скрипти в SSMS (Execute) або через `sqlcmd` (прапорець `-I` **обов'язковий**: у схемі є фільтровані індекси, а процедури запам'ятовують `QUOTED_IDENTIFIER`).

```bash
sqlcmd -S . -E -I -i db/p1/schema.sql
sqlcmd -S . -E -I -i db/p1/procedures.sql
sqlcmd -S . -E -I -i db/p1/seed.sql
```

Для Docker-варіанту замініть `-E` на `-U sa -P "YourStrong!Passw0rd" -C` і вкажіть `-S localhost,1433`.

Перевірка: `db/p1/checks.sql` (обмеження, FK, каскади, індекси) і `db/p1/checks_procedures.sql` (процедури). Усі рядки в Messages мають починатися з `OK`.

### 5.2. Каталог (CatalogContext)

```bash
sqlcmd -S . -E -I -i db/p2/design/schema.sql
sqlcmd -S . -E -I -i db/p2/design/seed.sql
```

`db/p2/design/schema.sql` - проєктна схема для звіту; у Лаб. 03 її реалізує EF Core Code First (Fluent API, міграції, `HasData`).

### 5.3. Відгуки (ReviewContext, MongoDB)

```bash
mongosh "mongodb://localhost:27017" --file db/p3/collections.js
mongosh "mongodb://localhost:27017" --file db/p3/seed.js
mongosh "mongodb://localhost:27017" --file db/p3/queries.js   # приклади запитів
```

### 5.4. Повторний запуск

Усі скрипти ідемпотентні: `schema.sql` перевіряє наявність таблиць, `seed.sql` використовує `NOT EXISTS` (виїзди та демо-бронювання створюються один раз), `seed.js` робить upsert за фіксованими `_id`. Повторний запуск не створює дублікатів.

## 6. Що перевірити після розгортання

1. `SELECT * FROM sys.foreign_keys` у `BookingContext` і `CatalogContext` не показує посилань за межі своєї БД.
2. Виклик процедури з недопустимим переходом (наприклад, `usp_ChangeBookingStatus @NewStatus = 'Completed'` для бронювання зі статусом `Submitted`) завершується помилкою 50002.
3. `db.reviews.find()` у MongoDB повертає документи з різними наборами полів (`photos`, `gpsTrack`, `weather`, `tags`, `comments`).
4. Очікувані кількості після seed: BookingContext - 3 клієнти, 3 RouteRefs, 4 виїзди, 3 бронювання, 8 записів історії; CatalogContext - 3 регіони, 3 маршрути, 5 точок, 11 зв'язків RoutePoints; MongoDB - 5 reviews, 2 discussions, 6 discussionMessages, 3 ratings.

## 7. Рішення щодо ключів, індексів і конкурентності

- **Первинні ключі:** `BIGINT IDENTITY` (в Каталозі `INT IDENTITY` для малого довідника `Regions`). Кожну БД заповнює одне джерело, тому послідовні числові ключі компактніші й швидші за GUID. `RouteRefs.Id` - без IDENTITY, це той самий Id, що в Каталозі. У MongoDB - `ObjectId`.
- **Каскади:** лише там, де дочірні дані не існують без батьків (`BookingDetails`, `BookingItems`, `RouteDetails`, `RoutePoints`, `GalleryImages`, `GearItems`). Від клієнта до бронювань і від точки до `RoutePoints` каскаду немає.
- **Індекси:** кожен створено під конкретний запит, пояснення - у коментарях DDL (фільтровані індекси лише для «живих» записів і виїздів з вільними місцями).
- **Конкурентність:** `RowVersion` у `Customers`, `Departures`, `Bookings`, `Routes` не дає двом користувачам «тихо» затерти зміни один одного (оптимістичний підхід). Для критичних операцій (резервування місць, зміна статусу) процедури додатково беруть `UPDLOCK` (песимістичний підхід у межах короткої транзакції).
- **Узгодженість місць:** `Departures.SeatsAvailable` - денормалізований лічильник; його змінюють лише процедури `usp_CreateBooking` і `usp_CancelBooking` в одній транзакції з бронюванням.
- **Soft delete і аудит:** `IsDeleted`, `CreatedAt/By`, `UpdatedAt/By`. Журнальні таблиці (`BookingStatusHistory`, `Payments`) мають лише потрібні для них поля.

## 8. Правила переходів статусів бронювання

```
Submitted -> AwaitingValidation -> SeatsConfirmed -> Paid -> Completed
Cancelled - лише зі Submitted, AwaitingValidation, SeatsConfirmed (до оплати)
```

| Процедура | Призначення |
|---|---|
| `usp_CreateBooking` | створення бронювання, резервування місць |
| `usp_GetBookingById` | читання бронювання (4 набори результатів) |
| `usp_GetCustomerBookings` | список бронювань клієнта з пагінацією |
| `usp_ChangeBookingStatus` | транзакційна зміна статусу з перевіркою переходу |
| `usp_PayBooking` | оплата і перехід у `Paid` |
| `usp_CancelBooking` | скасування до оплати і повернення місць |

Коди керованих помилок: 50001 не знайдено, 50002 недопустимий перехід, 50003 немає місць, 50005 некоректний параметр, 50006 виїзд у минулому, 50007 статус змінюється іншою процедурою.

## 9. Політика дублювання даних

Між базами немає FOREIGN KEY. Повна версія - `docs/duplication-policy.md`.

| Поле | Де зберігається | Джерело (власник) | Політика узгодженості |
|---|---|---|---|
| `RouteRefs.Id`, `RouteRefs.Name` | БД Бронювання | `Routes` (БД Каталогу) | Копія для читання, оновлюється подіями Каталогу |
| `BookingItems.RouteName` | БД Бронювання | `RouteRefs.Name` | Знімок на момент оформлення, не оновлюється |
| `BookingItems.UnitPrice` | БД Бронювання | `Departures.Price` (та сама БД) | Знімок на момент оформлення, не оновлюється |
| `reviews.routeId` | БД Відгуків | `Routes.Id` (БД Каталогу) | Логічне посилання без FK |
| `reviews.routeName`, `discussions.routeName` | БД Відгуків | `Routes.Name` (БД Каталогу) | Копія для читання, оновлюється подіями Каталогу |
| `reviews.author.userId` | БД Відгуків | `Customers.Id` (БД Бронювання) | Логічне посилання без FK |
| `reviews.author.displayName` | БД Відгуків | `Customers.FullName` (БД Бронювання) | Знімок на момент створення відгуку |
| `reviews.isVerifiedParticipant` | БД Відгуків | `Bookings` зі статусом `Completed` | Оновлюється подією про завершення поїздки |
| `ratings.avgRating`, `ratings.count` | БД Відгуків | `reviews` (та сама БД) | Похідні дані, перераховуються агрегацією |

## 10. ERD

Код діаграм у форматі DBML: `docs/erd-booking.dbml` і `docs/erd-catalog.dbml`. Вставте вміст на [dbdiagram.io](https://dbdiagram.io/d), перевірте відображення зв'язків і експортуйте PNG у `docs/`.

## 11. Права доступу

У кінці `db/p1/schema.sql` і `db/p2/design/schema.sql` наведено закоментований приклад окремого логіна застосунку з доступом лише до своєї БД (принцип найменших привілеїв).

## 12. Booking Service (Лаб. 02)

Мікросервіс контексту «Бронювання»: ASP.NET Core Web API, тришарова архітектура
(Api -> Bll -> Dal), ADO.NET + Dapper, Unit of Work, .NET Aspire.

### 12.1. Структура рішення

```
Services/Booking/
├── Carpathians.Booking.Domain/   # моделі, константи, доменні винятки
├── Carpathians.Booking.Dal/      # репозиторії (ADO.NET, Dapper), UnitOfWork
├── Carpathians.Booking.Bll/      # сервіси, DTO, профілі AutoMapper
├── Carpathians.Booking.Api/      # контролери, ProblemDetails, Swagger
├── Carpathians.Booking.AppHost/  # .NET Aspire (API + SQL Server у контейнері)
└── Carpathians.Booking.Tests/    # unit-тести (NUnit, NSubstitute)
```

Залежності спрямовані в один бік: Api -> Bll -> Dal; Domain підключають і Dal, і Bll.
Контролер звертається лише до сервісів, SQL-запити є тільки в Dal.

| Репозиторій | Реалізація |
|---|---|
| CustomerRepository | чистий ADO.NET (`SqlCommand`, `SqlDataReader`), Generic Repository не успадковує |
| DepartureRepository | ADO.NET + Dapper, `BaseDapperRepository<T>` |
| BookingRepository | ADO.NET + Dapper (multi-mapping, виклики збережених процедур) |

### 12.2. Що потрібно для запуску

- .NET SDK 9 (або 8)
- Docker Desktop (SQL Server запускає Aspire)
- Aspire CLI (Windows): `irm https://aspire.dev/install.ps1 | iex`, перевірка: `aspire --version`
- Rider або Visual Studio
- `sqlcmd` або Database-панель Rider для застосування SQL-скриптів

### 12.3. Запуск через Aspire

1. Запустіть Docker Desktop.
2. Запустіть проєкт `Carpathians.Booking.AppHost` (з IDE або `dotnet run` у його папці).
   Пароль SQL Server для локальної розробки задано в
   `Services/Booking/Carpathians.Booking.AppHost/appsettings.Development.json`
   (`Parameters:sql-password`). Це пароль лише локального контейнера.
3. Aspire створить порожню базу `BookingContext` у контейнері (порт `14333`).
   Застосуйте скрипти з кореня репозиторію в такому порядку:

```bash
sqlcmd -S localhost,14333 -U sa -P "<пароль>" -C -I -i db/p1/schema.sql
sqlcmd -S localhost,14333 -U sa -P "<пароль>" -C -I -i db/p1/procedures.sql
sqlcmd -S localhost,14333 -U sa -P "<пароль>" -C -I -i db/p1/seed.sql
```

Прапорець `-I` обов'язковий. Замість `sqlcmd` можна виконати ці три файли в Database-панелі
Rider або в SSMS, підключившись до `localhost,14333` (логін `sa`).
4. Відкрийте Aspire Dashboard (посилання в консолі). Ресурси `sql`, `BookingDb`, `booking-api`
   мають бути у статусі Running.
5. URL API візьміть у Dashboard (ресурс `booking-api`). Swagger: `<url>/swagger`.

Рядок підключення (`ConnectionStrings:BookingDb`) Aspire передає в API автоматично.
У `appsettings.json` проєкту Api він заданий для запуску без Aspire і перевизначається змінною
оточення `ConnectionStrings__BookingDb`.

### 12.4. Ендпоінти

| Метод і шлях | Опис | Статуси |
|---|---|---|
| GET, POST `/api/customers` | список, створення клієнта | 200, 201, 400, 409 |
| GET, PUT, DELETE `/api/customers/{id}` | читання, оновлення, видалення | 200, 204, 400, 404, 409 |
| GET, POST `/api/departures` | список, створення виїзду | 200, 201, 400, 409 |
| GET, PUT, DELETE `/api/departures/{id}` | читання, оновлення, видалення | 200, 204, 400, 404 |
| GET `/api/bookings/{id}` | бронювання з позиціями | 200, 404 |
| POST `/api/bookings` | створення бронювання (транзакція) | 201, 400, 404, 409 |
| POST `/api/bookings/{id}/confirm` | підтвердження місць (транзакція) | 204, 404, 409 |
| POST `/api/bookings/{id}/pay` | оплата | 200, 400, 404, 409 |
| POST `/api/bookings/{id}/cancel` | скасування | 204, 404, 409 |

### 12.5. Обробка помилок

Помилки повертаються у форматі ProblemDetails (RFC 9457) одним глобальним обробником
(`IExceptionHandler`), контролери не містять `try/catch`.

| Джерело | Виняток | HTTP |
|---|---|---|
| THROW 50001 (не знайдено) | `NotFoundException` | 404 |
| THROW 50002 (недопустимий перехід статусу), 50003 (немає місць), 50007 | `BusinessConflictException` | 409 |
| UNIQUE (2601, 2627), FK/CHECK (547) | `BusinessConflictException` | 409 |
| THROW 50005, 50006 (некоректні параметри) | `ValidationException` | 400 |
| DataAnnotations на DTO | `ValidationProblemDetails` | 400 |

### 12.6. Транзакції та рівень ізоляції

Unit of Work відкриває одне з'єднання й одну транзакцію на операцію; усі репозиторії
працюють через них. Транзакційні сценарії:

- `POST /api/bookings`: перевірка клієнта, зменшення вільних місць виїзду, створення
  бронювання з деталями, позицій (знімок назви маршруту і ціни) та запису в історію статусів;
- `POST /api/bookings/{id}/confirm`: послідовна зміна статусу через процедуру
  `usp_ChangeBookingStatus`, допустимість переходу перевіряє процедура.

Рівень ізоляції за замовчуванням: `ReadCommitted`, таймаут команд 30 с (за замовчуванням SqlClient).
Нестача місць захищена умовним `UPDATE ... WHERE SeatsAvailable >= @Seats`, а процедури беруть `UPDLOCK`.
Вищий рівень ізоляції (наприклад, Serializable) дає менше аномалій читання, але більше блокувань
і ризик взаємних блокувань (deadlock); `ReadCommitted` обрано як баланс між узгодженістю й конкурентністю.
Якщо одна з операцій транзакції завершується помилкою, усі зміни відкочуються.

### 12.7. Безпека запитів

Усі значення передаються в запити й процедури лише як параметри (SqlParameter / Dapper).
Назва таблиці в `BaseDapperRepository<T>` є константою з коду, а не вхідними даними.
Помилки SQL Server у Dal перетворюються на доменні винятки, текст SQL не потрапляє у відповідь API.

### 12.8. Приклади запитів

```bash
# створити клієнта (апостроф у імені не ламає запит)
curl -X POST <url>/api/customers -H "Content-Type: application/json" \
  -d '{"email":"obrien@example.com","fullName":"Patrick O'\''Brien","phone":"+380501234567"}'

# список виїздів (візьміть id з відповіді)
curl <url>/api/departures

# створити бронювання (транзакція)
curl -X POST <url>/api/bookings -H "Content-Type: application/json" \
  -d '{"customerId":1,"contactPhone":"+380501234567","meetingPoint":"Rakhiv","items":[{"departureId":1,"seats":2}]}'

# підтвердити бронювання: 204
curl -X POST <url>/api/bookings/1/confirm

# повторно підтвердити: 409 (ProblemDetails)
curl -X POST <url>/api/bookings/1/confirm

# неіснуюче бронювання: 404 (ProblemDetails)
curl <url>/api/bookings/99999
```

### 12.9. Тести

Unit-тести BLL (мокається Unit of Work) і перевірка конфігурації AutoMapper:

```bash
dotnet test Services/Booking/Carpathians.Booking.Tests
```

## Проєкт №2 — Catalog (EF Core)

Сервіс каталогу туристичних маршрутів, географічних точок, регіонів Карпат, необхідного спорядження та фотогалереї. Побудований за трирівневою архітектурою згідно з Clean Architecture та DDD-практиками на платформі **.NET 8** та **Entity Framework Core 8**.

---

### 1. Опис домену

Доменна модель моделює гірські туристичні маршрути в Українських Карпатах:
- **`Region` (Регіон)**: географічний масив (Чорногора, Горгани, Мармароси тощо).
- **`TrailRoute` (Туристичний маршрут)**: агрегатний корінь маршруту з характеристиками (складність, дистанція, тривалість, набір висоти), бізнес-правилами, м'яким видаленням (`IsDeleted`, `DeletedAt`, `DeletedBy`).
- **`RouteDetail` (Деталі маршруту)**: зв'язок 1:1 (опис, сезон, максимальна висота над рівнем моря, початкова точка, особливі вимоги).
- **`MapPoint` (Географічна точка)**: вершини, полонини, притулки, джерела з координатами (WGS84).
- **`RoutePoint` (Точка на маршруті)**: зв'язок M:N між маршрутами та точками через явну проміжну сутність зі складеним ключем `(RouteId, PointId)` та порядковим номером проходження `SequenceNo`.
- **`GearItem` (Спорядження)**: зв'язок 1:N до маршруту, список обов'язкового/рекомендованого спорядження.
- **`GalleryImage` (Фотогалерея)**: зв'язок 1:N до географічної точки, фотографії з порядком сортування.

---

### 2. Системні вимоги

- **.NET 8 SDK** (версія 8.0.400+)
- **SQL Server 2022** (або Docker-контейнер `mcr.microsoft.com/mssql/server:2022-latest`)
- **Docker / Docker Compose** (опціонально для контейнеризації)
- **.NET Aspire Workload** (для запуску через AppHost)

---

### 3. Запуск проєкту

#### Варіант А: Локальний запуск (Kestrel + Docker SQL Server)
1. Запустіть контейнер бази даних:
   ```bash
   docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=YourStrong!Passw0rd" -p 1434:1433 --name sql-catalog -d mcr.microsoft.com/mssql/server:2022-latest
   ```
2. Перейдіть до теки `Services/Catalog` та виконайте запуск:
   ```bash
   cd Services/Catalog/Carpathians.Catalog.Api
   dotnet run
   ```
   API слухає на `http://localhost:5231`.
   Swagger UI доступний за адресою: `http://localhost:5231/swagger`.

#### Варіант Б: Запуск через .NET Aspire AppHost
```bash
cd Services/Catalog/Carpathians.Catalog.AppHost
dotnet run
```

#### Варіант В: Збірка Docker-образу
```bash
# з кореня репозиторію (сервіс посилається на Platform.ServiceDefaults і Protos/)
docker build -t carpathians-catalog-api -f Services/Catalog/Carpathians.Catalog.Api/Dockerfile .
docker run -p 8080:8080 -e ConnectionStrings__CatalogDb="Server=host.docker.internal,1434;Database=CatalogContext;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True" carpathians-catalog-api
```

---

### 4. Застосування міграцій EF Core

У проєкті використовується `dotnet-ef`. Команди виконуються з явно вказаними проектами:

- **Створення нової міграції**:
  ```bash
  dotnet ef migrations add <MigrationName> --project Services/Catalog/Carpathians.Catalog.Dal --startup-project Services/Catalog/Carpathians.Catalog.Api
  ```
- **Застосування міграцій до бази даних**:
  ```bash
  dotnet ef database update --project Services/Catalog/Carpathians.Catalog.Dal --startup-project Services/Catalog/Carpathians.Catalog.Api
  ```
- **Генерація ідемпотентного SQL-скрипта міграції**:
  ```bash
  dotnet ef migrations script --idempotent --project Services/Catalog/Carpathians.Catalog.Dal --startup-project Services/Catalog/Carpathians.Catalog.Api -o migrations.sql
  ```

> **Примітка:** У середовищах розробки (`!env.IsProduction()`) міграції та сідінг автоматично виконуються під час старту застосунку в `Program.cs`.

---

### 5. Ідемпотентний сідінг даних

1. **Статичний сідінг (HasData)**: фіксовані системні довідники регіонів з фіксованими `Id` у `RegionConfiguration.cs`.
2. **Програмний сідінг (`DbInitializer.SeedAsync`)**:
   - Перевіряє наявність записів через `.AnyAsync()` перед вставкою, запобігаючи дублюванню.
   - Дотримується порядку залежностей: Регіони -> Точки -> Маршрути -> Деталі -> Спорядження -> RoutePoints -> Фотогалерея.
   - Виконується лише у середовищі розробки (`!env.IsProduction()`).
3. **SQL-скрипти**:
   - Схема: `db/p2/schema.sql`
   - Дані: `db/p2/seed.sql`

---

### 6. Пагінація, фільтрація та сортування

#### Параметри запиту (`GET /api/routes`)
| Параметр | Тип | За замовчуванням | Опис |
| :--- | :--- | :--- | :--- |
| `page` | `int` | `1` | Номер сторінки (мінімум 1) |
| `pageSize` | `int` | `20` | Кількість елементів на сторінку (1..100) |
| `search` | `string` | `null` | Пошук за назвою або slug |
| `regionId` | `int` | `null` | Фільтр за ідентифікатором регіону |
| `difficulty` | `enum` | `null` | Фільтр за складністю (`0 = Easy`, `1 = Moderate`, `2 = Hard`, `3 = Extreme`) |
| `maxDistanceKm` | `decimal` | `null` | Фільтр за максимальною відстанню маршруту |
| `sortBy` | `string` | `"created"` | Поле сортування: `name`, `distance`, `elevation`, `difficulty`, `created` |
| `sortDir` | `string` | `"desc"` | Напрямок: `asc` або `desc` |

#### Заголовки відповіді (Headers)
- `X-Total-Count`: загальна кількість знайдених записів
- `X-Page`: поточна сторінка
- `X-Page-Size`: розмір сторінки
- `X-Total-Pages`: загальна кількість сторінок

#### Тіло відповіді (`PagedResult<T>`)
```json
{
  "items": [ ... ],
  "totalCount": 42,
  "page": 1,
  "pageSize": 20,
  "totalPages": 3,
  "hasPrevious": false,
  "hasNext": true
}
```

---

### 7. Specification Pattern (Ardalis.Specification)

Фільтрація та вибірка маршрутів винесена у специфікації:
- **`RouteFilterPaginatedSpec`**: інкапсулює фільтри (Where), завантаження зв'язків (Include Region, Detail), сортування (OrderBy) та пагінацію (Skip/Take).
- **`RouteFilterCountSpec`**: інкапсулює ті самі критерії фільтрації без Skip/Take для точного підрахунку `totalCount`.
- **`PopularChallengingRoutesSpec`**: альтернативна доменна специфікація для складних маршрутів (`Difficulty >= Moderate`, `ElevationGain >= 500m`), що виконується через той самий метод `ListAsync(spec)`.

---

### 8. Таблиця HTTP-кодів відповідей

Усі помилки повертаються у стандартному форматі **RFC 7807 / RFC 9457 `ProblemDetails`** або `ValidationProblemDetails`.

| Код | Назва | Сценарій використання |
| :---: | :--- | :--- |
| **200** | OK | Успішне отримання ресурсу або списку (GET, PUT) |
| **201** | Created | Успішне створення сутності (POST), містить заголовок `Location` |
| **204** | NoContent | Успішне видалення сутності (DELETE) |
| **400** | BadRequest | Помилка валідації (DataAnnotations, FluentValidation), повертає `ValidationProblemDetails` зі списком полів |
| **404** | NotFound | Сутність з вказаним ID не знайдена (`EntityNotFoundException`) |
| **409** | Conflict | Порушення унікальності (`ConflictException`) або бізнес-правил домену (`DomainException`) |
| **500** | InternalServerError | Непередбачена системна помилка (у Production повертає безпечне повідомлення) |

---

### 9. Налаштування Serilog та структуровані логи

- **Бібліотека**: `Serilog.AspNetCore`, `Serilog.Sinks.File`.
- **Логування SQL**: `Microsoft.EntityFrameworkCore.Database.Command` налаштовано на рівень `Information` для відстеження кількості SQL-запитів.
- **Enrichment**:
  - `CorrelationId`: автоматично передається або генерується через заголовок `X-Correlation-Id` за допомогою `CorrelationIdMiddleware`.
  - `RequestPath`: поточний шлях запиту.
  - `UserId`: користувач запиту.
- **Запобігання дублюванню**: винятки обробляються та логуються виключно в `DomainExceptionHandler`. `app.UseSerilogRequestLogging()` забезпечує єдиний компактний HTTP summary-рядок для кожного запиту.
- **Структуровані параметри**: шаблони повідомлень формуються без string interpolation (наприклад, `_logger.LogInformation("Retrieving route with ID {Id}", id)`).

---

### 10. AutoMapper LicenseKey

Починаючи з AutoMapper 14.x+, для некомерційної розробки або ліцензійного використання потрібне встановлення ліцензійного ключа.
- **Де взяти**: на офіційному сайті [Lucky Penny Software](https://luckypennysoftware.com) або з ліцензійного договору компанії.
- **Конфігурація**: ключ передається через секцію `AutoMapper:LicenseKey` в `appsettings.json` або змінну середовища `AutoMapper__LicenseKey`:
  ```json
  "AutoMapper": {
    "LicenseKey": "your-license-key-here"
  }
  ```
  Для локальної розробки без ліцензійного ключа бібліотека виводить попередження в консоль, дозволяючи повноцінне виконання мапінгів.

## Проєкт №3 — Reviews (Clean Architecture + CQRS + MongoDB)

Сервіс відгуків, коментарів і обговорень маршрутів на базі `ReviewContext` (MongoDB). Побудований за
**Clean Architecture** з чотирма шарами (Domain, Application, Infrastructure, WebApi), принципом **CQRS**
і патерном **Mediator** (MediatR) на **.NET 8**. Повний опис — [`Services/Reviews/README.md`](Services/Reviews/README.md).

### Коротко

- **Domain** не залежить від жодного шару: сутності `Review`, `Discussion`, `DiscussionMessage` з інкапсуляцією
  та доменними винятками; Value Objects `Author`, `GeoPoint`, `WeatherInfo`, `MessagePreview`.
- **Application**: кожна операція — команда або запит MediatR; Pipeline Behaviors (логування, винятки,
  FluentValidation, продуктивність); читання — через `IReviewQueries` / `IDiscussionQueries`.
- **Infrastructure**: `BsonClassMap` без атрибутів у домені, структура документів як у Лаб. 01;
  оптимістична конкурентність за полем `version`; проєкції й aggregation pipeline; автоматичні індекси
  та ідемпотентний сідінг.
- **WebApi**: тонкі контролери через `ISender`, `ETag` / `If-Match`, ProblemDetails з MongoDB-винятками
  (DuplicateKey → 409, недоступна БД → 503), Serilog, Swagger, `/health`.

### Запуск

```bash
docker run -d --name mongo-carpathians -p 27017:27017 mongo:7
cd Services/Reviews/Carpathians.Reviews.WebApi
dotnet run
```

Swagger UI: `http://localhost:5203/swagger`. Індекси та початкові дані створюються автоматично під час старту.

## Проєкт №5 — Платформа на .NET Aspire

Сервіси з Лаб. 02–04 інтегровано в єдину платформу (теки `Platform.*` у корені): **AppHost** запускає контейнери
SQL Server і MongoDB, три сервіси, Aggregator і Gateway однією командою; **ServiceDefaults** дає всім
сервісам однакове структуроване логування (Serilog → Aspire Dashboard), трасування OpenTelemetry
(HTTP, SQL Server, MongoDB) і наскрізний `X-Correlation-Id`; **API Gateway** на YARP — єдина точка
входу `http://localhost:5000`; **Aggregator** паралельно збирає картку бронювання з трьох сервісів
і повертає часткову відповідь, якщо один із них недоступний. Повний опис —
[`docs/platform.md`](docs/platform.md).

```powershell
dotnet user-secrets set "Parameters:sql-password" "YourStrong!Passw0rd" --project Platform.AppHost
dotnet run --project Platform.AppHost
```

## Проєкт №6 — gRPC-виклики та кешування

Платформу з Лаб. 05 розширено оптимізацією продуктивності та внутрішньою комунікацією через gRPC:

- **Redis** запускається через AppHost (`AddRedis("redis").WithDataVolume()`) і підключений до всіх сервісів
  та Aggregator через Aspire-інтеграцію (`AddRedisDistributedCache("redis")`).
- **Дворівневий кеш** у кожному мікросервісі: `IMemoryCache` (L1, з `SizeLimit` і політиками витіснення) →
  Redis (L2, спільний для всіх екземплярів) → база даних; інвалідація синхронно з операцією запису;
  недоступний Redis не валить сервіс. Найповніше — у Каталозі (картки маршрутів, довідник регіонів, прогрівання).
- **gRPC-контракти** (`booking.proto`, `catalog.proto`, `reviews.proto`) і gRPC-сервіси з перетворенням доменних
  винятків на `RpcException` (`NOT_FOUND`, `INVALID_ARGUMENT`, ...).
- **Aggregator на gRPC**: типізовані gRPC-клієнти з service discovery, паралельні виклики `Task.WhenAll`,
  часткові відмови, кеш агрегованих відповідей у Redis (30 с).
- **Обсервабіліті**: gRPC-spans клієнта й сервера в одному trace, структуровані логи gRPC і кешу,
  метрики `cache.hits` / `cache.misses` / `cache.hit_ratio`, CorrelationId у метаданих gRPC-викликів;
  ендпоінт порівняння затримок HTTP і gRPC.

Повний опис — [`docs/platform.md`](docs/platform.md#8-grpc-і-кешування-лаб-06). Запуск — так само, як у Проєкті №5.

## Проєкт №7 — Resilience, бізнес-метрики та health checks

Платформу з Лаб. 05–06 розширено механізмами стійкості й спостережуваності:

- **Resilience** для всіх HTTP- і gRPC-клієнтів Aggregator. Standard Resilience Handler з ServiceDefaults
  у кожного клієнта замінено політикою сервісу (`RemoveAllResilienceHandlers` → `AddStandardResilienceHandler`),
  тож обробники не нашаровуються. Додатково gRPC-клієнти мають retry-політику каналу за gRPC-статусами.
- **Graceful degradation кешу.** Якщо Redis недоступний, сервіс читає з L1 і бази даних, пише попередження в лог,
  а health check показує `Degraded`.
- **Бізнес-метрики** через `IMeterFactory`: лічильники, гістограми тривалості й gauge у кожному сервісі та в Aggregator.
- **Health checks** для SQL Server, MongoDB, Redis (`Degraded`) і gRPC-залежностей. Liveness, readiness і startup
  розділено, відповіді містять JSON з деталями кожної перевірки. Dashboard показує стан ресурсів через `WithHttpHealthCheck`.

| Що | Де |
|---|---|
| Політики клієнтів | `Platform.Aggregator/Resilience/DownstreamPolicies.cs`, `DownstreamResilience.cs` |
| Метрики Aggregator, стан circuit breaker | `Platform.Aggregator/Metrics/` |
| Health checks залежностей Aggregator | `Platform.Aggregator/HealthChecks/` |
| Бізнес-метрики сервісів | `Services/*/*.Api/Metrics/`, `Services/Reviews/Carpathians.Reviews.WebApi/Metrics/` |
| Health checks сервісів | `Services/*/*.Api/HealthChecks/`, `Services/Reviews/Carpathians.Reviews.WebApi/HealthChecks/` |
| Meter, теги, кошики гістограм, health endpoints | `Platform.ServiceDefaults/Extensions.cs`, `Metrics/`, `Health/` |
| Імітація збоїв `/dev/faults` (Development) | `Platform.ServiceDefaults/Resilience/FaultInjection.cs` |

### 1. Політики resilience клієнтів Aggregator

Критичні сервіси — **Каталог** і **Бронювання**: без них картку бронювання не зібрати, Aggregator відповідає 503.
Для них важливіше дочекатися відповіді, тож повторів більше, а таймаути довші. Circuit breaker розмикається вже
при 30 % збоїв, щоб швидко зняти навантаження з сервісу, якому погано.

**Відгуки** — некритичний сервіс: без рейтингу й відгуків Aggregator повертає часткову відповідь (`warnings`).
Чекати на нього довго немає сенсу, тож політика коротка: один повтор і малі таймаути. Ланцюг розмикається лише
при систематичних збоях (70 %), але потім довше лишається розімкненим. Поки сервіс лежить, Aggregator миттєво
віддає часткову відповідь і не витрачає час на таймаути.

| Параметр | catalog-api | booking-api | reviews-api | Обґрунтування |
|---|---|---|---|---|
| Критичність | так | так | ні | без каталогу й бронювання відповідь неможлива; без відгуків — часткова |
| Retry (HTTP-рівень) | 3 повтори | 2 повтори | 1 повтор | критичним — більше шансів переждати тимчасовий збій |
| Затримка retry | 200 мс, експон., jitter | 200 мс, експон., jitter | 100 мс, експон., jitter | jitter розносить повтори клієнтів у часі (thundering herd) |
| Таймаут спроби | 2 с | 2 с | 1 с | типова відповідь — десятки мс; повільніше вважаємо збоєм |
| Загальний таймаут | 10 с | 8 с | 3 с | ≥ найгіршого часу всіх спроб: 9.4 с / 6.6 с / 2.1 с (перевіряє `Validate()`) |
| CB: частка збоїв | 30 % | 30 % | 70 % | критичні — швидкий fail-fast; відгуки — лише при систематичних збоях |
| CB: мін. запитів / вікно | 10 / 30 с | 10 / 30 с | 10 / 20 с | не розмикатися через 1–2 випадкові помилки при малому трафіку |
| CB: розімкнений | 15 с | 15 с | 30 с | критичні перевіряємо частіше; відгуки довше не турбуємо |
| gRPC retry: спроб | 4 | 3 | 2 | за статусами `UNAVAILABLE`, `RESOURCE_EXHAUSTED` |
| gRPC backoff | 0.2–2 с | 0.2–2 с | 0.1–0.5 с | `RetryThrottling` (10 токенів, 0.1) вимикає повтори при масових збоях |
| Deadline gRPC-виклику | 10 с | 8 с | 3 с | = загальний таймаут: повтори обох рівнів не тривають довше |
| Повтор POST (HTTP) | ні | ні | ні | `DisableForUnsafeHttpMethods`: зміни не дублюються; gRPC-виклики Aggregator лише читають |

**Чому два рівні повторів для gRPC.** gRPC повертає `UNAVAILABLE` чи `DEADLINE_EXCEEDED` статусом у трейлерах
при HTTP 200, тому HTTP-обробник їх не бачить. Він повторює лише мережеві збої, 5xx і таймаути, а повтори
за gRPC-статусами робить retry-політика каналу (`ServiceConfig.MethodConfigs`).

**Логи переходів circuit breaker.** `OnOpened` — Error, `OnHalfOpened` — Warning, `OnClosed` — Information,
категорія `Carpathians.Aggregator.Resilience`. Повтори й таймаути Polly логує сам (категорія `Polly`).

### 2. Бізнес-метрики

Для кожного сервісу Meter створюється через `IMeterFactory` в окремому класі метрик (Singleton). Назва Meter
збігається з назвою сервісу, і всі вони додані в OpenTelemetry через `AddMeter` у ServiceDefaults.
Тривалість записується для **всіх** виконань, включно з невдалими. Лічильник збільшується одразу після успішної операції.
Gauge читає знімок стану, який фоновий сервіс оновлює раз на 30 с. Callback gauge не звертається до бази.

| Meter | Інструмент | Тип | Теги |
|---|---|---|---|
| `Booking.Api` | `bookings.created` | Counter | `booking.status` |
| | `bookings.confirmed`, `bookings.cancelled` | Counter | — |
| | `bookings.paid` | Counter | `payment.method` (Card / Cash / Transfer) |
| | `bookings.seats.reserved`, `bookings.amount` (UAH) | Counter | — |
| | `bookings.operation.duration` (мс) | Histogram | `operation`, `status`, `error.type` |
| | `bookings.current` | ObservableGauge | `booking.status` |
| | `departures.seats.available` | ObservableGauge | — |
| `Catalog.Api` | `routes.created`, `routes.updated` | Counter | `route.difficulty` |
| | `routes.deleted` | Counter | — |
| | `routes.searches` | Counter | `search.type` (all / paged / popular_challenging) |
| | `points.created` | Counter | `point.type` |
| | `catalog.operation.duration` (мс) | Histogram | `operation`, `status`, `error.type` |
| | `routes.active` / `points.active` | ObservableGauge | `route.difficulty` / `point.type` |
| `Reviews.Api` | `reviews.created` | Counter | `review.rating` (1–5) |
| | `reviews.updated`, `reviews.deleted`, `reviews.comments.added`, `discussions.created`, `discussions.messages.posted` | Counter | — |
| | `reviews.operation.duration` (мс) | Histogram | `operation` (команда/запит CQRS), `operation.kind`, `status`, `error.type` |
| | `reviews.current` / `discussions.current` | ObservableGauge | `review.rating` / — |
| `Aggregator.Api` | `aggregator.requests` | Counter | `operation`, `aggregation.result`, `cache.result`, `status` |
| | `aggregator.request.duration` (мс) | Histogram | ті самі |
| | `aggregator.downstream.failures` | Counter | `downstream.service`, `rpc.grpc.status` |
| | `aggregator.circuit.state` | ObservableGauge | `downstream.service`, `transport` (0 closed / 1 half-open / 2 open) |

**Теги однакові в усіх сервісах:**
- `operation` — фіксований перелік операцій сервісу;
- `status` — `success` / `failure`;
- `error.type` — `not_found` / `conflict` / `validation` / `business_rule` / `cancelled` / `unavailable` / `error`.

Ідентифікаторів, часових міток і довільного тексту в тегах немає: кожна комбінація тегів — окремий часовий ряд,
тож кількість рядів обмежена переліками. Межі кошиків гістограм `*.duration` (5 мс … 10 с) задано через
OpenTelemetry View, щоб розрізняти повтори й таймаути політик.

### 3. Health checks

| Endpoint | Що перевіряє | Для чого |
|---|---|---|
| `/health/live` (і `/alive`) | лише процес (`self`, тег `live`) | liveness: збій → перезапуск; < 1 мс, без зовнішніх систем |
| `/health/ready` | БД, Redis, gRPC-залежності (тег `ready`) | readiness: збій → виключення з балансування |
| `/health/startup` | запуск завершено (тег `startup`) | startup probe для повільного старту (міграції, сидинг) |
| `/health` | усі перевірки | `WithHttpHealthCheck` в AppHost → стан ресурсу в Dashboard |
| `/health/cache` | статистика кешу (Лаб. 06) | діагностика L1/L2 |

Коди відповіді: `Healthy` і `Degraded` → 200, `Unhealthy` → 503. Тіло — JSON зі статусом, описом,
тривалістю, тегами, помилкою й даними кожної перевірки.

| Сервіс | Перевірка | Статус збою | Таймаут | Теги |
|---|---|---|---|---|
| booking-api | `booking-db` (SQL Server, `SELECT 1`) | Unhealthy | 5 с | database, sql, ready |
| catalog-api | `catalog-db` (SQL Server, `SELECT 1`) | Unhealthy | 5 с | database, sql, ready |
| reviews-api | `reviews-db` (MongoDB `ping`) | Unhealthy | 5 с | database, mongodb, ready |
| усі сервіси й Aggregator | `redis-cache` (Redis `PING`) | **Degraded** — кеш необов'язковий | 3 с | cache, redis, ready |
| aggregator | `catalog-api`, `booking-api` (gRPC `Health/Check`, deadline 2 с) | Unhealthy (критичні) | 3 с | grpc, downstream, ready |
| aggregator | `reviews-api` (gRPC `Health/Check`, deadline 2 с) | Degraded (некритичний) | 3 с | grpc, downstream, ready |

Мікросервіси відповідають за стандартним **gRPC Health Checking Protocol** (`AddGrpcHealthChecks`,
`MapGrpcHealthChecksService`). Відповідь будується на тих самих readiness-перевірках: недоступна БД → `NOT_SERVING`,
недоступний Redis → `SERVING`. Aggregator опитує сервіси власною перевіркою `DownstreamGrpcHealthCheck`.
Її клієнт без resilience-обробника, тож перевірка не повторюється й не впливає на circuit breaker робочих клієнтів.
У даних перевірки видно поточний стан circuit breaker сервісу.

### 4. Моніторинг в Aspire Dashboard

- **Resources** — стан кожного ресурсу (Healthy / Degraded / Unhealthy). Клік на ресурс показує деталі
  health checks. `WaitFor` чекає, поки залежність стане здоровою.
- **Metrics** — ресурс → Meter:
  - `Booking.Api`, `Catalog.Api`, `Reviews.Api`, `Aggregator.Api` — бізнес-метрики (фільтри за тегами, напр.
    `bookings.paid` за `payment.method`, `bookings.operation.duration` за `status`);
  - `Polly` — `resilience.polly.strategy.events`, фільтр `event.name` = `OnRetry` / `OnCircuitOpened` / `OnTimeout`;
    `resilience.polly.pipeline.duration` за `pipeline.name`;
  - `Carpathians.Cache` — `cache.hits`, `cache.misses`, `cache.distributed.available`.
- **Structured logs** — фільтр за ресурсом і рівнем:
  - `Carpathians.Aggregator.Resilience` — переходи circuit breaker;
  - `Polly` — повтори й таймаути;
  - `CorrelationId` — наскрізний пошук запиту.
- **Traces** — паралельні gRPC-виклики Aggregator, повтори видно як кілька spans спроб.
  Службові health-запити не трасуються.

**Критичні сигнали:**
- зростання `OnRetry` і `aggregator.downstream.failures` для одного сервісу — привід розслідувати сам сервіс;
- `aggregator.circuit.state = 2` — ланцюг розімкнено;
- `aggregation.result = partial` — Aggregator працює в деградованому режимі;
- `cache.distributed.available = 0` — Redis недоступний;
- p95 `*.operation.duration` — затримка ключових операцій.

### 5. Сценарії перевірки

`/dev/faults` (лише Development) вмикає штучні збої в сервісі без перезапуску. Health endpoints збоям не піддаються.

```powershell
# 1. Тимчасовий збій reviews-api: зупиніть reviews-api у Dashboard на 5–10 с і запросіть картку
curl.exe -i http://localhost:5000/api/aggregator/bookings/1/full
#    -> 200 з warnings ["reviews-api is unavailable"]; Metrics: OnRetry; aggregation.result = partial

# 2. Circuit breaker: усі запити reviews-api відповідають 503
curl.exe -k -X POST "https://localhost:7203/dev/faults?failureRate=1"
1..15 | % { curl.exe -s -o NUL -w "%{http_code} %{time_total}s`n" http://localhost:5000/api/aggregator/routes/1/overview }
#    -> лог Error "Circuit breaker for reviews-api (Grpc) opened"; наступні відповіді миттєві;
#       aggregator.circuit.state{downstream.service=reviews-api} = 2; через 30 с — HalfOpened -> Closed
curl.exe -k -X DELETE https://localhost:7203/dev/faults

# 3. Таймаут спроби: затримка 3 с > таймауту спроби 1 с
curl.exe -k -X POST "https://localhost:7203/dev/faults?failureRate=0&delayMs=3000"
#    -> OnTimeout у метриках Polly; часткова відповідь у межах загального таймауту reviews-api (3 с), а не 30 с

# 4. Redis: зупиніть redis у Dashboard
curl.exe -k https://localhost:7047/health/ready   # -> "redis-cache": Degraded, HTTP 200; API відповідає далі

# 5. SQL Server: зупиніть sql у Dashboard
curl.exe -k https://localhost:7047/health/live    # -> 200 Healthy
curl.exe -k https://localhost:7047/health/ready   # -> 503, "booking-db": Unhealthy

# 6. Бізнес-метрики: створіть кілька бронювань (Services/Booking/.../Carpathians.Booking.Api.http)
#    -> Metrics: bookings.created за booking.status, розподіл bookings.operation.duration
```

Порти сервісів (профіль `https`): booking-api `7047`, catalog-api `7052`, reviews-api `7203`, aggregator `5110`,
gateway `5000`. Точні адреси показує Dashboard → Resources.

## Проєкт №8 — Асинхронна подієва інтеграція (RabbitMQ)

Каталог — власник маршрутів. Політика дублювання (розділ 9) вимагає оновлювати копії назви маршруту подіями:
`RouteRefs` у БД Бронювання і `routeName` у відгуках та обговореннях. Каталог публікує integration event
у RabbitMQ, а Бронювання й Відгуки отримують його у власних чергах і оновлюють свої копії (eventual consistency).

| Що | Де |
|---|---|
| RabbitMQ (volume, Management UI на `15672`) | `Platform.AppHost/AppHost.cs` |
| Підключення (`Aspire.RabbitMQ.Client.v7`, `RabbitMQ.Client` 7.x), health check | `Platform.EventBus/EventBusExtensions.cs` |
| Топологія: exchanges, черги, bindings, DLX/DLQ, черги повторів | `Platform.EventBus/Topology/` |
| Базова подія, JSON, заголовки | `Platform.EventBus/Events/`, `Platform.EventBus/Messaging/` |
| Publisher (`IEventPublisher`, `RabbitMqEventPublisher`) | `Platform.EventBus/Publishing/` |
| Consumer (`BackgroundService`, ack/nack, retry, DLQ) | `Platform.EventBus/Consuming/` |
| Трасування через брокер, метрики | `Platform.EventBus/Diagnostics/`, `Platform.ServiceDefaults/Extensions.cs` |
| Події Каталогу, outbox, фонова публікація | `Services/Catalog/Carpathians.Catalog.Api/IntegrationEvents/`, `Carpathians.Catalog.Dal/Outbox/` |
| Публікація після збереження | `Services/Catalog/Carpathians.Catalog.Bll/Services/RouteService.cs` |
| Обробники Бронювання, `ProcessedEvents` | `Services/Booking/Carpathians.Booking.Api/IntegrationEvents/`, `Carpathians.Booking.Dal/Migrations/` |
| Обробник Відгуків, `processedEvents` | `Services/Reviews/Carpathians.Reviews.WebApi/IntegrationEvents/`, `Carpathians.Reviews.Infrastructure/Persistence/IntegrationEventStore.cs` |

### 1. Топологія

```
                                  route.*         ┌─> booking.catalog-sync ──nack──┐
Catalog ──publish──> catalog-events (topic) ──────┤        ▲                       │
                                  route.renamed   └─> reviews.catalog-sync ─nack─┐ │
                                                           ▲                     │ │
                                                  *.retry (TTL 5 с)              ▼ ▼
                                                                    catalog-events.dlx (direct)
                                                                     ├─> booking.catalog-sync.dlq
                                                                     └─> reviews.catalog-sync.dlq
```

- `catalog-events` — durable topic exchange. Routing keys: `route.created`, `route.renamed`, `route.deleted`.
- Кожен сервіс-споживач має власну durable-чергу (fan-out між сервісами). Кілька екземплярів одного сервісу
  читають ту саму чергу — competing consumers.
  - `booking.catalog-sync` має binding `route.*` (`*` — рівно одне слово), бо Бронюванню потрібен повний довідник.
  - `reviews.catalog-sync` має binding лише `route.renamed`, бо Відгукам потрібна тільки назва.
- DLX має тип `direct`, ключ дорівнює назві черги-джерела (`x-dead-letter-routing-key`). Відхилене повідомлення
  потрапляє лише в DLQ своєї черги. З `fanout` воно опинилося б у DLQ усіх споживачів.
- Черга `<черга>.retry` не має споживачів. Через `x-message-ttl` (5 с) RabbitMQ повертає повідомлення в основну
  чергу, тож між повторами є затримка без блокування споживача.
- Топологію оголошують у коді під час старту: publisher оголошує свій exchange, кожен consumer — свої черги.
  Повторне оголошення безпечне. Параметри черг через Management UI не змінюються, інакше буде `PRECONDITION_FAILED`.

### 2. Події та публікація

| Подія | Routing key | Дані | Споживачі |
|---|---|---|---|
| `RouteCreatedIntegrationEvent` | `route.created` | `routeId`, `name`, `slug`, `regionId`, `difficulty` | Бронювання |
| `RouteRenamedIntegrationEvent` | `route.renamed` | `routeId`, `oldName`, `newName` | Бронювання, Відгуки |
| `RouteDeletedIntegrationEvent` | `route.deleted` | `routeId`, `name` | Бронювання |

- Усі події — незмінні `record` з базовим `IntegrationEvent`: `EventId` (ключ ідемпотентності), `OccurredAt`,
  `CorrelationId`. Кожна подія самодостатня, споживач не звертається до Каталогу по додаткові дані.
- Кожен сервіс має власну копію record-а, як в eShop. Контракт — JSON, а не спільна збірка.
- Формат: System.Text.Json, camelCase, enum рядком, дати ISO 8601 UTC. Невідомі поля ігноруються.
  Сумісні зміни схеми лише додають поля. Несумісна зміна — нова версія (`x-event-version`).
- Метадані повідомлення:
  - `Persistent`, `ContentType = application/json`, `MessageId = EventId`, `Type` = тип події, `Timestamp`;
  - заголовки `x-correlation-id`, `x-event-version`, `x-source`, `traceparent`.
- Канал публікації працює з publisher confirms: подія вважається опублікованою після підтвердження брокера.

**Публікація лише після збереження + Transactional Outbox.** `RouteService` створює, перейменовує й видаляє
маршрут так:

1. Зміни маршруту і запис події в таблицю `IntegrationEventLog` виконуються в одній транзакції БД.
2. Після коміту подія публікується, запис отримує стан `Published`.
3. Транзакція не вдалася — немає ні змін, ні події.
4. Брокер недоступний або сервіс упав після коміту — подія лишається в outbox. `CatalogOutboxPublisher`
   кожні 10 с допублікує її і продовжить trace початкового запиту.

Зміна лише дистанції чи складності подію не породжує: копії тримають тільки назву.

### 3. Споживання, ідемпотентність, реплікація

`RabbitMqEventConsumer` — це `BackgroundService` на кожну чергу. Він працює так:

- створює канал, оголошує топологію, ставить `BasicQos(prefetch: 10)`;
- підписується через `AsyncEventingBasicConsumer` і `BasicConsumeAsync(autoAck: false)`;
- за `stoppingToken` скасовує споживача й закриває канал.

Обробник обирається за `BasicProperties.Type`. Кожен тип події має окремий клас-обробник, він створюється
в DI-scope повідомлення.

| Результат обробки | Дія |
|---|---|
| Успіх | `BasicAckAsync` |
| Тимчасовий збій (БД недоступна, таймаут) | копія з `x-retry-count + 1` і `x-last-error` → `<черга>.retry`, ack оригіналу |
| Вичерпано 3 повтори | `BasicNackAsync(requeue: false)` → DLX → DLQ |
| Постійний збій: некоректний JSON, невідомий тип, несумісна версія, некоректні дані | одразу `BasicNackAsync(requeue: false)` → DLQ |

Повторно публікується копія, а не `requeue`, бо при `requeue` повідомлення повертається без змін і лічильник
спроб не збільшити.

**Бронювання (SQL Server, Dapper):**

- В одній транзакції виконується `INSERT dbo.ProcessedEvents (EventId)` і `MERGE dbo.RouteRefs`.
- Порушення PRIMARY KEY (помилка 2627) означає, що подію вже оброблено: відкат і пропуск.
- `RouteRefs.CatalogChangedAt` зберігає час останньої застосованої події. Старша подія, що прийшла пізніше
  (out-of-order), новіші дані не перезапише.
- `route.deleted` не видаляє рядок (на нього посилаються виїзди), а ставить `IsActive = 0`.
- Знімки в оформлених бронюваннях (`BookingItems.RouteName`, `BookingItems.UnitPrice`) не змінюються.
- Таблицю `ProcessedEvents` і нові стовпці `RouteRefs` створює `Dal/Migrations/001_IntegrationEvents.sql`
  під час старту сервісу (ідемпотентно). Також вони є в `db/p1/schema.sql`.

**Відгуки (MongoDB):**

- `RouteRenamedHandler` виконує наявну CQRS-команду `SyncRouteNameCommand`: `UpdateMany` по `reviews.routeName`
  і `discussions.routeName`, інвалідація кешу маршруту.
- `processedEvents` (`_id = EventId`) — ідемпотентність, `routeNameSync` (`_id = routeId`, `changedAt`) — out-of-order.
- MongoDB платформи працює без replica set, тому багатодокументних транзакцій немає. Безпеку забезпечує порядок
  кроків: перевірка `EventId` → оновлення назви (`$set` тим самим значенням нічого не змінює вдруге) → запис
  `EventId`. Збій між кроками призведе лише до повторної обробки з тим самим результатом.

### 4. Трасування і логування

- Publisher створює span `catalog-events publish` (Producer) і записує його контекст у заголовок `traceparent`
  (W3C, пропагатор OpenTelemetry).
- Consumer витягує контекст із заголовків (значення `byte[]`) і створює span `<черга> process` (Consumer) як нащадка.
  Теги: `messaging.system`, `messaging.destination.name`, `messaging.message.id`, `messaging.message.type`.
- ActivitySource і Meter `Platform.Messaging` підключено в ServiceDefaults. Вбудоване трасування клієнта вимкнено
  (`DisableTracing`), щоб spans не дублювалися.
- Один trace: `PUT /api/routes/{id}` → SQL Каталогу → `catalog-events publish` → `booking.catalog-sync process`
  → SQL Бронювання і `reviews.catalog-sync process` → MongoDB.
- Логи обробки й збоїв містять `EventId`, `EventType`, `CorrelationId`, `Queue`, `RetryCount` та ідентифікатори
  маршруту. Винятки логуються з повним stack trace.
- Метрики (Meter `Platform.Messaging`):
  - `messaging.process.messages` з тегом `outcome` (`ack` / `retry` / `dead_letter`);
  - `messaging.process.duration`;
  - `messaging.queue.depth` — глибина основних черг, черг повторів і DLQ.

### 5. Сценарії перевірки

Management UI: `http://localhost:15672`. Логін — `guest`, пароль — параметр `messaging-password`
(Dashboard → Resources → messaging, або user-secrets AppHost). Запити — у `Services/Catalog/Carpathians.Catalog.Api/Carpathians.Catalog.Api.http`.

1. **Топологія.** Management UI → Exchanges: `catalog-events`, `catalog-events.dlx`. Queues:
   `booking.catalog-sync`, `reviews.catalog-sync` та їхні `.retry` і `.dlq`. У черзі → Bindings.
2. **Перейменування.** `PUT https://localhost:7052/api/routes/1` з новою назвою (запит 1 у `.http`).
   За кілька секунд оновлюються копії:
   - `SELECT Id, Name, CatalogChangedAt, IsActive FROM dbo.RouteRefs WHERE Id = 1` (BookingContext) — нова назва;
   - `SELECT TOP 5 * FROM dbo.ProcessedEvents ORDER BY ProcessedAt DESC` — EventId події;
   - `SELECT RouteName, UnitPrice FROM dbo.BookingItems` — без змін (знімки);
   - `GET https://localhost:7203/api/reviews?routeId=1` — нова `routeName` в усіх відгуках.
3. **Дублікат.** `GET /dev/outbox` → `eventId` останньої події, потім `POST /dev/outbox/{eventId}/republish`.
   У Structured logs booking-api і reviews-api з'являється «Duplicate RouteRenamedIntegrationEvent ... skipped»,
   дані не змінюються.
4. **Обробка після перезапуску.** Зупиніть booking-api у Dashboard і перейменуйте маршрут кілька разів.
   Повідомлення чекають у `booking.catalog-sync`. Після запуску booking-api їх оброблено всі, Ready = 0.
5. **Некоректний JSON → DLQ.** Management UI → Queues → `booking.catalog-sync` → Publish message:
   property `type = RouteRenamedIntegrationEvent`, payload `{not json`. У лозі — «Permanent failure ... Sending
   to DLQ», повідомлення лежить у `booking.catalog-sync.dlq` із заголовком `x-death`.
6. **Повтори.** Зупиніть `sql` у Dashboard і опублікуйте коректну подію в `booking.catalog-sync` через
   Management UI (тіло нижче). У логах буде «Retry 1/3 … 2/3 … 3/3» з інтервалом 5 с, потім «failed after
   3 retries, sending to DLQ». Якщо запустити `sql` раніше, подія обробиться з наступної спроби.

Тіло для ручної публікації, property `type = RouteRenamedIntegrationEvent`:

```json
{"eventId":"7d1c3a52-1111-4a8e-9f00-000000000001","occurredAt":"2026-11-18T12:00:00Z","correlationId":"manual-1",
 "routeId":1,"oldName":"Заросляк – Говерла","newName":"Заросляк – Говерла (ручна подія)"}
```

**Aspire Dashboard:**

- Traces: фільтр `catalog-api`, запит `PUT api/routes/{id}`. Один trace містить publish, process в обох сервісах
  і запити до БД. Між publish і process є невеликий проміжок — це eventual consistency.
- Structured logs: фільтр за `CorrelationId` запиту показує ланцюжок у трьох сервісах.
- Metrics → `Platform.Messaging`: `messaging.process.messages` за `outcome`, `messaging.queue.depth` для `*.dlq`.
- Resources: стан `messaging`. Health check RabbitMQ входить у `/health/ready` сервісів.
