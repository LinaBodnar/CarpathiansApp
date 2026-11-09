# Carpathians.Reviews — мікросервіс відгуків і обговорень

Практичне заняття №04: **Clean Architecture + CQRS (MediatR) + MongoDB**.
Сервіс працює з базою `ReviewContext` з Проєкту №3 (Лаб. 01, тека `reviews-db/`): колекції
`reviews`, `discussions`, `discussionMessages`, `ratings`.

---

## 1. Шари рішення

| Проєкт | Шар | Вміст | Залежить від |
|---|---|---|---|
| `Carpathians.Reviews.Domain` | Domain (Core) | сутності `Review`, `Comment`, `Discussion`, `DiscussionMessage`; Value Objects `Author`, `GeoPoint`, `WeatherInfo`, `MessagePreview`; доменні винятки; інтерфейси `IReviewRepository`, `IDiscussionRepository` | — (лише `MongoDB.Bson` для типу `ObjectId`) |
| `Carpathians.Reviews.Application` | Application (Use Cases) | команди й запити MediatR з обробниками, валідатори FluentValidation, Pipeline Behaviors, DTO, AutoMapper-профіль, інтерфейси `IReviewQueries`, `IDiscussionQueries`, `IRatingService` | Domain |
| `Carpathians.Reviews.Infrastructure` | Infrastructure (Persistence) | `MongoDbContext`, BSON-мапінг (`BsonClassMap`), репозиторії з оптимістичною конкурентністю, читальна сторона (проєкції, aggregation pipeline), індекси, сідінг, health check | Application, Domain |
| `Carpathians.Reviews.WebApi` | Presentation | контролери (лише `ISender`), контракти API, глобальна обробка помилок, Serilog, Swagger | Application, Infrastructure (лише для реєстрації DI) |
| `Carpathians.Reviews.Tests` | Тести | unit-тести домену, обробників, валідаторів, behaviors, BSON-мапінгу, обробки помилок | усі шари |

```
            ┌──────────────────────────┐
            │  WebApi (Presentation)   │
            └──────┬─────────────┬─────┘
                   │             │ (лише DI)
                   ▼             ▼
      ┌────────────────┐   ┌──────────────────┐
      │  Application   │◄──│  Infrastructure  │
      └───────┬────────┘   └────────┬─────────┘
              ▼                     │
      ┌────────────────┐            │
      │     Domain     │◄───────────┘
      └────────────────┘
```

Правило залежностей: внутрішні шари не знають про зовнішні. `Domain` не посилається на жоден проєкт
рішення; `Application` оголошує інтерфейси, які реалізує `Infrastructure`; жоден контролер чи обробник
не звертається до `IMongoCollection` напряму.

---

## 2. CQRS і Pipeline Behaviors

Кожна операція API — окрема команда (`ICommand`, `ICommand<T>`) або запит (`IQuery<T>`) з власним обробником.

| Команди (запис) | Запити (читання) |
|---|---|
| `CreateReviewCommand` | `GetReviewByIdQuery` |
| `UpdateReviewCommand` (з `ExpectedVersion`) | `GetReviewsListQuery` (фільтри, сортування, skip/limit) |
| `DeleteReviewCommand` | `GetReviewsFeedQuery` (курсорна пагінація) |
| `AddReviewCommentCommand` | `GetRouteRatingQuery`, `GetTopRatedRoutesQuery` (aggregation) |
| `CreateDiscussionCommand`, `PostDiscussionMessageCommand` | `GetDiscussionByIdQuery`, `GetRouteDiscussionsQuery`, `GetDiscussionMessagesQuery` |
| `SyncRouteNameCommand` (оновлення денормалізованої назви маршруту) | |

Обробники команд працюють через репозиторії доменного шару; обробники запитів — через `IReviewQueries` /
`IDiscussionQueries`, реалізація яких у Infrastructure одразу проєктує документи у DTO.

Кожна команда й запит проходить ланцюжок behaviors (порядок реєстрації = порядок виконання):

1. `LoggingBehavior` — «Handling {Request}» / «Handled in N ms» (структуровано, Serilog);
2. `UnhandledExceptionBehavior` — логування винятків (Warning для бізнес-винятків, Error для решти);
3. `ValidationBehavior` — усі валідатори FluentValidation; помилки збираються разом, обробник не викликається;
4. `PerformanceBehavior` — попередження, якщо обробник працює довше 500 мс.

Валідація на двох рівнях: DataAnnotations у контрактах API (`[Required]`, довжини) і FluentValidation
у pipeline (діапазони, коректність `ObjectId`, асинхронна перевірка «один відгук від автора на маршрут»).

---

## 3. Модель документів і MongoDB

