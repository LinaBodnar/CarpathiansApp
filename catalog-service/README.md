# Carpathians Catalog Service

Сервіс каталогу туристичних маршрутів, географічних точок, регіонів Карпат, необхідного спорядження та фотогалереї. Побудований за трирівневою архітектурою згідно з Clean Architecture та DDD-практиками на платформі **.NET 8** та **Entity Framework Core 8**.

---

## 1. Опис домену

Доменна модель моделює гірські туристичні маршрути в Українських Карпатах:
- **`Region` (Регіон)**: географічний масив (Чорногора, Горгани, Мармароси тощо).
- **`TrailRoute` (Туристичний маршрут)**: агрегатний корінь маршруту з характеристиками (складність, дистанція, тривалість, набір висоти), бізнес-правилами, м'яким видаленням (`IsDeleted`, `DeletedAt`, `DeletedBy`).
- **`RouteDetail` (Деталі маршруту)**: зв'язок 1:1 (опис, сезон, максимальна висота над рівнем моря, початкова точка, особливі вимоги).
- **`MapPoint` (Географічна точка)**: вершини, полонини, притулки, джерела з координатами (WGS84).
- **`RoutePoint` (Точка на маршруті)**: зв'язок M:N між маршрутами та точками через явну проміжну сутність зі складеним ключем `(RouteId, PointId)` та порядковим номером проходження `SequenceNo`.
- **`GearItem` (Спорядження)**: зв'язок 1:N до маршруту, список обов'язкового/рекомендованого спорядження.
- **`GalleryImage` (Фотогалерея)**: зв'язок 1:N до географічної точки, фотографії з порядком сортування.

---

## 2. Системні вимоги

- **.NET 8 SDK** (версія 8.0.400+)
- **SQL Server 2022** (або Docker-контейнер `mcr.microsoft.com/mssql/server:2022-latest`)
- **Docker / Docker Compose** (опціонально для контейнеризації)
- **.NET Aspire Workload** (для запуску через AppHost)

---

## 3. Запуск проєкту

### Варіант А: Локальний запуск (Kestrel + Docker SQL Server)
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

### Варіант Б: Запуск через .NET Aspire AppHost
```bash
cd catalog-service/Carpathians.Catalog.AppHost
dotnet run
```

### Варіант В: Збірка Docker-образу
```bash
cd catalog-service
docker build -t carpathians-catalog-api -f Carpathians.Catalog.Api/Dockerfile .
docker run -p 8080:8080 -e ConnectionStrings__CatalogDb="Server=host.docker.internal,1434;Database=CatalogContext;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True" carpathians-catalog-api
```

---

## 4. Застосування міграцій EF Core

У проєкті використовується `dotnet-ef`. Команди виконуються з явно вказаними проектами:

- **Створення нової міграції**:
  ```bash
  dotnet ef migrations add <MigrationName> --project Carpathians.Catalog.Dal --startup-project Carpathians.Catalog.Api
  ```
- **Застосування міграцій до бази даних**:
  ```bash
  dotnet ef database update --project Carpathians.Catalog.Dal --startup-project Carpathians.Catalog.Api
  ```
- **Генерація ідемпотентного SQL-скрипта міграції**:
  ```bash
  dotnet ef migrations script --idempotent --project Carpathians.Catalog.Dal --startup-project Carpathians.Catalog.Api -o migrations.sql
  ```

> **Примітка:** У середовищах розробки (`!env.IsProduction()`) міграції та сідінг автоматично виконуються під час старту застосунку в `Program.cs`.

---

## 5. Ідемпотентний сідінг даних

1. **Статичний сідінг (HasData)**: фіксовані системні довідники регіонів з фіксованими `Id` у `RegionConfiguration.cs`.
2. **Програмний сідінг (`DbInitializer.SeedAsync`)**:
   - Перевіряє наявність записів через `.AnyAsync()` перед вставкою, запобігаючи дублюванню.
   - Дотримується порядку залежностей: Регіони -> Точки -> Маршрути -> Деталі -> Спорядження -> RoutePoints -> Фотогалерея.
   - Виконується лише у середовищі розробки (`!env.IsProduction()`).
3. **SQL-скрипти**:
   - Схема: `db/p2/schema.sql`
   - Дані: `db/p2/seed.sql`

---

## 6. Пагінація, фільтрація та сортування

### Параметри запиту (`GET /api/routes`)
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

### Заголовки відповіді (Headers)
- `X-Total-Count`: загальна кількість знайдених записів
- `X-Page`: поточна сторінка
- `X-Page-Size`: розмір сторінки
- `X-Total-Pages`: загальна кількість сторінок

### Тіло відповіді (`PagedResult<T>`)
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

## 7. Specification Pattern (Ardalis.Specification)

Фільтрація та вибірка маршрутів винесена у специфікації:
- **`RouteFilterPaginatedSpec`**: інкапсулює фільтри (Where), завантаження зв'язків (Include Region, Detail), сортування (OrderBy) та пагінацію (Skip/Take).
- **`RouteFilterCountSpec`**: інкапсулює ті самі критерії фільтрації без Skip/Take для точного підрахунку `totalCount`.
- **`PopularChallengingRoutesSpec`**: альтернативна доменна специфікація для складних маршрутів (`Difficulty >= Moderate`, `ElevationGain >= 500m`), що виконується через той самий метод `ListAsync(spec)`.

---

## 8. Таблиця HTTP-кодів відповідей

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

## 9. Налаштування Serilog та структуровані логи

- **Бібліотека**: `Serilog.AspNetCore`, `Serilog.Sinks.File`.
- **Логування SQL**: `Microsoft.EntityFrameworkCore.Database.Command` налаштовано на рівень `Information` для відстеження кількості SQL-запитів.
- **Enrichment**:
  - `CorrelationId`: автоматично передається або генерується через заголовок `X-Correlation-Id` за допомогою `CorrelationIdMiddleware`.
  - `RequestPath`: поточний шлях запиту.
  - `UserId`: користувач запиту.
- **Запобігання дублюванню**: винятки обробляються та логуються виключно в `DomainExceptionHandler`. `app.UseSerilogRequestLogging()` забезпечує єдиний компактний HTTP summary-рядок для кожного запиту.
- **Структуровані параметри**: шаблони повідомлень формуються без string interpolation (наприклад, `_logger.LogInformation("Retrieving route with ID {Id}", id)`).

---

## 10. AutoMapper LicenseKey

Починаючи з AutoMapper 14.x+, для некомерційної розробки або ліцензійного використання потрібне встановлення ліцензійного ключа.
- **Де взяти**: на офіційному сайті [Lucky Penny Software](https://luckypennysoftware.com) або з ліцензійного договору компанії.
- **Конфігурація**: ключ передається через секцію `AutoMapper:LicenseKey` в `appsettings.json` або змінну середовища `AutoMapper__LicenseKey`:
  ```json
  "AutoMapper": {
    "LicenseKey": "your-license-key-here"
  }
  ```
  Для локальної розробки без ліцензійного ключа бібліотека виводить попередження в консоль, дозволяючи повноцінне виконання мапінгів.
