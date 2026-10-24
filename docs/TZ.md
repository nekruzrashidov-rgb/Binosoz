# ТЗ: backend платформы для застройщика (.NET)

24.10.2026

## 1. Цель и границы MVP

Нужен один backend (REST API на .NET), который обслуживает три клиента: сайт с 3D-моделью дома, CRM для менеджеров и личный кабинет покупателя. Главное правило: бронь на сайте сразу попадает в CRM, а платежи и документы из CRM сразу видны покупателю.

**Входит в MVP**

- Каталог: дома, этажи, квартиры, цены, статусы.
- Заявки с сайта и бронь квартиры на 48 часов.
- Сделки с этапами, клиенты, менеджеры.
- График платежей, ввод платежей, пересчёт по курсу НБТ, акт сверки в PDF.
- Документы, ход стройки, чат покупателя с менеджером.
- Уведомления в Telegram.

**Не входит в MVP**

- Онлайн-оплата через банк (отдельный этап после выбора банка).
- VR, сенсорные экраны, мобильное приложение на Flutter.
- Несколько застройщиков в одной системе (multi-tenant).

**Открытые вопросы к застройщику**

- Один застройщик или платформа для нескольких?
- Нужны ли названия и описания на двух языках (русский и таджикский) в самих данных?
- Через какой банк принимаются платежи сейчас и какой нужен договор: в сомони или в долларовом эквиваленте?

## 2. Стек и архитектура

Модульный монолит: один сервис, внутри чёткие модули. Для одного разработчика это проще, чем микросервисы, и легко разделить позже.

| Что | Выбор |
| --- | --- |
| Платформа | .NET 10 (LTS, актуальная стабильная версия), ASP.NET Core Web API |
| База данных | PostgreSQL 18, доступ через EF Core (Npgsql) |
| Вход и роли | ASP.NET Core Identity, JWT (access 15 минут + refresh-токен) |
| Валидация | FluentValidation |
| Фоновые задачи | Hangfire (снятие просроченной брони, загрузка курса) |
| Чат | SignalR |
| PDF | QuestPDF (акт сверки, график) |
| Файлы | MinIO или диск VPS, доступ по подписанным ссылкам |
| Логи | Serilog, health checks |
| Документация API | Swagger (OpenAPI) |
| Тесты | xUnit, Testcontainers (PostgreSQL в тестах) |
| Развёртывание | Docker Compose на VPS, CI через GitHub Actions |

**Структура решения**

```
Sitora/
├── Sitora.sln
├── docker-compose.yml
├── docs/                       ТЗ, решения
├── src/
│   ├── Sitora.Domain/          ядро, ни от чего не зависит
│   │   ├── Entities/           Building, Floor, Apartment, Lead, Customer, Booking, Deal,
│   │   │                       PaymentPlan, Payment, ExchangeRate, Document, ConstructionStage,
│   │   │                       ChatMessage, Notification, AuditLog, AppUser
│   │   ├── Enums/              ApartmentStatus, BookingStatus, DealStage, LeadStatus, Currency, UserRole
│   │   ├── DTOs/               запросы и ответы API по модулям
│   │   ├── Interfaces/         IBookingService, IDealService, IPaymentService ...
│   │   │                       ISmsSender, INotificationSender, IFileStorage, IRateProvider, IPdfGenerator
│   │   └── Exceptions/         NotFound, Conflict, Forbidden, BusinessRule
│   ├── Sitora.Infrastructure/  реализация: база, сервисы, интеграции
│   │   ├── Data/               AppDbContext, Configurations/, Migrations/, Seed/
│   │   ├── Services/           AuthService, CatalogService, BookingService, DealService,
│   │   │                       PaymentService, StatementService, ChatService, CrmService ...
│   │   ├── Integrations/       Telegram, SMS (заглушка), курс НБТ, файлы, QuestPDF
│   │   ├── Jobs/               ExpireBookingsJob, UpdateRatesJob (Hangfire)
│   │   ├── Security/           JwtTokenService, EncryptionService
│   │   └── DependencyInjection.cs
│   └── Sitora.WebApi/          вход в систему, только HTTP
│       ├── Controllers/        по одному на модуль и сущность
│       ├── Hubs/               ChatHub (SignalR)
│       ├── Middleware/         обработка ошибок, язык запроса
│       ├── Validators/         FluentValidation
│       ├── Program.cs, appsettings.json, Dockerfile
└── tests/
    └── Sitora.Tests/           бронь, график, курс, права
```

