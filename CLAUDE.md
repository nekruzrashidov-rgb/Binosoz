# Binosoz — Платформа для застройщика

## Что это
SaaS-платформа для продажи квартир в новостройках. Три клиента на одном backend:
1. **Публичный сайт** — 3D-модель дома, каталог квартир, калькулятор рассрочки, заявка.
2. **CRM для сотрудников** — лиды, брони, сделки, платежи, документы, чат.
3. **Личный кабинет покупателя** — свои сделки, график платежей, документы, чат с менеджером.

## Стек
- **Backend:** .NET 10 LTS, ASP.NET Core Web API, PostgreSQL 18, EF Core 10 (Npgsql)
- **Auth:** ASP.NET Core Identity + JWT (access 15 мин, refresh 30 дней, ротация)
- **Фоновые задачи:** Hangfire (PostgreSQL-хранилище, схема `hangfire`)
- **Чат:** SignalR (хаб `/hubs/chat`)
- **PDF:** QuestPDF (Community-лицензия)
- **Excel:** ClosedXML (импорт квартир)
- **Валидация:** FluentValidation
- **Логи:** Serilog
- **API-документация:** Swagger (OpenAPI, отключён в Production)
- **Тесты:** xUnit + Testcontainers (postgres:18)
- **Frontend:** Vanilla JavaScript (без сборщика), HTML/CSS статика
- **Деплой:** Docker Compose + Caddy (reverse proxy)
- **CI:** GitHub Actions (`.github/workflows/ci.yml`)

## Структура проекта
```
Binosoz/
├── CLAUDE.md                  ← этот файл
├── .env / .env.example        ← переменные окружения (секреты только здесь)
├── docker-compose.yml         ← dev
├── docker-compose.prod.yml    ← prod
├── docs/
│   ├── TZ.md                  ← полное техзадание
│   ├── DECISIONS.md           ← все принятые решения с обоснованием
│   ├── RESULT.md              ← что реализовано (по ролям и этапам)
│   ├── API_CONTRACT.md        ← все эндпоинты API
│   ├── SECURITY.md            ← модель угроз и меры защиты
│   └── DEPLOY.md              ← инструкция по деплою
├── backend/
│   ├── Sitora.sln
│   ├── src/
│   │   ├── Sitora.Domain/         ← ядро: сущности, DTO, интерфейсы, исключения
│   │   │   ├── Entities/          ← Apartment, AppUser, AuditLog, Booking, Building,
│   │   │   │                         ChatMessage, ConstructionStage, Customer, Deal,
│   │   │   │                         Document, EmailCode, ExchangeRate, Floor, Lead,
│   │   │   │                         Notification, Organization, Payment, PaymentPlan,
│   │   │   │                         RefreshToken, SmsCode
│   │   │   ├── Enums/             ← ApartmentStatus, BookingStatus, DealStage,
│   │   │   │                         LeadStatus, Currency, UserRole
│   │   │   ├── DTOs/              ← запросы и ответы API
│   │   │   ├── Interfaces/        ← IBookingService, IDealService, IPaymentService,
│   │   │   │                         ISmsSender, INotificationSender, IFileStorage,
│   │   │   │                         IRateProvider, IPdfGenerator, IAuditService
│   │   │   └── Exceptions/        ← NotFoundException, ConflictException,
│   │   │                             ForbiddenException, BusinessRuleException,
│   │   │                             UnauthorizedException
│   │   ├── Sitora.Infrastructure/ ← реализация: база, сервисы, интеграции
│   │   │   ├── Data/
│   │   │   │   ├── AppDbContext.cs
│   │   │   │   ├── Configurations/ ← EF конфигурации сущностей
│   │   │   │   ├── Migrations/     ← 7 миграций EF Core
│   │   │   │   └── Seed/           ← сидер (Development-данные)
│   │   │   ├── Services/           ← вся бизнес-логика:
│   │   │   │   AuthService.cs      (675 строк: email OTP, SMS, JWT, refresh, lockout)
│   │   │   │   BookingService.cs   (бронь: partial unique index + FOR UPDATE)
│   │   │   │   CatalogService.cs   (дома, этажи, квартиры, импорт Excel)
│   │   │   │   ChatService.cs      (история, SignalR)
│   │   │   │   CrmService.cs       (пайплайн, конверсия лида)
│   │   │   │   DealService.cs      (сделки, скидки, отмена)
│   │   │   │   DocumentService.cs  (загрузка, подписанные ссылки)
│   │   │   │   PaymentService.cs   (платежи, сторно, пересчёт по курсу)
│   │   │   │   StatementService.cs (акт сверки, PDF)
│   │   │   │   ...
│   │   │   ├── Integrations/       ← Telegram (заглушка→лог), SMS (заглушка),
│   │   │   │                          курс НБТ (заглушка), QuestPDF, файлы (диск)
│   │   │   ├── Jobs/               ← ExpireBookingsJob, UpdateRatesJob (Hangfire)
│   │   │   └── Security/           ← JwtTokenService, EncryptionService (AES-256-GCM)
│   │   └── Sitora.WebApi/          ← HTTP-слой
│   │       ├── Controllers/        ← 22 тонких контроллера (принял DTO → сервис → ответ)
│   │       ├── Hubs/ChatHub.cs     ← SignalR: JoinDeal, SendMessage, messageReceived
│   │       ├── Middleware/         ← DomainExceptionHandler, SecurityHeadersMiddleware,
│   │       │                          ImpersonationAuditMiddleware
│   │       ├── Validators/         ← FluentValidation
│   │       └── Program.cs          ← точка входа, DI, пайплайн
│   └── tests/
│       └── Sitora.Tests/           ← 163 теста, Testcontainers, покрывают:
│                                      auth, catalog, chat, payments, sales, security
└── frontend/
    ├── index.html             ← точка входа (сейчас SPA, будет multi-page)
    ├── css/styles.css         ← 36 КБ, design tokens, компоненты
    └── js/
        ├── config.js          ← API_BASE, ORG_SLUG, контакты
        ├── i18n.js            ← переводы RU/TJ (62 КБ)
        ├── ui.js              ← h() DOM-builder, иконки, модалки, тосты
        ├── api.js             ← все вызовы API, токены в localStorage
        ├── app.js             ← шапка, роутинг (#hash), тема, язык
        ├── site.js            ← публичный сайт: каталог, калькулятор, контакты
        ├── house3d.js         ← 3D-модель дома (Three.js, 45 КБ)
        ├── cabinet.js         ← кабинет покупателя + login (все шаги OTP/SMS)
        ├── crm.js             ← CRM: лиды, сделки, пайплайн, отчёты (55 КБ)
        ├── platform.js        ← раздел PlatformOwner
        ├── panels.js          ← боковые панели (детали квартиры, бронь)
        ├── mock.js            ← demo-режим без backend (57 КБ)
        └── ai.js              ← AI-ассистент (3.5 КБ)
```

