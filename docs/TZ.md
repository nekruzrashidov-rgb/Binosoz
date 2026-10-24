# START: собери backend платформы для застройщика с нуля

Ты (Claude Code) работаешь в пустой папке. Твоя задача: создать backend на .NET для платформы застройщика (сайт с 3D-домом, CRM, личный кабинет покупателя). Прочитай весь файл до конца, потом работай по правилам ниже.

## Как работать

1. Сначала напиши короткий план этапа (5–10 пунктов) и только потом правь код.
2. Работай этапами из раздела «Этапы». После каждого этапа:
   - запусти `dotnet build` и `dotnet test`, исправь ошибки;
   - сделай `git commit` с понятным сообщением на русском;
   - выведи итог: что готово, как запустить, что проверить вручную;
   - **остановись и жди слова «продолжай»**. Следующий этап сам не начинай.
3. Не придумывай ключи, пароли и данные банков. Всё внешнее (SMS, Telegram, банк) закрой интерфейсом и сделай заглушку, которая пишет в лог. Секреты бери из переменных окружения, в репозиторий их не клади.
4. Если решение неочевидно или в ТЗ есть пробел, выбери разумный вариант, запиши его в `docs/DECISIONS.md` одной строкой и иди дальше. Спрашивай меня только если без ответа нельзя продолжать.
5. Пиши код простым и читаемым. Комментарии только там, где причина неочевидна. Сообщения об ошибках для пользователя на русском.

## Что за продукт

Единый API для трёх клиентов:
- **Сайт**: 3D-дом, выбор этажа и квартиры, заявка и бронь.
- **CRM** для менеджеров: заявки, клиенты, сделки по этапам, платежи.
- **Личный кабинет покупателя**: график и платежи, документы, ход стройки, чат с менеджером.

Главное правило: бронь на сайте сразу попадает в CRM, а платежи и документы из CRM сразу видны покупателю.

Не входит в MVP: онлайн-оплата через банк, VR, мобильное приложение, несколько застройщиков в одной системе.

## Стек

- .NET 10 (LTS, актуальная стабильная версия). Если SDK не установлен, остановись и скажи мне, не понижай версию сам. .NET 11 и другие предварительные версии не используй.
- ASP.NET Core Web API с контроллерами.
- PostgreSQL 18 (образ `postgres:18`; PostgreSQL 19 пока в RC, не использовать), EF Core (Npgsql), миграции. В образах PostgreSQL 18 том данных монтируется в `/var/lib/postgresql`, а не в `/var/lib/postgresql/data`: учти это в docker-compose.yml.
- ASP.NET Core Identity, JWT (access 15 минут + refresh-токен).
- FluentValidation, Serilog, Swagger (OpenAPI), Hangfire (PostgreSQL storage), SignalR, QuestPDF.
- Тесты: xUnit, Testcontainers для PostgreSQL.
- Docker Compose для запуска WebApi + PostgreSQL.
- Все NuGet-пакеты ставь последних стабильных версий, предварительные (preview, rc) не бери.

## Структура решения

Три проекта плюс тесты. Отдельного проекта Application и репозиториев нет: бизнес-логика лежит в сервисах Infrastructure и работает с `AppDbContext` напрямую.