**Модули внутри:** Identity, Catalog, Sales (заявки, бронь, сделки), Payments, Documents, Construction, Chat, Notifications.

**Зависимости.** Domain ни на что не ссылается. Infrastructure ссылается на Domain и реализует его интерфейсы. WebApi ссылается на Domain и Infrastructure; контроллер тонкий: принял DTO, вызвал сервис, вернул результат.

**Упрощения.** Отдельного проекта Application и репозиториев нет: сервисы в Infrastructure/Services работают с AppDbContext напрямую. DTO лежат в Domain, валидаторы в WebApi.

**Версии на 24.10.2026.** .NET 10 — актуальная стабильная LTS-версия (.NET 11 пока release candidate). PostgreSQL 18 — последняя стабильная (PostgreSQL 19 пока RC1). NuGet-пакеты — последние стабильные, без preview.

## 3. Роли и права

Пять ролей для сотрудников и одна для покупателя. Покупатель видит только свои сделки, платежи и документы. Проверка владельца делается на сервере в каждом запросе, не на клиенте.

| Роль | Кто | Что может |
| --- | --- | --- |
| Admin | Владелец системы | Всё, включая пользователей, настройки и аудит |
| Manager | Менеджер по продажам | Клиенты, заявки, бронь, сделки, документы, чат |
| Accountant | Бухгалтер | Ввод и сторно платежей, акты сверки, отчёты |
| Content | Контент-менеджер | Дома, этажи, квартиры, цены, ход стройки, фото |
| Customer | Покупатель | Только свои сделки, платежи, документы, чат |
| Гость (без входа) | Посетитель сайта | Каталог и отправка заявки |

**Вход.** Сотрудники: почта и пароль, двухфакторная проверка позже. Покупатели: номер телефона и код из SMS.

## 4. Модель данных

Ядро системы: квартира → сделка → график платежей → платёж. Деньги хранятся как `decimal(18,2)` вместе с кодом валюты (TJS или USD). Все таблицы имеют `Id` (Guid), `CreatedAt`, `UpdatedAt`.

| Таблица | Ключевые поля | Связи |
| --- | --- | --- |
| Users | Email, Phone, PasswordHash, Role, IsActive | — |
| Buildings | Name (ru/tj), Address, FloorsCount, Description, ModelUrl | 1→много Floors |
| Floors | BuildingId, Number, PlanImageUrl | → Apartments |
| Apartments | FloorId, Number, Rooms, Area, PricePerM2, Price, Status, PlanCoords | → Bookings, Deals |
| Customers | FullName, Phone, Email, PassportData (шифровано), UserId | → Deals |
| Leads | Name, Phone, ApartmentId?, Source, Status, ManagerId | → Customer при конверсии |
| Bookings | ApartmentId, CustomerId/LeadId, ExpiresAt, Status | одна активная на квартиру |
| Deals | ApartmentId, CustomerId, ManagerId, Stage, Price, Discount, Currency | → PaymentPlan |
| PaymentPlans | DealId, DownPaymentPct, Months, PaymentDay, StartDate | → ScheduleItems |
| PaymentScheduleItems | PlanId, DueDate, AmountDue, Status | — |
| Payments | DealId, Amount, Currency, RateUsed, PaidAt, Method, ReversalOfId? | сторно вместо удаления |
| ExchangeRates | Date, Currency, RateToTjs | — |
| Documents | DealId, Type, FileKey, SignedAt | — |
| ConstructionStages | BuildingId, Name, Percent, UpdatedAt | → Photos |
| ConstructionPhotos | StageId, FileKey, TakenAt | — |
| ChatMessages | DealId, SenderId, Text, ReadAt | — |
| Notifications | UserId, Channel, Text, SentAt | — |
| AuditLog | UserId, Entity, EntityId, Action, Before, After, At | — |

