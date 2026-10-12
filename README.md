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

## 12. Booking Service (Лаб. 02)

Мікросервіс контексту «Бронювання»: ASP.NET Core Web API, тришарова архітектура
(Api -> Bll -> Dal), ADO.NET + Dapper, Unit of Work, .NET Aspire.

### 12.1. Структура рішення

```
booking-service/
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
   `booking-service/Carpathians.Booking.AppHost/appsettings.Development.json`
   (`Parameters:sql-password`). Це пароль лише локального контейнера.
3. Aspire створить порожню базу `BookingContext` у контейнері (порт `14333`).
   Застосуйте скрипти з кореня репозиторію в такому порядку:

```bash
sqlcmd -S localhost,14333 -U sa -P "<пароль>" -C -I -i booking-db/schema.sql
sqlcmd -S localhost,14333 -U sa -P "<пароль>" -C -I -i booking-db/procedures.sql
sqlcmd -S localhost,14333 -U sa -P "<пароль>" -C -I -i booking-db/seed.sql
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
dotnet test booking-service/Carpathians.Booking.Tests
```