```
Sitora/
├── Sitora.sln
├── docker-compose.yml
├── README.md
├── CLAUDE.md                     правила проекта (создай из раздела «Правила проекта»)
├── .gitignore
├── docs/
│   ├── TZ.md                     это ТЗ (если файла нет, создай его из этого START.md)
│   └── DECISIONS.md              принятые решения, по одной строке
├── src/
│   ├── Sitora.Domain/            ядро, ни от чего не зависит
│   │   ├── Entities/             Building, Floor, Apartment, Lead, Customer, Booking, Deal,
│   │   │                         PaymentPlan, PaymentScheduleItem, Payment, ExchangeRate,
│   │   │                         Document, ConstructionStage, ConstructionPhoto,
│   │   │                         ChatMessage, Notification, AuditLog, AppUser
│   │   ├── Enums/                ApartmentStatus, BookingStatus, DealStage, LeadStatus,
│   │   │                         Currency, DocumentType, UserRole
│   │   ├── DTOs/                 запросы и ответы API по папкам модулей:
│   │   │                         Auth, Catalog, Sales, Payments, Documents,
│   │   │                         Construction, Chat, Common (PagedResult и т.п.)
│   │   ├── Interfaces/           IAuthService, ICatalogService, ILeadService, IBookingService,
│   │   │                         IDealService, IPaymentService, IDocumentService,
│   │   │                         IConstructionService, IChatService, ICrmService, IAuditService
│   │   │                         и внешние: ISmsSender, INotificationSender,
│   │   │                         IFileStorage, IRateProvider, IPdfGenerator
│   │   └── Exceptions/           NotFoundException, ConflictException,
│   │                             ForbiddenException, BusinessRuleException
│   │
│   ├── Sitora.Infrastructure/    реализация: база, сервисы, интеграции
│   │   ├── Data/
│   │   │   ├── AppDbContext.cs
│   │   │   ├── Configurations/   настройки таблиц и индексов, по файлу на сущность
│   │   │   ├── Migrations/
│   │   │   └── Seed/             сидер для Development
│   │   ├── Services/             AuthService, CatalogService, LeadService, BookingService,
│   │   │                         DealService, PaymentService, PaymentScheduleCalculator,
│   │   │                         StatementService, DocumentService, ConstructionService,
│   │   │                         ChatService, CrmService, AuditService
│   │   ├── Integrations/         TelegramNotificationSender, SmsSender (заглушка),
│   │   │                         NbtRateProvider, LocalFileStorage, QuestPdfGenerator
│   │   ├── Jobs/                 ExpireBookingsJob, UpdateRatesJob (Hangfire)
│   │   ├── Security/             JwtTokenService, EncryptionService
│   │   └── DependencyInjection.cs  регистрация всего одним вызовом AddInfrastructure()
│   │
│   └── Sitora.WebApi/            вход в систему, только HTTP
│       ├── Controllers/          Auth, Buildings, Floors, Apartments, Leads, Bookings,
│       │                         Customers, Deals, Crm, Payments, Rates, Documents,
│       │                         Construction, Chat, Users, Audit
│       ├── Hubs/                 ChatHub (SignalR)
│       ├── Middleware/           обработка ошибок (Exception → Problem Details), язык запроса
│       ├── Validators/           FluentValidation для DTO
│       ├── Program.cs
│       ├── appsettings.json
│       └── Dockerfile
└── tests/
    └── Sitora.Tests/             бронь, график, курс, права
```

**Зависимости между проектами**
- Domain не знает ни про базу, ни про HTTP и не ссылается на другие проекты.
- Infrastructure ссылается на Domain и реализует его интерфейсы.
- WebApi ссылается на Domain и Infrastructure. Контроллер тонкий: принял DTO, вызвал сервис, вернул результат. Бизнес-логики в контроллерах нет.
- Исключения домена WebApi превращает в ответы 404, 409, 403 и 422 в одном middleware.
- Domain не должен подключать NuGet-пакеты, кроме самых базовых. Валидаторы живут в WebApi.

**Пример потока (бронь).** `BookingsController` принимает запрос и вызывает `IBookingService`. `BookingService` открывает транзакцию через `AppDbContext`, проверяет квартиру, создаёт бронь, вызывает `INotificationSender` и пишет запись через `IAuditService`.

## Роли

| Роль | Кто | Что может |
| --- | --- | --- |
| Admin | владелец | всё, пользователи, аудит, настройки |
| Manager | менеджер по продажам | клиенты, заявки, бронь, сделки, документы, чат |
| Accountant | бухгалтер | ввод и сторно платежей, акты сверки, отчёты |
| Content | контент-менеджер | дома, этажи, квартиры, цены, ход стройки, фото |
| Customer | покупатель | только свои сделки, платежи, документы, чат |
| Гость | без входа | каталог и отправка заявки |