**Индексы и ограничения:** уникальный `(FloorId, Number)` для квартиры; частичный уникальный индекс на `Bookings(ApartmentId)` где `Status = Active`; индекс на `Payments(DealId, PaidAt)`.

## 5. API

REST, JSON, префикс `/api/v1`. Списки с пагинацией (`page`, `pageSize`), ошибки в формате Problem Details. Язык текстов — по заголовку `Accept-Language` (ru, tg).

| Модуль | Метод и путь | Что делает | Доступ |
| --- | --- | --- | --- |
| Auth | POST /auth/login | Вход сотрудника | все |
| Auth | POST /auth/sms/send, /auth/sms/verify | Вход покупателя по коду | все |
| Auth | POST /auth/refresh, /auth/logout | Обновление и выход | вход |
| Catalog | GET /buildings, /buildings/{id} | Дома и описание | гость |
| Catalog | GET /buildings/{id}/floors | Этажи со счётчиком свободных | гость |
| Catalog | GET /floors/{id}/apartments | Квартиры этажа со статусами | гость |
| Catalog | GET /apartments/{id} | Карточка квартиры | гость |
| Catalog | POST/PUT /buildings, /floors, /apartments | Создание и правка | Content, Admin |
| Catalog | POST /apartments/import | Импорт из Excel | Content, Admin |
| Sales | POST /leads | Заявка с сайта | гость |
| Sales | GET /leads, PATCH /leads/{id} | Список и смена статуса | Manager |
| Sales | POST /apartments/{id}/bookings | Бронь квартиры | гость, Manager |
| Sales | POST /bookings/{id}/extend, /cancel | Продлить или снять бронь | Manager |
| Sales | GET/POST /customers | Клиенты | Manager |
| Sales | GET/POST /deals, PATCH /deals/{id}/stage | Сделки и этапы | Manager |
| Sales | GET /crm/summary, /crm/pipeline | Показатели и воронка | Manager, Admin |
| Payments | POST /deals/{id}/plan | Создать график платежей | Manager |
| Payments | GET /deals/{id}/schedule | График с суммами в TJS и USD | владелец, сотрудник |
| Payments | POST /deals/{id}/payments | Внести платёж | Accountant |
| Payments | POST /payments/{id}/reverse | Сторно платежа | Accountant, Admin |
| Payments | GET /deals/{id}/statement.pdf | Акт сверки на дату | владелец, сотрудник |
| Payments | GET /rates/current | Текущий курс НБТ | все |
| Documents | GET /deals/{id}/documents | Список документов | владелец, сотрудник |
| Documents | POST /deals/{id}/documents | Загрузка файла | Manager |
| Documents | GET /documents/{id}/download | Ссылка на скачивание на 15 минут | владелец, сотрудник |
| Construction | GET /buildings/{id}/stages | Этапы и проценты | покупатель, сотрудник |
| Construction | PUT /stages/{id}, POST /stages/{id}/photos | Обновить и добавить фото | Content |
| Chat | GET /deals/{id}/messages, POST /deals/{id}/messages | История и новое сообщение | владелец, сотрудник |
| Chat | WebSocket /hubs/chat (SignalR) | Сообщения в реальном времени | владелец, сотрудник |
| Admin | GET /audit, GET/POST /users | Аудит и пользователи | Admin |

## 6. Бизнес-правила

