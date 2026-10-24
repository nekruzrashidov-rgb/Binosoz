# Sitora — backend платформы застройщика

Единый API для сайта с 3D-домом, CRM менеджеров и личного кабинета покупателя.
Бронь с сайта сразу попадает в CRM, платежи и документы из CRM сразу видны покупателю.

- ТЗ: [docs/TZ.md](docs/TZ.md)
- Принятые решения: [docs/DECISIONS.md](docs/DECISIONS.md)
- Деплой на VPS: [docs/DEPLOY.md](docs/DEPLOY.md)
- Проверка безопасности: [docs/SECURITY.md](docs/SECURITY.md)

Стек: .NET 10, ASP.NET Core Web API, PostgreSQL 18, EF Core, Identity + JWT, Hangfire, SignalR, QuestPDF, Serilog.

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

```bash
export ConnectionStrings__Default="Host=localhost;Port=5432;Database=sitora;Username=...;Password=..."
export Jwt__Key="<не короче 32 символов>"
export Encryption__Key="<32 байта в base64>"
export SEED_ADMIN_PASSWORD="<пароль админа>"
dotnet run --project src/Sitora.WebApi
```

В Development и Staging миграции применяются при старте API. В Production — только отдельной командой `dotnet Sitora.WebApi.dll --migrate` (в prod-compose это сервис `migrate`); без неё API не стартует. Новая миграция:

```bash
dotnet tool restore
dotnet dotnet-ef migrations add <Имя> --project src/Sitora.Infrastructure --output-dir Data/Migrations
```

## Тесты

```bash
dotnet test
```

По умолчанию тесты поднимают PostgreSQL 18 через Testcontainers (нужен запущенный Docker).
Без Docker задайте строку подключения к своему серверу — тесты создадут временные базы и удалят их:

```bash
export SITORA_TEST_DB="Host=localhost;Port=5432;Username=postgres;Password=..."
dotnet test
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
| `Storage__RootPath` | папка для документов и фото (по умолчанию `./storage`, в Docker — том `files`) |
| `Hangfire__ServerEnabled` | `false` выключает фоновые задачи в этом экземпляре |
| `BOOTSTRAP_ADMIN_EMAIL`, `BOOTSTRAP_ADMIN_PASSWORD` | первый администратор в любом окружении, если админов ещё нет (в Production создаётся командой `--migrate`) |
| `Database__MigrateOnStartup` | применять миграции при старте API; по умолчанию `true`, в Production `false` |
| `SEED_ADMIN_PASSWORD`, `SEED_MANAGER_PASSWORD`, `SEED_ACCOUNTANT_PASSWORD`, `SEED_CONTENT_PASSWORD` | пароли пользователей сидера (только Development) |

В docker-compose эти значения берутся из `.env`; образец — [.env.example](.env.example).

## API (`/api/v1`)

Ошибки — в формате Problem Details на русском. Списки — с `page` и `pageSize` (по умолчанию 20, максимум 100). Язык названий — по `Accept-Language` (`ru`, `tg`).

| Метод и путь | Доступ |
| --- | --- |
| `POST /auth/login`, `/auth/refresh`, `/auth/logout`, `GET /auth/me` | все / вошедшие |
| `POST /auth/sms/send`, `/auth/sms/verify` | покупатель (вход по коду из SMS) |
| `GET /buildings`, `/buildings/{id}`, `/buildings/{id}/floors`, `/floors/{id}/apartments`, `/apartments/{id}` | гость |
| `POST`, `PUT /buildings`, `/floors`, `/apartments`; `PATCH /apartments/{id}/on-request` | Content, Admin |
| `POST /apartments/import?buildingId=…` (файл `.xlsx`) | Content, Admin |
| `POST /leads` | гость |
| `GET /leads`, `PATCH /leads/{id}` | Manager, Admin |
| `POST /apartments/{id}/bookings` | гость (имя и телефон), Manager (клиент) |
| `POST /bookings/{id}/extend`, `/bookings/{id}/cancel` | Manager, Admin |
| `GET`, `POST /customers`, `GET /customers/{id}` | Manager, Admin |
| `GET`, `POST /deals`, `GET /deals/{id}`, `PATCH /deals/{id}/stage` | Manager, Admin |
| `GET /crm/summary`, `/crm/pipeline` | Manager, Admin |
| `GET /me/deals` | покупатель |
| `POST /deals/{id}/plan` | Manager, Admin |
| `GET /deals/{id}/schedule`, `/deals/{id}/schedule.pdf`, `/deals/{id}/statement.pdf?date=…` | владелец сделки, Manager, Accountant, Admin |
| `POST /deals/{id}/payments`, `/payments/{id}/reverse` | Accountant, Admin |
| `GET /rates/current` | все |
| `PUT /rates` | Accountant, Admin |
| `GET /deals/{id}/documents`, `/documents/{id}/download` (подписанная ссылка на 15 минут) | владелец сделки, Manager, Accountant, Admin |
| `GET /files?token=…` — скачивание по подписанной ссылке | любой, у кого есть действующая ссылка |
| `POST /deals/{id}/documents` (файл) | Manager, Accountant, Admin |
| `GET /buildings/{id}/stages` (фото — подписанными ссылками на 15 минут) | покупатель со сделкой в доме, сотрудники |
| `POST /buildings/{id}/stages`, `PUT /stages/{id}`, `POST /stages/{id}/photos` | Content, Admin |
| `GET`, `POST /deals/{id}/messages`; SignalR `/hubs/chat` | владелец сделки, Manager, Accountant, Admin |
| `GET /audit`, `GET`, `POST /users`, `PATCH /users/{id}/status` | Admin |

- Для чужой сделки любой из адресов `/deals/{id}/…` отвечает 404, как для несуществующей.
- Покупатель входит так: `sms/send` с телефоном → код в SMS → `sms/verify` с кодом. Пока SMS — заглушка: в Development код виден в логе API.
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
src/Sitora.Domain          сущности, перечисления, DTO, интерфейсы, исключения
src/Sitora.Infrastructure  EF Core, Identity, сервисы, интеграции, фоновые задачи, JWT, шифрование
src/Sitora.WebApi          контроллеры, хаб SignalR, middleware, валидаторы, Program.cs, Dockerfile
tests/Sitora.Tests         модульные и интеграционные тесты
deploy/Caddyfile           обратный прокси с HTTPS для продакшена
scripts/                   бэкап и восстановление
```