Сотрудники входят по почте и паролю. Покупатели входят по телефону и коду из SMS. Проверка «чужая ли это сделка» делается на сервере в каждом запросе: чужой `dealId` отдаёт 404.

## Модель данных

У всех таблиц есть `Id` (Guid), `CreatedAt`, `UpdatedAt`. Деньги: `decimal(18,2)` плюс код валюты (`TJS` или `USD`).

| Таблица | Ключевые поля | Связи |
| --- | --- | --- |
| Users | Email, Phone, PasswordHash, Role, IsActive | |
| Buildings | Name (ru и tj), Address, FloorsCount, Description, ModelUrl | много Floors |
| Floors | BuildingId, Number (0 = коммерческий этаж), PlanImageUrl | много Apartments |
| Apartments | FloorId, Number, Rooms, Area, PricePerM2, Price, Status (Free, Booked, Sold, OnRequest) | Bookings, Deals |
| Customers | FullName, Phone, Email, PassportData (шифровать), UserId | Deals |
| Leads | Name, Phone, ApartmentId?, Source, Status, ManagerId | |
| Bookings | ApartmentId, CustomerId или LeadId, ExpiresAt, Status (Active, Expired, Cancelled, Converted) | |
| Deals | ApartmentId, CustomerId, ManagerId, Stage, Price, Discount, DiscountReason, Currency | PaymentPlan |
| PaymentPlans | DealId, DownPaymentPct, Months, PaymentDay, StartDate | ScheduleItems |
| PaymentScheduleItems | PlanId, DueDate, AmountDue, Status | |
| Payments | DealId, Amount, Currency, RateUsed, PaidAt, Method, ReversalOfId? | |
| ExchangeRates | Date, Currency, RateToTjs | |
| Documents | DealId, Type, FileKey, SignedAt | |
| ConstructionStages | BuildingId, Name, Percent, UpdatedAt | Photos |
| ConstructionPhotos | StageId, FileKey, TakenAt | |
| ChatMessages | DealId, SenderId, Text, ReadAt | |
| Notifications | UserId, Channel, Text, SentAt | |
| AuditLog | UserId, Entity, EntityId, Action, Before, After, At | |

Индексы: уникальный `(FloorId, Number)` для квартиры; **частичный уникальный индекс на `Bookings(ApartmentId)` где `Status = 'Active'`**; индекс `Payments(DealId, PaidAt)`.

## Бизнес-правила

1. **Статусы квартиры.** Free → Booked → Sold. Из Booked можно вернуться в Free (бронь истекла или отменена). OnRequest ставят только Content и Admin. Статус напрямую не правится, его меняют только операции брони и сделки.
2. **Бронь.** 48 часов. Одна активная бронь на квартиру: транзакция плюс частичный уникальный индекс, чтобы два покупателя не забронировали одну квартиру одновременно. Hangfire раз в 5 минут снимает просроченные брони. Продлить бронь может только Manager.
3. **Сделка.** Этапы: Заявка, Показ, Бронь, Договор, Оплата, Завершена. Вперёд свободно, назад только с причиной и записью в аудит. На этапе «Договор» квартира становится Sold.
4. **Цена.** Цена = площадь × цена за м². При создании сделки цена копируется в сделку и больше не меняется при смене прайса. Скидку задаёт Manager с обязательной причиной.
5. **График платежей.** Первоначальный взнос в процентах, срок в месяцах, день платежа. Месячный платёж = (цена − взнос) ÷ месяцы, округление до 1 сомони, последний платёж забирает остаток округлений. Платёж закрывает самые старые неоплаченные пункты графика.
6. **Долларовый эквивалент.** Если договор в USD, сумма в сомони = сумма USD × курс НБТ на дату оплаты. Курс грузится раз в день фоновой задачей через интерфейс `IRateProvider`. Сначала сделай заглушку и ручной ввод курса бухгалтером. Курс платежа сохраняй в `RateUsed`.
7. **Платежи не удаляются.** Ошибку исправляют сторно: создаётся обратная запись со ссылкой на исходный платёж.
8. **Акт сверки.** PDF на выбранную дату: начислено, оплачено, остаток, список платежей с курсами (QuestPDF).
9. **Аудит.** Каждое изменение статуса, цены, платежа и документа пишется в AuditLog: кто, когда, значения «до» и «после».

