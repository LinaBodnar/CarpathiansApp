# Carpathians.Reviews — мікросервіс відгуків (Clean Architecture + CQRS + MongoDB)

Практичне заняття №04. Сервіс працює з MongoDB-базою `ReviewContext` з Проєкту №3 (Лаб. 01, `reviews-db/`).

## Шари рішення

| Проєкт | Шар | Залежить від |
|---|---|---|
| `Carpathians.Reviews.Domain` | Domain (Core): сутності, Value Objects, доменні винятки, інтерфейси репозиторіїв | — (лише `MongoDB.Bson` для типу `ObjectId`) |
| `Carpathians.Reviews.Application` | Application (Use Cases): команди, запити, обробники MediatR, Pipeline Behaviors, валідатори, DTO | Domain |
| `Carpathians.Reviews.Infrastructure` | Infrastructure (Persistence): MongoDB-контекст, BSON-мапінг, репозиторії, індекси, сідінг | Application, Domain |
| `Carpathians.Reviews.WebApi` | Presentation: контролери, обробка помилок, DI, Swagger | Application, Infrastructure (лише для реєстрації DI) |
| `Carpathians.Reviews.Tests` | unit-тести домену й Application | Domain, Application, Infrastructure |

```
WebApi ──► Application ──► Domain
   │            ▲
   └──► Infrastructure
```

Головне правило: внутрішні шари не знають про зовнішні. `Domain` не має посилань на інші проєкти рішення.