Структура документів — як у Лаб. 01 (`reviews-db/init.js`): camelCase, вкладені `author`, `comments`,
`weather`, `gpsTrack`. Мапінг налаштовано в `Infrastructure/Persistence/Mappings/MongoMappings.cs` через
`BsonClassMap` — доменні класи не містять BSON-атрибутів.

| Прийом | Де |
|---|---|
| Embedding | `reviews.author`, `reviews.comments`, `reviews.weather`, `reviews.gpsTrack` |
| Referencing | `reviews.routeId` (Каталог, без FK), `reviews.discussionId` → `discussions`, `discussionMessages.discussionId` |
| Гібрид | `discussions.lastMessages` (3 останні повідомлення) + повна гілка в `discussionMessages` |
| Гнучка схема | `photos`, `tags`, `gpsTrack`, `weather`, `discussionId`, `isVerifiedParticipant` є лише в частині документів; порожні масиви не записуються |
| Денормалізація | `reviews.routeName`, `discussions.routeName` (копія з Каталогу), `ratings` (похідні дані) |

**Оптимістична конкурентність.** Кожен документ має поле `version`. Репозиторій замінює документ лише
з фільтром `{ _id, version: очікувана }` і збільшує версію; якщо документ змінив інший запит — `409 Conflict`.
API віддає версію в заголовку `ETag`, а `PUT`/`DELETE` приймають очікувану версію в `If-Match`.
Документи, створені seed-скриптом Лаб. 01 (без поля `version`), вважаються версією 0.

### Індекси (створюються автоматично при старті — `IndexCreationService`)

Назви й параметри збігаються з `reviews-db/init.js`, тому повторне створення безпечне.

| Колекція | Індекс | Під який запит |
|---|---|---|
| reviews | `ix_reviews_route_created` `{ routeId: 1, createdAt: -1 }` | останні відгуки маршруту |
| reviews | `ux_reviews_route_author` `{ routeId: 1, author.userId: 1 }`, unique | один відгук від автора на маршрут |
| reviews | `ix_reviews_author` `{ author.userId: 1 }` | відгуки користувача |
| reviews | `ix_reviews_discussion` `{ discussionId: 1 }`, partial | відгуки з обговоренням |
| reviews | `tx_reviews_text` `{ text: "text" }` | повнотекстовий пошук `?search=` |
| reviews | `ix_reviews_tags` `{ tags: 1 }` (multikey) | фільтр `?tag=` |
| reviews | `ix_reviews_rating` `{ rating: -1, _id: -1 }` | сортування `?sortBy=rating` |
| discussions | `ix_discussions_route_activity` `{ routeId: 1, lastActivityAt: -1 }` | обговорення маршруту |
| discussionMessages | `ix_messages_discussion_created` `{ discussionId: 1, createdAt: 1 }` | гілка повідомлень |
| ratings | `ux_ratings_route` `{ routeId: 1 }`, unique | один рейтинг на маршрут, `$merge on: routeId` |

### Сідінг (`IDataSeeder`, ідемпотентний)

`DiscussionSeeder` → `ReviewSeeder` → `RatingSeeder`. Кожен сідер спершу перевіряє, чи колекція порожня,
і вставляє дані bulk-операцією `InsertManyAsync`; рейтинги рахуються aggregation pipeline з `$merge`.
Повторний запуск застосунку не створює дублікатів. Дані ті самі, що в `reviews-db/seed.sql`:
5 reviews, 2 discussions, 6 discussionMessages, 3 ratings. Сідінг вимикається `MongoDb:SeedOnStartup=false`
(так налаштовано в `appsettings.Production.json`).

---

## 4. Запуск

Потрібно: **.NET 8 SDK**, **MongoDB 6+** (локально або в Docker), за бажанням `mongosh` / MongoDB Compass.

```bash
# 1. MongoDB у Docker
docker run -d --name mongo-carpathians -p 27017:27017 mongo:7

# 2. (необов'язково) JSON Schema валідатори з Лаб. 01
mongosh "mongodb://localhost:27017" --file reviews-db/init.js

# 3. Запуск сервісу — індекси й початкові дані створяться автоматично
cd reviews-service/Carpathians.Reviews.WebApi
dotnet run
```

- API: `http://localhost:5203`, Swagger UI: `http://localhost:5203/swagger`
- Health check: `http://localhost:5203/health`
- Готові запити для Rider / VS: `Carpathians.Reviews.WebApi/Carpathians.Reviews.WebApi.http`

Тести:

```bash
cd reviews-service
dotnet test
```

### Конфігурація