## Эндпоинты (`/api/v1`)

Списки с `page` и `pageSize`. Ошибки в формате Problem Details. Язык по `Accept-Language` (ru, tg).

| Модуль | Метод и путь | Доступ |
| --- | --- | --- |
| Auth | POST /auth/login, /auth/refresh, /auth/logout | все |
| Auth | POST /auth/sms/send, /auth/sms/verify | покупатель |
| Catalog | GET /buildings, /buildings/{id}, /buildings/{id}/floors, /floors/{id}/apartments, /apartments/{id} | гость |
| Catalog | POST и PUT /buildings, /floors, /apartments | Content, Admin |
| Catalog | POST /apartments/import (Excel) | Content, Admin |
| Sales | POST /leads | гость |
| Sales | GET /leads, PATCH /leads/{id} | Manager |
| Sales | POST /apartments/{id}/bookings | гость, Manager |
| Sales | POST /bookings/{id}/extend, /bookings/{id}/cancel | Manager |
| Sales | GET и POST /customers | Manager |
| Sales | GET и POST /deals, PATCH /deals/{id}/stage | Manager |
| Sales | GET /crm/summary, /crm/pipeline | Manager, Admin |
| Payments | POST /deals/{id}/plan | Manager |
| Payments | GET /deals/{id}/schedule | владелец, сотрудник |
| Payments | POST /deals/{id}/payments, POST /payments/{id}/reverse | Accountant, Admin |
| Payments | GET /deals/{id}/statement.pdf | владелец, сотрудник |
| Payments | GET /rates/current, PUT /rates | все, Accountant |
| Documents | GET и POST /deals/{id}/documents, GET /documents/{id}/download | владелец, сотрудник |
| Construction | GET /buildings/{id}/stages, PUT /stages/{id}, POST /stages/{id}/photos | покупатель, Content |
| Chat | GET и POST /deals/{id}/messages, хаб SignalR /hubs/chat | владелец, сотрудник |
| Admin | GET /audit, GET и POST /users | Admin |

## Начальные данные (seed)

Для разработки добавь сидер, который запускается только в Development:

- Дом «Sitora Residence», Душанбе. Этаж 0 (коммерческий) и этажи 1–16.
- На этажах 1–16 по 8 квартир. Типы по порядку номеров на этаже: 1к 46 м², 2к 68 м², 3к 92 м², 2к 70 м², студия 34 м², 2к 66 м², 1к 48 м², студия 36 м².
- Цена за м² = 9200 + номер этажа × 95 (сомони). Цена квартиры = площадь × цена за м², округление до 100.
- Статусы случайные, но воспроизводимые (фиксированный seed): чем ниже этаж, тем больше проданных.
- Админ `admin@sitora.local`; пароль возьми из переменной `SEED_ADMIN_PASSWORD`, в коде пароля быть не должно.
- По одному пользователю на роли Manager, Accountant, Content, пароли тоже из переменных окружения.
- 10 клиентов с выдуманными именами, 5 сделок на разных этапах, график и платежи для одной сделки.

## Нефункциональные требования

