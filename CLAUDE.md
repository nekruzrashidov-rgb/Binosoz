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
│   │   │   │   ├── Migrations/     ← миграции EF Core (24)
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
│   │       ├── Controllers/        ← 37 тонких контроллеров (принял DTO → сервис → ответ)
│   │       ├── Hubs/ChatHub.cs     ← SignalR: JoinDeal, SendMessage, messageReceived
│   │       ├── Middleware/         ← DomainExceptionHandler, SecurityHeadersMiddleware,
│   │       │                          ImpersonationAuditMiddleware
│   │       ├── Validators/         ← FluentValidation
│   │       └── Program.cs          ← точка входа, DI, пайплайн
│   └── tests/
│       └── Sitora.Tests/           ← 380 тестов, Testcontainers, покрывают:
│                                      auth, catalog, chat, payments, sales, security
└── frontend/                   ← vanilla JS без сборщика; сайт отдаёт сам API (wwwroot) и копия на Vercel
    ├── index.html, apartments.html, about.html, gallery.html, contact.html   ← публичные страницы ЖК
    ├── login.html, register.html, accept-invite.html, cabinet.html, crm.html, platform.html, payment.html,
    │   404.html, offline.html     ← вход, кабинет, CRM, платформа, тестовый банк, ошибки
    ├── manifest.json, sw.js       ← PWA (Service Worker «сначала сеть»), robots.txt, sitemap.xml
    ├── img/gallery/               ← виды Душанбе с Wikimedia Commons (WebP 400/800/1200), авторы — CREDITS.md
    ├── css/styles.css             ← 171 КБ: токены (:root и [data-theme='dark']), все компоненты
    └── js/
        ├── tanzimot-api-url.js        ← config: API_BASE ('/api/v1'; Vercel → Render), ORG_SLUG, CONTACT, INSTALLMENT
        ├── i18n/ru.js, tg.js, en.js   ← тексты (по 1549 ключей); ru — на каждой странице и запасной
        ├── tarjumaho-ru-tg-en.js      ← S.t(), S.initLang()/S.setLang() (грузит tg/en по требованию), S.bus
        ├── komponentho-modal-toast-tabs.js ← h() DOM-builder, иконки, модалки, тосты
        ├── zaprosho-ba-backend.js     ← все вызовы API, токены (localStorage sitora.auth)
        ├── routing-va-navigatsiya.js  ← запуск страницы: шапка, тема, язык, мобильное меню, reveal
        ├── sahifai-asosi-script.js    ← публичный сайт: главная, каталог, калькулятор, контакты, футер
        ├── modeli-3d-bino-script.js   ← 3D-дом каталога (Three.js r128, грузится лениво; единственный 3D-движок)
        ├── galereya-script.js, dar-borai-mo-script.js ← /gallery (фото Душанбе + планировки), /about
        ├── kabineti-xaridor-script.js ← вход и кабинет покупателя
        ├── crm-script.js, panelho-crm-script.js ← CRM и её боковые панели
        ├── superadmin-platform-script.js ← раздел PlatformOwner
        ├── pardoxt-script.js          ← payment.html (тестовый банк)
        ├── ai-assistent.js, pwa-sabt.js ← AI-ассистент, регистрация Service Worker
        └── namunavi-malumot-mock.js   ← demo-бэкенд; сейчас ни одна страница его не подключает
```

## Frontend: правила
- **CSP** `script-src 'self' https://cdnjs.cloudflare.com`: никаких inline-скриптов и `onclick=` в HTML; все `<script src>` с `defer` (тест `FrontendTests`).
- **Цвета — только токены**: `--ink/--ink-2/--muted` (текст), `--bg/--surface/--surface-2` (фоны), `--accent` (фон кнопок),
  `--accent-text` (медный текст и ссылки), `--accent-2` (градиенты), статусы `--good/--warn/--bad/--info` и их `-soft`.
  Полупрозрачность — `color-mix(in srgb, var(--accent) 20%, transparent)`. Хардкод цвета текста ломает тёмную тему.
- **Тёмные секции** на светлом сайте (hero главной, рассрочка, «Остались вопросы?», футер, 3D-сцена, итог калькулятора)
  переопределяют `--ink/--ink-2/--muted` локально — компоненты внутри сами светлые. Новую тёмную секцию добавлять в этот список.
