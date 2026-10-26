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
carpathians-databases/
├── README.md
├── booking-db/
│   ├── schema.sql              # DDL: таблиці, ключі, CHECK, індекси
│   ├── procedures.sql          # 6 процедур + функція переходів статусів
│   ├── seed.sql                # тестові дані (ідемпотентний)
│   ├── checks.sql              # перевірка обмежень схеми
│   └── checks_procedures.sql   # перевірка процедур
├── catalog-db/
│   ├── schema.sql
│   └── seed.sql
├── reviews-db/
│   ├── init.js                 # колекції, JSON Schema, індекси
│   ├── seed.js                 # тестові дані (ідемпотентний)
│   ├── queries.js              # запити й aggregation pipeline
│   └── sample-docs.json        # приклади документів
└── docs/
    ├── erd-booking.dbml        # ERD Проєкту №1 (dbdiagram.io)
    ├── erd-catalog.dbml        # ERD Проєкту №2 (dbdiagram.io)
    └── duplication-policy.md
```

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
sqlcmd -S . -E -I -i booking-db/schema.sql
sqlcmd -S . -E -I -i booking-db/procedures.sql
sqlcmd -S . -E -I -i booking-db/seed.sql
```

Для Docker-варіанту замініть `-E` на `-U sa -P "YourStrong!Passw0rd" -C` і вкажіть `-S localhost,1433`.

Перевірка: `booking-db/checks.sql` (обмеження, FK, каскади, індекси) і `booking-db/checks_procedures.sql` (процедури). Усі рядки в Messages мають починатися з `OK`.

### 5.2. Каталог (CatalogContext)

```bash
sqlcmd -S . -E -I -i catalog-db/schema.sql
sqlcmd -S . -E -I -i catalog-db/seed.sql
```

`catalog-db/schema.sql` - проєктна схема для звіту; у Лаб. 03 її реалізує EF Core Code First (Fluent API, міграції, `HasData`).

### 5.3. Відгуки (ReviewContext, MongoDB)

```bash
mongosh "mongodb://localhost:27017" --file reviews-db/init.js
mongosh "mongodb://localhost:27017" --file reviews-db/seed.js
mongosh "mongodb://localhost:27017" --file reviews-db/queries.js   # приклади запитів
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

У кінці `booking-db/schema.sql` і `catalog-db/schema.sql` наведено закоментований приклад окремого логіна застосунку з доступом лише до своєї БД (принцип найменших привілеїв).


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
2. Перейдіть до теки `catalog-service` та виконайте запуск:
   ```bash
   cd catalog-service/Carpathians.Catalog.Api
   dotnet run
   ```
   API слухає на `http://localhost:5231`.
   Swagger UI доступний за адресою: `http://localhost:5231/swagger`.

#### Варіант Б: Запуск через .NET Aspire AppHost
```bash
cd catalog-service/Carpathians.Catalog.AppHost
dotnet run
```

#### Варіант В: Збірка Docker-образу
```bash
cd catalog-service
docker build -t carpathians-catalog-api -f Carpathians.Catalog.Api/Dockerfile .
docker run -p 8080:8080 -e ConnectionStrings__CatalogDb="Server=host.docker.internal,1434;Database=CatalogContext;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True" carpathians-catalog-api
```

---

### 4. Застосування міграцій EF Core

У проєкті використовується `dotnet-ef`. Команди виконуються з явно вказаними проектами:

- **Створення нової міграції**:
  ```bash
  dotnet ef migrations add <MigrationName> --project catalog-service/Carpathians.Catalog.Dal --startup-project catalog-service/Carpathians.Catalog.Api
  ```
- **Застосування міграцій до бази даних**:
  ```bash
  dotnet ef database update --project catalog-service/Carpathians.Catalog.Dal --startup-project catalog-service/Carpathians.Catalog.Api
  ```
- **Генерація ідемпотентного SQL-скрипта міграції**:
  ```bash
  dotnet ef migrations script --idempotent --project catalog-service/Carpathians.Catalog.Dal --startup-project catalog-service/Carpathians.Catalog.Api -o migrations.sql
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
