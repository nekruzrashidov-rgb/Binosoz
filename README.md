# Sitora — backend платформы застройщика

Единый API для сайта с 3D-домом, CRM менеджеров и личного кабинета покупателя.
Бронь с сайта сразу попадает в CRM, платежи и документы из CRM сразу видны покупателю.

- ТЗ: [docs/TZ.md](docs/TZ.md)
- Принятые решения: [docs/DECISIONS.md](docs/DECISIONS.md)
- Деплой на VPS: [docs/DEPLOY.md](docs/DEPLOY.md)
- Проверка безопасности: [docs/SECURITY.md](docs/SECURITY.md)

Стек: .NET 10, ASP.NET Core Web API, PostgreSQL 18, EF Core, Identity + JWT, Hangfire, SignalR, QuestPDF, Serilog.
Фронтенд (сайт, кабинет, CRM; чистый JavaScript без сборки) — в [frontend/](frontend/README.md). Контракт API и сверка с фронтендом — [docs/API_CONTRACT.md](docs/API_CONTRACT.md). Прежний React-фронтенд и прочее неиспользуемое — в `_old/`.

## API и фронтенд одной командой (Windows)

Двойной щелчок по `run.cmd` в корне репозитория или в терминале:

```bash
.\run.cmd
```

Скрипт открывает API в отдельном окне «Sitora API» (если он ещё не запущен), ждёт, пока API ответит, раздаёт папку `frontend/` статик-сервером `scripts/serve-web.mjs` и открывает сайт в браузере (http://localhost:5500). Нужны .NET 10 SDK, Node.js, локальный PostgreSQL и секреты в user secrets (раздел «Переменные окружения»). Остановить сайт — Ctrl+C, API — закрыть его окно.

## Запуск для разработки за 3 команды

Нужен Docker с Compose.

```bash
cp .env.example .env      # заполните POSTGRES_PASSWORD, JWT_KEY, ENCRYPTION_KEY и SEED_*_PASSWORD
docker compose up -d --build
curl http://localhost:8080/health
```

- Swagger: http://localhost:8080/swagger
- Вход админа: `POST /api/v1/auth/login` с `{"email":"admin@sitora.local","password":"<SEED_ADMIN_PASSWORD>"}`.
  Так же входят `manager@`, `accountant@` и `content@sitora.local` со своими паролями `SEED_*`.
- PostgreSQL снаружи доступен на `localhost:5433` (пользователь и база `sitora`).

В Development сидер создаёт дом «Sitora Residence» (коммерческий этаж 0 и 16 жилых этажей по 8 квартир), 10 выдуманных клиентов, 5 сделок на этапах от «Заявки» до «Оплаты» (у сделки на «Оплате» — график и три платежа), 2 брони с сайта и 8 этапов стройки. Курс USD сидер не создаёт: его вводит бухгалтер через `PUT /api/v1/rates`.

## Запуск без Docker

Нужны .NET 10 SDK и PostgreSQL 18.

Секреты для Development удобнее один раз положить в user secrets (хранятся в профиле пользователя, вне репозитория). После этого `dotnet run` работает в любом окне без переменных окружения:

```bash
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=sitora;Username=...;Password=..." --project backend/src/Sitora.WebApi
dotnet user-secrets set "Jwt:Key" "<не короче 32 символов>" --project backend/src/Sitora.WebApi
dotnet user-secrets set "Encryption:Key" "<32 байта в base64>" --project backend/src/Sitora.WebApi
dotnet user-secrets set "SEED_ADMIN_PASSWORD" "<пароль админа>" --project backend/src/Sitora.WebApi
dotnet run --project backend/src/Sitora.WebApi
```

Вместо user secrets можно задать те же значения переменными окружения (`ConnectionStrings__Default`, `Jwt__Key`, `Encryption__Key`, `SEED_ADMIN_PASSWORD`). В Staging и Production user secrets не читаются — только переменные окружения.

В Development и Staging миграции применяются при старте API. В Production — только отдельной командой `dotnet Sitora.WebApi.dll --migrate` (в prod-compose это сервис `migrate`); без неё API не стартует. Новая миграция:

```bash
dotnet tool restore
dotnet dotnet-ef migrations add <Имя> --project backend/src/Sitora.Infrastructure --output-dir Data/Migrations
```

## Тесты

```bash
dotnet test backend/Sitora.sln
```

По умолчанию тесты поднимают PostgreSQL 18 через Testcontainers (нужен запущенный Docker).
Без Docker задайте строку подключения к своему серверу — тесты создадут временные базы и удалят их:

```bash
export SITORA_TEST_DB="Host=localhost;Port=5432;Username=postgres;Password=..."
dotnet test backend/Sitora.sln
```

## CI

[.github/workflows/ci.yml](.github/workflows/ci.yml) запускается на push в `main` и на pull request:
- задача «Сборка и тесты» — сборка с `--warnaserror` и все тесты, PostgreSQL 18 поднимает Testcontainers;
- задача «Docker-образ и compose» — сборка образа по `Dockerfile` и проверка обоих compose-файлов.

Workflow ещё ни разу не запускался: у репозитория пока нет удалённого репозитория на GitHub.

## Продакшен

Коротко: VPS с Docker, A-запись домена на сервер, `.env` по образцу и

```bash
docker compose -f docker-compose.prod.yml up -d --build
```

Caddy сам получает сертификат HTTPS. Бэкап — `./scripts/backup.sh`, восстановление — `./scripts/restore.sh`.
Пошагово, с обновлением, откатом и cron для бэкапов — в [docs/DEPLOY.md](docs/DEPLOY.md).

## Переменные окружения

| Переменная | Назначение |
| --- | --- |
| `ConnectionStrings__Default` | строка подключения к PostgreSQL |
| `Jwt__Key` | ключ подписи JWT, не короче 32 символов |
| `Encryption__Key` | ключ шифрования паспортных данных, 32 байта в base64 (`openssl rand -base64 32`) |
| `ASPNETCORE_ENVIRONMENT` | `Development`, `Staging` или `Production` |
| `AllowedHosts` | домен API в продакшене (`api.sitora.tj`) |
| `Cors__AllowedOrigins__0`, `__1`, … | адреса фронтендов, которым разрешён CORS |
| `ForwardedHeaders__Enabled` | `true`, только если API стоит за своим обратным прокси (в prod-compose это Caddy) |
| `RateLimiting__LoginPerMinute`, `__SmsPerHour`, `__PublicFormsPerHour` | лимиты на один IP (по умолчанию 10, 20, 30) |
| `RateLimiting__AnonymousPerMinute`, `__UserPerMinute` | общий лимит: гостю на IP и вошедшему на пользователя (по умолчанию 300 и 600) |
| `Storage__RootPath` | папка для документов и фото (по умолчанию `./storage`, в Docker — том `files`) |
| `Hangfire__ServerEnabled` | `false` выключает фоновые задачи в этом экземпляре |
| `BOOTSTRAP_ADMIN_EMAIL`, `BOOTSTRAP_ADMIN_PASSWORD` | первый владелец организации по умолчанию, если владельцев ещё нет (в Production создаётся командой `--migrate`) |
| `ADMIN_EMAIL`, `ADMIN_PASSWORD` | создатель платформы (PlatformOwner); пароль — его второй фактор |
| `Email__Provider` | `Dev` (код в лог, только для разработки), `Smtp` или `Resend` |
| `Email__From`, `Email__FromName`, `Email__InviteUrl` | отправитель писем и адрес страницы приглашения (токен дописывается в конец) |
| `Email__Smtp__Host`, `__Port`, `__Username`, `__Password`, `__UseSsl` | SMTP |
| `Email__Resend__ApiKey` | ключ Resend |
| `Auth__SmsLoginEnabled`, `Auth__PasswordLoginEnabled` | старый вход по SMS (по умолчанию `false`) и вход по паролю (`true`) |
| `Auth__EmailCodesPerMinute`, `Auth__EmailCodesPerHour` | лимит кодов из почты на адрес и на IP (1 и 5) |
| `Database__MigrateOnStartup` | применять миграции при старте API; по умолчанию `true`, в Production `false` |
| `SEED_ADMIN_PASSWORD`, `SEED_MANAGER_PASSWORD`, `SEED_ACCOUNTANT_PASSWORD`, `SEED_CONTENT_PASSWORD` | пароли пользователей сидера (только Development) |
| `SEED_RICHTOWER_OWNER_PASSWORD` | пароль владельца демо-застройщика Rich Tower (`owner@rich-tower.tj`, только Development); `Seed__RichTower=false` отключает этот сидер |

В docker-compose эти значения берутся из `.env`; образец — [.env.example](.env.example).

## API (`/api/v1`)

Ошибки — в формате Problem Details на русском. Списки — с `page` и `pageSize` (по умолчанию 20, максимум 100). Язык названий — по `Accept-Language` (`ru`, `tg`).

| Метод и путь | Доступ |
| --- | --- |
| `POST /auth/email/send`, `/auth/email/verify`, `/auth/second-factor`, `/auth/invitations/accept` | все (вход по коду из почты) |
| `POST /auth/login`, `/auth/refresh`, `/auth/logout`, `GET /auth/me` | все / вошедшие |
| `POST /auth/sms/send`, `/auth/sms/verify` | выключены, включаются `Auth__SmsLoginEnabled=true` |
| `GET /buildings`, `/buildings/{id}`, `/buildings/{id}/floors`, `/floors/{id}/apartments`, `/apartments/{id}` | гость |
| `POST`, `PUT /buildings`, `/floors`, `/apartments`; `PATCH /apartments/{id}/on-request` | Content, руководители (Owner, ObjectAdmin) |
| `POST /apartments/import?buildingId=…` (файл `.xlsx`) | Content, руководители (Owner, ObjectAdmin) |
| `POST /leads` | гость |
| `GET /leads`, `PATCH /leads/{id}` | Manager, руководители (Owner, ObjectAdmin) |
| `POST /apartments/{id}/bookings` | гость (имя и телефон), Manager (клиент) |
| `POST /bookings/{id}/extend`, `/bookings/{id}/cancel` | Manager, руководители (Owner, ObjectAdmin) |
| `GET`, `POST /customers`, `GET /customers/{id}` | Manager, руководители (Owner, ObjectAdmin) |
| `GET`, `POST /deals`, `GET /deals/{id}`, `PATCH /deals/{id}/stage` | Manager, руководители (Owner, ObjectAdmin) |
| `GET /crm/summary`, `/crm/pipeline` | Manager, руководители (Owner, ObjectAdmin) |
| `GET /me/deals` | покупатель |
| `POST /deals/{id}/plan` | Manager, руководители (Owner, ObjectAdmin) |
| `GET /deals/{id}/schedule`, `/deals/{id}/schedule.pdf`, `/deals/{id}/statement.pdf?date=…` | владелец сделки, Manager, Accountant, руководители (Owner, ObjectAdmin) |
| `POST /deals/{id}/payments`, `/payments/{id}/reverse` | Accountant, руководители (Owner, ObjectAdmin) |
| `GET /reports/payments?from=&to=`, `/reports/payments.xlsx` — поступления за период | Accountant, руководители (Owner, ObjectAdmin) |
| `GET /reports/debts?asOf=`, `/reports/debts.xlsx` — задолженность на дату | Accountant, руководители (Owner, ObjectAdmin) |
| `GET /rates/current` | все |
| `PUT /rates` | Accountant, руководители (Owner, ObjectAdmin) |
| `GET /deals/{id}/documents`, `/documents/{id}/download` (подписанная ссылка на 15 минут) | владелец сделки, Manager, Accountant, руководители (Owner, ObjectAdmin) |
| `GET /files?token=…` — скачивание по подписанной ссылке | любой, у кого есть действующая ссылка |
| `POST /deals/{id}/documents` (файл) | Manager, Accountant, руководители (Owner, ObjectAdmin) |
| `GET /buildings/{id}/stages` (фото — подписанными ссылками на 15 минут) | покупатель со сделкой в доме, сотрудники |
| `POST /buildings/{id}/stages`, `PUT /stages/{id}`, `POST /stages/{id}/photos` | Content, руководители (Owner, ObjectAdmin) |
| `GET`, `POST /deals/{id}/messages`; SignalR `/hubs/chat` | владелец сделки, Manager, Accountant, руководители (Owner, ObjectAdmin) |
| `GET`, `POST /users`, `PATCH /users/{id}/status`, `POST`, `GET`, `DELETE /invitations` | руководители (Owner, ObjectAdmin) |
| `GET /audit`, `POST /buildings`, `PUT /users/{id}/buildings` | Owner |
| `GET`, `POST /platform/organizations`, `POST /platform/impersonate/{organizationId}`, `/platform/impersonate/stop`, `/platform/totp` | PlatformOwner |
| `GET /health/live`, `/health/ready` (и `/health`) — без префикса `/api/v1` | все |

- Для чужой сделки любой из адресов `/deals/{id}/…` отвечает 404, как для несуществующей.
- Все входят по почте: `email/send` → код в письме → `email/verify`. Создатель платформы после кода вводит второй фактор (`second-factor`). В Development с `Email__Provider=Dev` код виден в логе API. Роли, организации и изменения для фронтенда — в [docs/API_CONTRACT.md](docs/API_CONTRACT.md).
- Бронь живёт 48 часов; фоновая задача Hangfire раз в 5 минут снимает просроченные брони.
- Чат в реальном времени: подключитесь к `/hubs/chat` (JWT в заголовке или в `?access_token=`), вызовите `JoinDeal(dealId)` и слушайте событие `messageReceived`. Отправка — `SendMessage(dealId, text)` в хабе или `POST /deals/{id}/messages`.
- Excel для импорта: первая строка — заголовки, столбцы A–G: этаж, номер квартиры, комнат (0 — студия), площадь, цена за м², позиция на плане этажа (0–7), ID в 3D-модели (необязательно; пустая ячейка не стирает уже заданный).
- Для сайта с 3D-домом у квартиры есть `planSlot` (позиция 0–7 на плане этажа, уникальна в пределах этажа) и `model3dId` (объект в 3D-модели). Описание дома — `description` на языке `Accept-Language`, плюс `descriptionRu` и `descriptionTg`.

## Известные ограничения

- **Docker не запускался.** На машине разработки нет WSL, поэтому образ не собирался, контейнеры (`docker compose up`, сервис `migrate`, Caddy) не поднимались, и инструкция деплоя на VPS не проходилась. Без Docker проверено:
  - оба compose-файла проходят `docker compose config`;
  - команда публикации из Dockerfile работает, и опубликованная сборка в Production проходит путь «отказ без миграций → `--migrate` → старт за прокси → `/health`, HSTS, редирект HTTP → HTTPS, закрытый Swagger, отказ чужому Host, вход bootstrap-админа, JSON-логи без паролей»;
  - команды `pg_dump`/`pg_restore` из скриптов восстанавливают базу с данными сидера один в один (в пустую базу и поверх изменённой), команды `tar`/`find` для файлов работают.

  Первый деплой нужно пройти по [docs/DEPLOY.md](docs/DEPLOY.md) с проверками из её раздела 4.
- **Внешние сервисы — заглушки.** SMS, Telegram и загрузка курса НБТ закрыты интерфейсами (`ISmsSender`, `INotificationSender`, `IRateProvider`) и только пишут в лог. Вне Development вход покупателей по SMS не заработает, пока не подключён провайдер SMS. Курс USD вводит бухгалтер вручную.
- **Один экземпляр API.** Кэш каталога в памяти процесса, SignalR без backplane. Для нескольких экземпляров нужен Redis (кэш и SignalR).
- **Файлы на диске сервера** (том `files`), не в объектном хранилище. Их бэкапит `scripts/backup.sh` вместе с базой.
- **Производительность каталога** (300 мс для 95% запросов) обеспечивается кэшем на 30 секунд, но нагрузочно не проверялась.
- **Лицензия QuestPDF** — Community: бесплатна для компаний с годовой выручкой до 1 млн долларов. Перед продакшеном проверить, подходит ли застройщику.
- **Выданный access-токен** заблокированного пользователя действует до 15 минут; refresh-токены отзываются сразу.
- **Нет** смены и сброса пароля сотрудниками, дашборда Hangfire, онлайн-оплаты, мобильного приложения и нескольких застройщиков (последние три — вне MVP по ТЗ).
- Телефоны принимаются только таджикские (`+992`), валюты договора — TJS и USD.
- Миграции в продакшене применяются отдельной командой при деплое; отменить их можно только восстановлением из бэкапа, поэтому перед обновлением делайте бэкап (см. DEPLOY.md).

## Структура

```
backend/                          .NET: решение Sitora.sln, проекты и тесты
backend/src/Sitora.Domain          сущности, перечисления, DTO, интерфейсы, исключения
backend/src/Sitora.Infrastructure  EF Core, Identity, сервисы, интеграции, фоновые задачи, JWT, шифрование
backend/src/Sitora.WebApi          контроллеры, хаб SignalR, middleware, валидаторы, Program.cs, Dockerfile
backend/tests/Sitora.Tests         модульные и интеграционные тесты
frontend/                         сайт, кабинет и CRM (статические файлы; vercel.json публикует эту папку)
deploy/Caddyfile                  обратный прокси с HTTPS для продакшена
scripts/                          бэкап и восстановление
```