1. **Статусы квартиры.** Free → Booked → Sold. Из Booked можно вернуться в Free (бронь истекла или отменена). Статус OnRequest ставит только Content или Admin. Статус меняют только сами операции брони и сделки, напрямую его править нельзя.
2. **Бронь.** Срок 48 часов. На квартиру может быть только одна активная бронь: правило защищает частичный уникальный индекс в базе и транзакция, чтобы два покупателя не забронировали одну квартиру одновременно. Фоновая задача раз в 5 минут снимает просроченные брони. Продлить бронь может только Manager.
3. **Сделка.** Этапы: Заявка, Показ, Бронь, Договор, Оплата, Завершена. Вперёд — свободно, назад — только с причиной и записью в аудите. На этапе «Договор» квартира получает статус Sold.
4. **Цена.** Цена = площадь × цена за м². При создании сделки цена копируется в сделку и дальше не меняется при изменении прайса. Скидку задаёт Manager с обязательной причиной.
5. **График платежей.** Первоначальный взнос в процентах, срок в месяцах, день платежа. Месячный платёж = (цена − взнос) ÷ месяцы, округление до 1 сомони, последний платёж забирает остаток округлений. Платёж закрывает самые старые неоплаченные пункты графика.
6. **Долларовый эквивалент.** Если договор в USD, сумма к оплате в сомони = сумма USD × курс НБТ на дату оплаты. Курс загружается раз в день фоновой задачей, запасной вариант — ручной ввод Accountant. Курс, по которому принят платёж, сохраняется в платеже (`RateUsed`).
7. **Платежи не удаляются.** Ошибку исправляют сторно: создаётся обратная запись со ссылкой на исходный платёж.
8. **Акт сверки.** PDF на выбранную дату: начислено, оплачено, остаток, список платежей с курсами.
9. **Доступ к данным.** Покупатель видит только сделки, где его `CustomerId` совпадает с его учётной записью; чужой `dealId` даёт 404.
10. **Аудит.** Каждое изменение статуса, цены, платежа и документа пишется в AuditLog с автором, временем и значениями «до» и «после».

## 7. Интеграции

Все внешние сервисы закрыты интерфейсами (`INotificationSender`, `ISmsSender`, `IFileStorage`, `IRateProvider`), чтобы подменять провайдеров без переписывания бизнес-логики.

| Интеграция | Для чего | Этап | Статус |
| --- | --- | --- | --- |
| Telegram Bot API | Уведомления менеджеру о заявке и брони, клиенту о платеже | 2 | готов к работе |
| SMS-провайдер | Коды входа покупателя | 4 | нужно выбрать провайдера в Таджикистане |
| Курс НБТ | Пересчёт USD → TJS в платежах | 4 | проверить доступный формат данных; запасной вариант — ручной ввод |
| Хранилище файлов | Документы, фото стройки, планировки | 2 | MinIO или диск |
| PDF (QuestPDF) | Акт сверки, график платежей | 4 | локальная библиотека |
| Банковский эквайринг | Онлайн-оплата из кабинета | после MVP | нужны банк и документация от него |

Доступность провайдеров в Таджикистане, формат курса НБТ и условия банков нужно проверить на месте, я не сверял их по источникам.

## 8. Нефункциональные требования

- **Безопасность.** Только HTTPS. Пароли в виде хеша (Identity). Лимиты запросов на вход и отправку SMS (например, 5 кодов в час на номер). Настроенный CORS только на свои домены. Проверка прав на каждом эндпоинте. Защита по списку OWASP Top 10.
- **Персональные данные.** Паспортные данные хранить только если нужны для договора, в зашифрованном виде, доступ только у Manager и Admin. Требования закона РТ о персональных данных нужно уточнить у юриста.
- **Производительность.** Каталог (этажи и квартиры) отвечает быстрее 300 мс для 95% запросов, кэш каталога на 30 секунд со сбросом при смене статуса.
- **Бэкапы.** `pg_dump` каждый день, хранить 14 дней на другом сервере, раз в месяц проверять восстановление.
- **Наблюдаемость.** Serilog в файлы или Seq, эндпоинт `/health`, алерт в Telegram при падении.
- **Окружения.** dev, staging, prod. Миграции базы через EF Core, применение при деплое отдельной командой.
- **Языки.** Названия и описания домов хранятся на русском и таджикском; ошибки API тоже на обоих.
- **Тесты.** Покрыть тестами бронь (включая одновременные запросы), расчёт графика, пересчёт курса и проверку прав доступа.

