# Sitora — backend платформы застройщика

Единый API для сайта с 3D-домом, CRM менеджеров и личного кабинета покупателя.
ТЗ: [docs/TZ.md](docs/TZ.md). Принятые решения: [docs/DECISIONS.md](docs/DECISIONS.md).

## Запуск за 3 команды

Нужен Docker с Compose.

```bash
cp .env.example .env      # заполните пароли и JWT_KEY (не короче 32 символов)
docker compose up -d --build
curl http://localhost:8080/health
```

- Swagger: http://localhost:8080/swagger
- Вход админа: `POST /api/v1/auth/login` с `{"email":"admin@sitora.local","password":"<SEED_ADMIN_PASSWORD>"}`.
- PostgreSQL снаружи доступен на `localhost:5433` (пользователь и база `sitora`).

## Запуск без Docker

Нужны .NET 10 SDK и PostgreSQL 18.

```bash
export ConnectionStrings__Default="Host=localhost;Port=5432;Database=sitora;Username=...;Password=..."
export Jwt__Key="<не короче 32 символов>"
export SEED_ADMIN_PASSWORD="<пароль админа>"
dotnet run --project src/Sitora.WebApi
```

Миграции применяются при старте. Новая миграция:

```bash
dotnet tool restore
dotnet dotnet-ef migrations add <Имя> --project src/Sitora.Infrastructure --output-dir Data/Migrations
```

## Тесты

```bash
dotnet test
```

По умолчанию тесты поднимают PostgreSQL 18 через Testcontainers (нужен запущенный Docker).
Без Docker задайте строку подключения к своему серверу — тесты создадут временную базу и удалят её:

```bash
export SITORA_TEST_DB="Host=localhost;Port=5432;Username=postgres;Password=..."
dotnet test
```

## Переменные окружения

| Переменная | Назначение |
| --- | --- |
| `ConnectionStrings__Default` | строка подключения к PostgreSQL |
| `Jwt__Key` | ключ подписи JWT, не короче 32 символов |
| `ASPNETCORE_ENVIRONMENT` | `Development`, `Staging` или `Production` |
| `SEED_ADMIN_PASSWORD`, `SEED_MANAGER_PASSWORD`, `SEED_ACCOUNTANT_PASSWORD`, `SEED_CONTENT_PASSWORD` | пароли пользователей сидера (только Development) |

В docker-compose эти значения берутся из `.env` (`POSTGRES_PASSWORD`, `JWT_KEY`, `SEED_*`).

## Что уже есть в API

| Метод и путь | Доступ |
| --- | --- |
| `POST /api/v1/auth/login`, `/refresh`, `/logout`, `GET /auth/me` | все / вошедшие |
| `GET /api/v1/buildings`, `/buildings/{id}`, `/buildings/{id}/floors` | гость |
| `GET /api/v1/floors/{id}/apartments`, `/apartments/{id}` | гость |
| `POST`, `PUT /api/v1/buildings`, `/floors`, `/apartments` | Content, Admin |
| `PATCH /api/v1/apartments/{id}/on-request` | Content, Admin |
| `POST /api/v1/apartments/import?buildingId=…` (файл `.xlsx`) | Content, Admin |
| `POST /api/v1/leads` | гость |

Формат Excel для импорта: первая строка — заголовки, столбцы A–E: этаж, номер квартиры, комнат (0 — студия), площадь, цена за м².

В Development сидер создаёт дом «Sitora Residence»: коммерческий этаж 0 и 16 жилых этажей по 8 квартир.

## Структура

```
src/Sitora.Domain          сущности, перечисления, DTO, интерфейсы, исключения
src/Sitora.Infrastructure  EF Core, Identity, сервисы, интеграции, JWT
src/Sitora.WebApi          контроллеры, middleware, валидаторы, Program.cs
tests/Sitora.Tests         интеграционные тесты
```
