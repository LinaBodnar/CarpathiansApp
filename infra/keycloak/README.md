# infra/keycloak — Keycloak як код (Pulumi, C#)

Pulumi-проєкт створює realm **DevRealmDrivers** у Keycloak, який запускає Aspire AppHost (`http://localhost:8080`):

| Що | Де в коді |
|---|---|
| Realm (registration, remember me, themes, термін дії токенів, ротація refresh token) | `KeycloakStack.cs` |
| Resource servers `booking-api`, `catalog-api`, `payments-api`: ресурс, scopes, client-ролі permissions | `ResourceServerExtensions.AddResourceServer` |
| Realm-ролі `admin`, `manager`, `user`, `guest` — composite над client-ролями за матрицею | `KeycloakStack.cs` |
| Permission Policies: role-політики і scope-permissions | `ResourceServerExtensions.WithRolePermissions` |
| Клієнти `postman` (Authorization Code), `swagger` (публічний, PKCE), `aggregator` (Client Credentials, composite client-роль `service-reader`); mappers `role`, `aud`, `permissions` | `ClientFactory.cs` |
| Тестові користувачі `alice` (admin), `bob` (manager), `carol` (user), `dave` (guest): email підтверджено, пароль тимчасовий | `KeycloakStack.cs`, `ClientFactory.AddTestUser` |
| Outputs `realmUrl`, `adminConsoleUrl`, `clientIds`, `clientSecrets` | `KeycloakStack.cs` |
| Матриця «роль → permissions» | `Platform.ServiceDefaults/Security/Permissions.cs` (той самий файл, що й у сервісах) |

## Запуск

1. Запустити AppHost і дочекатися, поки ресурс `keycloak` стане Healthy.
2. У цій папці (один раз):
   ```powershell
   pulumi login --local
   pulumi stack init dev
   pulumi config set --secret keycloak:password <Dashboard -> Parameters -> keycloak-admin-password>
   pulumi config set --secret testUserPassword <пароль тестових користувачів>
   ```
3. `pulumi up` — створює або оновлює realm (ідемпотентно; повторний запуск без змін нічого не змінює).
4. `pulumi stack output` — realm, Admin Console, клієнти; секрети: `pulumi stack output clientSecrets --show-secrets`.
5. Паролі тестових користувачів тимчасові (зміна під час першого входу). Для прогону матриці через password grant:
   `pulumi config set temporaryPasswords false`, потім `pulumi up`.

Keycloak зберігає дані в PostgreSQL (`keycloak-postgres`). Якщо realm створювали до переходу на PostgreSQL,
Pulumi-стан посилається на ресурси, яких уже немає: `pulumi refresh`, потім `pulumi up`.

## Отримання permissions (RPT)

1. Access token користувача (клієнт `postman`, Authorization Code або для швидкої перевірки — password grant).
2. Обмін на RPT для конкретного resource server:
   ```
   POST http://localhost:8080/realms/DevRealmDrivers/protocol/openid-connect/token
   Authorization: Bearer <access token>
   grant_type=urn:ietf:params:oauth:grant-type:uma-ticket&audience=booking-api
   ```
3. У RPT claim `authorization.permissions[].scopes` містить лише дозволені permissions (напр. для `bob`: `bookings:read`, `bookings:update`, `bookings:approve`). Сервіси читають його в `PermissionAuthorizationHandler`.

Звичайний access token теж містить permissions — claim `permissions` (mapper client-ролей):
`{"booking-api": ["bookings:read", "bookings:update", "bookings:approve"], "catalog-api": [...]}`.