## 9. План работ

Шесть этапов, около 10–14 недель при работе одного разработчика на полтора десятка часов в неделю (оценка, уточнится после первого этапа). Каждый этап заканчивается тем, что можно показать застройщику.

| Этап | Что делаем | Срок | Результат |
| --- | --- | --- | --- |
| 1. Основа | Решение, база, Identity, роли, Swagger, Docker, CI | 1–2 нед. | Вход работает, API запускается командой |
| 2. Каталог и заявки | Дома, этажи, квартиры, импорт из Excel, заявки, Telegram | 2 нед. | Сайт с 3D-домом показывает реальные квартиры |
| 3. Бронь и CRM | Бронь с защитой от двойной брони, сделки, этапы, Hangfire, отчёты | 2–3 нед. | Менеджеры работают в CRM, бронь с сайта попадает в неё |
| 4. Кабинет покупателя | SMS-вход, график, платежи, курс НБТ, документы, акт сверки PDF | 3 нед. | Покупатель видит свои платежи и документы |
| 5. Стройка и чат | Этапы, фото, SignalR-чат | 1–2 нед. | Ход стройки и связь с менеджером |
| 6. Запуск | Проверка безопасности, нагрузка, бэкапы, деплой на VPS, документация | 1–2 нед. | Продакшн и обучение сотрудников |

После MVP: онлайн-оплата через банк, приложение на Flutter, двухфакторная проверка, отчёты по продажам.

## 10. С чего начать

Начни с этапа 1: пустой, но запускаемый API с базой и входом. Потом только каталог, чтобы сайт с 3D-домом уже можно было показать.

1. Установи .NET 10 SDK, Docker Desktop (для PostgreSQL), Git и среду (Visual Studio, Rider или VS Code).
2. Создай решение:

```
dotnet new sln -n Sitora
dotnet new classlib -n Sitora.Domain -o src/Sitora.Domain
dotnet new classlib -n Sitora.Infrastructure -o src/Sitora.Infrastructure
dotnet new webapi -n Sitora.WebApi -o src/Sitora.WebApi --use-controllers
dotnet new xunit -n Sitora.Tests -o tests/Sitora.Tests
dotnet sln add src/Sitora.Domain src/Sitora.Infrastructure src/Sitora.WebApi tests/Sitora.Tests
dotnet add src/Sitora.Infrastructure reference src/Sitora.Domain
dotnet add src/Sitora.WebApi reference src/Sitora.Domain src/Sitora.Infrastructure
dotnet add tests/Sitora.Tests reference src/Sitora.Domain src/Sitora.Infrastructure src/Sitora.WebApi
dotnet add src/Sitora.Infrastructure package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add src/Sitora.Infrastructure package Microsoft.AspNetCore.Identity.EntityFrameworkCore
dotnet add src/Sitora.WebApi package Serilog.AspNetCore
```

3. Подними PostgreSQL в Docker (`docker run -e POSTGRES_PASSWORD=dev -p 5432:5432 -v sitora-pg:/var/lib/postgresql postgres:18`) и подключи EF Core.
4. Сделай сущности Building, Floor, Apartment и первую миграцию.
5. Добавь `GET /buildings`, `GET /floors/{id}/apartments` и загрузку тестовых данных из Excel.
6. Подключи сайт из прототипа к этому API вместо демо-данных.

**Критерии приёмки этапа 1–2**

- [ ] API запускается командой `docker compose up`.
- [ ] Вход сотрудника выдаёт JWT, права по ролям работают.
- [ ] Каталог отдаёт дом, этажи и квартиры со статусами.
- [ ] Импорт из Excel загружает реальный прайс застройщика.
- [ ] Заявка с сайта приходит менеджеру в Telegram.
- [ ] Сайт с 3D-домом работает на реальных данных.