- **Тёмная тема**: `[data-theme='dark']` меняет токены; проверка — `scripts/check-contrast.js` (0 текстов ниже AA в обеих темах).
- **Честные данные**: цифры, цены, свободные квартиры — только из API; `DEFAULT_*` в config — только для demo; пустой API —
  карточка «Продажи скоро откроются». Ничего не выдумывать (даты, отзывы, фото).
- **Переводы**: новый ключ — во все три файла `js/i18n/*.js`.
- **Фото галереи** — только CC0 / общественное достояние / CC BY / CC BY-SA (без NC и ND), под каждым «Фото: автор, лицензия»
  со ссылкой на страницу файла, запись в `img/gallery/CREDITS.md`. Подписи честные: это виды города, не здания комплекса.

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
- Формат ошибок: Problem Details (RFC 9457) с `code` (snake_case) и `traceId`; тексты и коды — `Sitora.WebApi/Errors/ErrorCatalog.cs`.
  Новая ошибка домена — строка в каталоге (ru, code, tg).
- Язык ответов и ошибок: по заголовку `Accept-Language` (ru, tg)
- Пагинация: `page` (от 1), `pageSize` (по умолчанию 20, макс 100); вне границ — тихо исправляется
- Публичный каталог кэшируется (Output Cache, 15–30 с); изменения каталога сбрасывают тег `catalog`
- Все эндпоинты: `docs/API_CONTRACT.md`

## Auth-флоу
- **Сотрудники:** `POST /auth/email/send` → `POST /auth/email/verify` → (опционально) `POST /auth/second-factor`
- **Покупатели:** `POST /auth/sms/send` → `POST /auth/sms/verify`
- **Токены:** access (JWT, 15 мин) + refresh (64 байта, SHA-256 в БД, 30 дней, ротация)
- **Refresh-cookie:** `sitora_rt` (HttpOnly, SameSite=Strict, `Path=/api/v1/auth`), режим `Auth__RefreshCookie__Mode` = Dual|Cookie|Body
- **Dev-режим:** фиксированный код `123456` для тестирования (только `IsDevelopment()`); почты задаются в user-secrets:
  `dotnet user-secrets set "Auth:DevFixedCodeEmails:0" "почта" --project src/Sitora.WebApi`

## Конфигурация (переменные окружения)
```
ConnectionStrings__DefaultConnection=...
Jwt__Key=...                    # ≥32 байта, base64
Encryption__Key=...             # 32 байта, base64 (для паспортных данных)
Telegram__BotToken=...
Telegram__ChatId=...
Auth__DevFixedCodeForAnyEmail=true   # только Development
Hangfire__ServerEnabled=true
Storage__Provider=Local          # или S3 (MinIO, R2, B2, AWS): Storage__S3__Endpoint/Bucket/AccessKey/SecretKey/Region
Storage__RootPath=/app/storage
Sentry__Dsn=                     # пусто — ошибки никуда не отправляются
Health__Key=                     # ключ X-Health-Key для /health/ready снаружи
Auth__RefreshCookie__Mode=Dual
Logging__File__Enabled=true      # файлы логов (только VPS; на Render — stdout)
```

## Что сейчас заглушки (нужно заменить перед продакшеном)
- **SMS** — `SmsStubSender`: пишет код в лог. Заменить на реального провайдера.
- **Telegram** — `TelegramStubSender`: пишет в лог. Заменить на `TelegramBotClient`.
- **Курс НБТ** — `NbtRateStubProvider`: ничего не загружает. Заменить на парсинг nbt.tj.
- **Файлы** — `Storage__Provider=Local` (диск) или `S3`. На Render диск временный — нужен S3/R2 или Render Disk (см. `docs/DEPLOY.md`).

## Деплой
- Dev: `docker compose up -d` (PostgreSQL на порту 5433)
- Prod: `docker compose -f docker-compose.prod.yml up -d`
- Миграции: в Dev/Staging — при старте; в Prod — только `dotnet run --migrate`
- Подробно: `docs/DEPLOY.md`

## Что планируется (следующие шаги)
- Реальный SMS-провайдер (Tcell/Megafon Таджикистан)
- Реальный курс НБТ
- Онлайн-оплата (Alif, Корти Миллӣ)