| Ключ | Призначення |
|---|---|
| `MongoDb:ConnectionString` | рядок підключення; пул і таймаути — параметрами `maxPoolSize`, `serverSelectionTimeoutMS` |
| `MongoDb:DatabaseName` | назва БД (`ReviewContext`) |
| `MongoDb:SeedOnStartup` | наповнювати порожню БД під час старту |
| `LuckyPenny:LicenseKey` | ключ Community-ліцензії MediatR 13+ / AutoMapper 15+ ([luckypennysoftware.com](https://luckypennysoftware.com)); без ключа лише попередження в лозі |
| `Serilog` | рівні логування, консоль і файл `logs/reviews-*.log` |

Окремі значення для середовищ — `appsettings.Development.json` і `appsettings.Production.json`; будь-який
ключ можна перевизначити змінною оточення, наприклад `MongoDb__ConnectionString`.

---

## 5. Ендпоінти

| Метод | Шлях | Опис | Успіх |
|---|---|---|---|
| GET | `/api/reviews` | список: `routeId`, `authorId`, `minRating`, `tag`, `search`, `sortBy` (createdAt\|rating), `sortDir`, `page`, `pageSize` | 200 + `X-Total-Count` |
| GET | `/api/reviews/feed` | курсорна пагінація: `routeId`, `after`, `limit` | 200 |
| GET | `/api/reviews/{id}` | повний відгук | 200 + `ETag` |
| POST | `/api/reviews` | створення відгуку | 201 + `Location` |
| PUT | `/api/reviews/{id}` | редагування (`If-Match: "версія"`) | 204 + `ETag` |
| DELETE | `/api/reviews/{id}` | видалення (`If-Match` — необов'язково) | 204 |
| POST | `/api/reviews/{id}/comments` | коментар (вбудований масив) | 200 + `ETag` |
| GET | `/api/routes/{routeId}/rating` | рейтинг маршруту (`$group`) | 200 |
| GET | `/api/routes/top-rated` | найкращі маршрути: `top`, `minReviews` | 200 |
| GET | `/api/routes/{routeId}/discussions` | обговорення маршруту | 200 |
| PUT | `/api/routes/{routeId}/name` | синхронізація `routeName` (імітація події Каталогу) | 200 |
| GET | `/api/discussions/{id}` | обговорення з останніми повідомленнями | 200 |
| POST | `/api/discussions` | нове обговорення (+ прив'язка до відгуку `reviewId`) | 201 |
| GET | `/api/discussions/{id}/messages` | повна гілка повідомлень | 200 |
| POST | `/api/discussions/{id}/messages` | нове повідомлення / відповідь `replyTo` | 201 |

Приклад створення відгуку:

```bash
curl -i -X POST http://localhost:5203/api/reviews \
  -H "Content-Type: application/json" \
  -d '{"routeId":3,"routeName":"Заросляк – Говерла – Петрос","authorId":"7","authorName":"Ірина","rating":5,"text":"Довгий, але дуже красивий маршрут","tags":["ridge"]}'
```

---

## 6. Обробка помилок

Усі помилки повертаються у форматі **ProblemDetails** (RFC 9457) з `traceId` і `correlationId`;
помилки валідації — `ValidationProblemDetails` з переліком помилок по полях. Обробник —
`WebApi/Middleware/GlobalExceptionHandler.cs`, таблиця відповідності — `ExceptionStatusMapper.cs`.

| Виняток | Статус |
|---|---|
| `ValidationException` (FluentValidation), некоректний `If-Match` | 400 |
| `DomainException` | 400 |
| `NotFoundException` | 404 |
| `ConflictException` (у т. ч. конфлікт версій) | 409 |
| `MongoWriteException` з `DuplicateKey` (унікальний індекс) | 409 |
| інші `MongoWriteException` | 400 |
| `MongoConnectionException`, таймаут вибору сервера | 503 |
| інші | 500 (у Production — без внутрішніх деталей) |

Кожен виняток логується рівно один раз: у `UnhandledExceptionBehavior` (MediatR) або, якщо виняток
виник поза pipeline, у глобальному обробнику.

---

## 7. Перевірка (контрольні точки)

1. `Carpathians.Reviews.Domain.csproj` не містить `ProjectReference`.
2. `POST /api/reviews` з `"rating": 10` → **400** з `errors.Rating`; у лозі є `Handling CreateReviewCommand`,
   але немає `Handled CreateReviewCommand` — обробник не виконувався.
3. `GET /api/reviews/{id}` → `ETag: "N"`; `PUT` з `If-Match: "N-1"` → **409**.
4. Повторний відгук того самого `authorId` на той самий `routeId` → **400** (валідатор) або **409** (унікальний індекс).
5. `GET /api/reviews/66f0c2000000000000000099` → **404**; `GET /api/reviews/not-an-id` → **400**.
6. `db.reviews.find()` у Compass/mongosh — документи camelCase без приватних полів .NET, з різними наборами полів.
7. `db.reviews.getIndexes()` — індекси з таблиці вище; повторний запуск не дублює seed-дані.
8. `PUT /api/routes/1/name` з новою назвою → `routeName` оновлено в reviews і discussions маршруту 1.