## Роли
| Роль | Доступ |
|------|--------|
| Admin | Всё, включая пользователей, аудит, настройки |
| Manager | Лиды, брони, сделки, клиенты, документы, чат |
| Accountant | Платежи, сторно, акты сверки, курсы, отчёты |
| Content | Каталог (дома/этажи/квартиры), ход стройки, фото |
| Customer | Только свои сделки, платежи, документы, чат |
| PlatformOwner | Управление организациями (мультиарендность) |
| Гость | Каталог (read-only) + отправка заявки |

## Бизнес-правила (нельзя нарушать)
- **Деньги** — только `decimal(18,2)` + код валюты (TJS или USD). Никогда `float/double`.
- **Платежи** — не удалять, только сторно (`ReversalOfId`). Сторно одно, сторно сторно нельзя.
- **Бронь** — одна активная на квартиру: partial unique index `IX_Bookings_ApartmentId_Active` + `SELECT FOR UPDATE` в транзакции. Двойная бронь → 409.
- **Статус квартиры** — меняют только `Book()`, `ReleaseBooking()`, `Sell()`, `SetOnRequest()` на сущности. Через PUT статус не меняется.
- **AuditLog** — любое изменение статуса, цены, платежа, документа пишется в той же транзакции.
- **Права** — проверяются на сервере в каждом запросе. Чужая сделка → 404 (не 403).
- **Секреты** — только из переменных окружения (`.env`). В Development — ещё из `dotnet user-secrets`. В репозиторий значения секретов не коммитить.
- **Ошибки** — текст на русском языке.
- **После каждой задачи** — обязательно `dotnet build` и `dotnet test`.

## API
- Префикс: `/api/v1`
- Формат ошибок: Problem Details (RFC 7807)
- Язык ответов: по заголовку `Accept-Language` (ru, tg)
- Пагинация: `page` (от 1), `pageSize` (по умолчанию 20, макс 100)
- Все эндпоинты: `docs/API_CONTRACT.md`

## Auth-флоу
- **Сотрудники:** `POST /auth/email/send` → `POST /auth/email/verify` → (опционально) `POST /auth/second-factor`
- **Покупатели:** `POST /auth/sms/send` → `POST /auth/sms/verify`
- **Токены:** access (JWT, 15 мин) + refresh (64 байта, SHA-256 в БД, 30 дней, ротация)
- **Dev-режим:** фиксированный код `123456` для тестирования (только `IsDevelopment()`)

## Конфигурация (переменные окружения)
```
ConnectionStrings__DefaultConnection=...
Jwt__Key=...                    # ≥32 байта, base64
Encryption__Key=...             # 32 байта, base64 (для паспортных данных)
Telegram__BotToken=...
Telegram__ChatId=...
Auth__DevFixedCodeForAnyEmail=true   # только Development
Hangfire__ServerEnabled=true
Storage__RootPath=/app/files
```

## Что сейчас заглушки (нужно заменить перед продакшеном)
- **SMS** — `SmsStubSender`: пишет код в лог. Заменить на реального провайдера.
- **Telegram** — `TelegramStubSender`: пишет в лог. Заменить на `TelegramBotClient`.
- **Курс НБТ** — `NbtRateStubProvider`: ничего не загружает. Заменить на парсинг nbt.tj.
- **Файлы** — хранятся на диске (`/app/files`). Заменить на MinIO/S3 для продакшена.

## Деплой
- Dev: `docker compose up -d` (PostgreSQL на порту 5433)
- Prod: `docker compose -f docker-compose.prod.yml up -d`
- Миграции: в Dev/Staging — при старте; в Prod — только `dotnet run --migrate`
- Подробно: `docs/DEPLOY.md`

## Что планируется (следующие шаги)
- Мультистраничный frontend: `login.html`, `cabinet.html`, `crm.html`, `platform.html`
- Backend отдаёт frontend из `wwwroot/` (без Vercel)
- Реальный SMS-провайдер (Tcell/Megafon Таджикистан)
- Реальный курс НБТ
- Хранение файлов в MinIO
- Онлайн-оплата (Alif, Корти Миллӣ)