- Только HTTPS в продакшене. Лимиты запросов на вход и отправку SMS (например, 5 кодов в час на номер). CORS только для своих доменов.
- Паспортные данные шифруются, доступ только Manager и Admin.
- Каталог отвечает быстрее 300 мс для 95% запросов, кэш 30 секунд со сбросом при смене статуса.
- Serilog, эндпоинт `/health`.
- Окружения: Development, Staging, Production. Миграции EF Core.
- Тесты обязательны для: брони (включая два одновременных запроса), расчёта графика, перерасчёта по курсу, проверки владельца сделки, прав по ролям.

## Этапы

### Этап 1. Основа
Решение и три проекта (Domain, Infrastructure, WebApi) плюс тесты, docker-compose (WebApi + PostgreSQL 18), EF Core + миграции, Identity, роли, JWT с refresh, `POST /auth/login`, Serilog, Swagger, `/health`, `.gitignore`, README, CLAUDE.md, docs/DECISIONS.md, сидер пользователей.
**Готово, когда:** `docker compose up` поднимает WebApi, вход админа отдаёт JWT, Swagger открывается, тест входа проходит.

### Этап 2. Каталог и заявки
Сущности Building, Floor, Apartment, публичные эндпоинты каталога (со счётчиком свободных квартир на этаже), CRUD для Content, импорт квартир из Excel, `POST /leads`, интерфейс `INotificationSender` с заглушкой Telegram, полный сидер дома.
**Готово, когда:** каталог отдаёт 16 этажей и 128 квартир со статусами, заявка с сайта создаётся и пишется в лог уведомлений.

### Этап 3. Бронь и CRM
Бронь с защитой от двойной брони, Hangfire-задача снятия просроченных броней, клиенты, сделки с этапами, `/crm/summary` и `/crm/pipeline`, аудит.
**Готово, когда:** тест одновременной брони проходит, бронь с сайта видна в `/crm/pipeline`, статусы квартир меняются только через операции.

### Этап 4. Кабинет покупателя
Вход по SMS (заглушка `ISmsSender`), график платежей, ввод и сторно платежей, курс (заглушка `IRateProvider` + ручной ввод), документы, акт сверки PDF.
**Готово, когда:** покупатель видит только свои сделки, чужой `dealId` даёт 404, акт сверки скачивается и сходится с платежами.

### Этап 5. Стройка и чат
Этапы стройки с процентами и фото, сообщения по сделке, SignalR-хаб.
**Готово, когда:** менеджер и покупатель обмениваются сообщениями в реальном времени, покупатель видит проценты по этапам.

### Этап 6. Запуск
Проверка безопасности по списку выше, лимиты, бэкап `pg_dump` в скрипте, Dockerfile для продакшена, инструкция деплоя на VPS, финальный README.
**Готово, когда:** все тесты зелёные, инструкция деплоя проверена на чистой машине, список известных ограничений записан в README.

## Правила проекта (создай из них файл CLAUDE.md)

```markdown
# Проект: backend платформы для застройщика
Полное ТЗ: docs/TZ.md. Решения: docs/DECISIONS.md.
Стек: .NET 10 LTS, ASP.NET Core Web API, PostgreSQL 18, EF Core, Identity + JWT.
Проекты: Sitora.Domain, Sitora.Infrastructure, Sitora.WebApi, tests/Sitora.Tests. Бизнес-логика в Infrastructure/Services, контроллеры тонкие.
- Деньги только decimal(18,2) + код валюты. Платежи не удаляются, только сторно.
- Одна активная бронь на квартиру (частичный уникальный индекс + транзакция).
- Статус квартиры меняют только операции брони и сделки.
- Любое изменение статуса, цены, платежа, документа пишется в AuditLog.
- Права проверяются на сервере в каждом запросе; чужая сделка даёт 404.
- Секреты только из переменных окружения.
- После каждой задачи: dotnet build и dotnet test.
- Сообщения об ошибках на русском.
```

## Начни

Выполни этап 1. Сначала покажи план, потом делай и по итогам остановись.
