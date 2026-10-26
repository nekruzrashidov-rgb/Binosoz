# Контракт API и фронтенд web/

Сверка реального API (`/swagger/v1/swagger.json`, 26.10.2026) со слоем `web/js/api.js`. Расхождения исправлены во фронтенде. Найденные ошибки бэкенда исправлены в бэкенде и перечислены в конце.

## Общее

- **Адрес.** `web/js/config.js` → `API_BASE`, например `http://localhost:5194/api/v1`. Все пути ниже — относительно него. Исключения — `/health*` и хаб `/hubs/chat`: они от корня сервера.
- **Демо-режим.** Пустой `API_BASE` включает встроенный бэкенд `web/js/mock.js`. Мапперы в `api.js` принимают и его формат.
- **CORS.** Адрес сайта должен быть в `Cors__AllowedOrigins`. В Development уже разрешены `http://localhost:5500` и `http://127.0.0.1:5500`.
- **Форматы данных.** Перечисления — строками (`"Free"`, `"Booked"`). Деньги — числа. Даты: `DateOnly` — `YYYY-MM-DD`, время — ISO в UTC. Идентификаторы — GUID (UUIDv7). Короткий номер в интерфейсе — последние 8 символов: начало UUIDv7 — метка времени, она у соседних записей совпадает.
- **Язык.** `Accept-Language: ru|tg` влияет на название и описание дома. Тексты ошибок всегда на русском.
- **Списки.** Списки отдаются страницами: `{ items, page, pageSize, totalCount }`, `pageSize` не больше 100. `api.js` забирает все страницы по 100. Без этого при сдаче было бы видно только первые 20 записей.

## Вход и сессия

| Метод | Путь | Тело | Ответ |
| --- | --- | --- | --- |
| POST | `/auth/login` | `{ email, password }` | `AuthResponse` |
| POST | `/auth/sms/send` | `{ phone }` | 202 `{ message, expiresInSeconds }` |
| POST | `/auth/sms/verify` | `{ phone, code }` | `AuthResponse` |
| POST | `/auth/refresh` | `{ refreshToken }` | `AuthResponse` |
| POST | `/auth/logout` | `{ refreshToken }` | 204 |
| GET | `/auth/me` | — | `{ id, email, phone, fullName, roles[] }` |

`AuthResponse = { accessToken, accessTokenExpiresAt, refreshToken, refreshTokenExpiresAt, user }`.

Сессия:
- Access-токен живёт 15 минут и передаётся в заголовке `Authorization: Bearer`.
- На 401 `api.js` один раз вызывает `/auth/refresh`. Параллельные запросы ждут одного и того же обновления.
- Refresh-токен меняется при каждом обновлении. Повторное использование старого токена отзывает все сессии пользователя.
- Если обновить не удалось, сессия очищается, и человек видит «Нужно войти».

## Ошибки

| Код | Тело | Что показывает интерфейс |
| --- | --- | --- |
| 400 | `ValidationProblemDetails`: `errors: { "Phone": ["…"] }`, ключи в PascalCase, `title` по-английски | Текст под соответствующим полем; если поле не найдено — тексты из `errors` над формой. `title` не показывается |
| 401, 403, 404, 409, 422 | `ProblemDetails`, русский текст в `detail` | `detail`; без него — русская фраза по коду (`err.4xx`) |
| 422 импорта Excel | `ImportResult { created, updated, errors: [{ row, message }] }` | Список «Строка N: …» |
| 429 | `ProblemDetails` и заголовок `Retry-After` | Текст и число секунд |
| сеть, CORS | — | «Нет связи с сервером…» |

Имена полей в форме и в API различаются. `api.js` переименовывает ключи ошибок обратно:

| Поле в API | Поле в форме |
| --- | --- |
| `DownPaymentPct` | `downPaymentPercent` |
| `PaidAt` | `date` |
| `PassportData` | `passport` |
| `RateToTjs` | `rate` |
| `PricePerM2` | `pricePerSqm` |
| `SortOrder` | `order` |
| `Name` (этап стройки) | `title` |

## Каталог (без входа)

| Метод | Путь | Ответ | `api.js` |
| --- | --- | --- | --- |
| GET | `/buildings` | страница `BuildingResponse` | `buildings()` |
| GET | `/buildings/{id}` | `BuildingResponse` | `building()` |
| GET | `/buildings/{id}/floors` | страница `FloorResponse` | `floors()` |
| GET | `/floors/{id}/apartments` | страница `ApartmentResponse` | `floorApartments()` |
| GET | `/apartments/{id}` | `ApartmentResponse` | `apartment()` |
| GET | `/rates/current?currency=USD` | `{ date, currency, rateToTjs, source }`; 404, если курс не введён | `rate()` → `{ rate, date }` |
| POST | `/leads` | `{ name, phone, apartmentId?, source, comment? }` | `createLead()` |
| POST | `/apartments/{id}/bookings` | `{ name, phone }` (гость) или `{ customerId }` (менеджер) → `BookingResponse`; 409, если квартира занята | `book()` |

Состав ответов:
- `ApartmentResponse`: `id, floorId, buildingId, floorNumber, number, rooms, area, pricePerM2, price, currency, status, planSlot (0–7), model3dId`.
- `BuildingResponse`: `name` на языке запроса, `nameRu/Tg`, `descriptionRu/Tg`, `totalApartments`, `freeApartments`.

## CRM

| Метод | Путь | Роли | Примечание для фронтенда |
| --- | --- | --- | --- |
| GET | `/crm/summary` | Manager, Admin | `newLeads, leadsInProgress, activeBookings, bookingsExpiringIn24Hours, dealsByStage{}, apartmentsByStatus{}, soldAmount[{currency, amount}]`. Сумма продаж на дашборде — в сомони, USD по текущему курсу |
| GET | `/crm/pipeline` | Manager, Admin | `columns[{ stage, count, cards[{ kind: Lead\|Booking\|Deal, id, clientName, phone, apartmentId, apartmentNumber, amount, currency, expiresAt }] }]` |
| GET | `/leads?status=` | Manager, Admin | `status`: `New, InProgress, Converted, Rejected`. Интерфейс показывает `Converted` и `Rejected` как «Закрыта», а «Закрыть» отправляет `Rejected` |
| PATCH | `/leads/{id}` | Manager, Admin | `{ status, managerId, comment }` («Взять себе» — `managerId` текущего пользователя) |
| POST | `/bookings/{id}/extend` | Manager, Admin | +48 часов |
| POST | `/bookings/{id}/cancel` | Manager, Admin | `{ reason }` |
| GET | `/customers?search=` | Manager, Admin | страница `CustomerListItem` |
| GET | `/customers/{id}` | Manager, Admin | `passportData` — только здесь. Сделки клиента — `GET /deals?customerId=` |
| POST | `/customers` | Manager, Admin | `{ fullName, phone, email?, passportData?, leadId? }` |
| GET | `/deals?stage=&customerId=` | Manager, Accountant, Admin | страница `DealResponse` |
| GET | `/deals/{id}` | Manager, Accountant, Admin | `DealResponse` без этажа, площади и оплат: `api.deal()` дополняет их из `/apartments/{id}` и `/deals/{id}/schedule` |
| POST | `/deals` | Manager, Admin | `{ apartmentId, customerId, stage?, currency?, discount?, discountReason? }` |
| PATCH | `/deals/{id}/stage` | Manager, Admin | `{ stage, reason }`; назад и в отмену — только с причиной (иначе 422) |

## Деньги

| Метод | Путь | Роли | Тело / ответ |
| --- | --- | --- | --- |
| POST | `/deals/{id}/plan` | Manager, Admin | `{ downPaymentPct, months, paymentDay (1–28), startDate }` → `ScheduleResponse` |
| GET | `/deals/{id}/schedule` | владелец, сотрудник сделки | `{ currency, total, paid, remaining, overdue, downPaymentPct, months, paymentDay, startDate, items[], payments[], equivalent }`; 404, если графика ещё нет |
| GET | `/deals/{id}/schedule.pdf`, `/statement.pdf?date=` | владелец, сотрудник сделки | PDF |
| POST | `/deals/{id}/payments` | Accountant, Admin | `{ amount, currency, paidAt, method: Cash\|BankTransfer\|Card\|Other, comment }` |
| POST | `/payments/{id}/reverse` | Accountant, Admin | `{ reason }` → обратная запись: суммы со знаком минус, `reversalOfId` указывает на исходный платёж, у исходного `isReversed = true` |
| PUT | `/rates` | Accountant, Admin | `{ date, currency: "USD", rateToTjs }` |
| GET | `/reports/payments?from=&to=` | Accountant, Admin | `{ rows[], totalsByCurrency[], totalTjs }`, Excel — `.xlsx` |
| GET | `/reports/debts?asOf=` | Accountant, Admin | `rows[{ dealId, customerName, phone, apartmentNumber, currency, overdue, oldestOverdueDate, daysOverdue, remaining }]`, Excel — `.xlsx` |

Подробности:
- Пункт графика: `kind` (`DownPayment` или `Monthly`), `status` (`Pending`, `PartiallyPaid` или `Paid`) и признак `isOverdue`. Интерфейс показывает «Просрочен» по `isOverdue`.
- Отдельного списка платежей сделки нет: история берётся из `schedule.payments`.
- PDF и Excel скачиваются с токеном. PDF открывается в новой вкладке, Excel сохраняется под именем из `Content-Disposition`.

## Документы, файлы, чат, стройка

| Метод | Путь | Роли | Примечание |
| --- | --- | --- | --- |
| GET | `/deals/{id}/documents` | владелец, сотрудник сделки | `{ id, type, fileName, contentType, sizeBytes, signedAt, createdAt }` |
| POST | `/deals/{id}/documents` | Manager, Accountant, Admin | form-data: `file`, `type` (Contract, Annex, Act, Invoice, Receipt, Other), `signedAt?`; PDF, PNG, JPG, DOCX до 20 МБ |
| GET | `/documents/{id}/download` | владелец, сотрудник сделки | `{ url, expiresAt }` — подписанная ссылка на 15 минут. `url` относительный (`/api/v1/files?token=…`), фронтенд добавляет адрес сервера |
| GET | `/files?token=` | все, по ссылке | сам файл; подделанная или просроченная ссылка — 404 |
| GET | `/deals/{id}/messages` | владелец, сотрудник сделки | страница `{ id, dealId, senderId, senderName, fromCustomer, text, createdAt, readAt }`, новые сверху |
| POST | `/deals/{id}/messages` | владелец, сотрудник сделки | `{ text }` |
| GET | `/buildings/{id}/stages` | покупатель с неотменённой сделкой, сотрудники | `{ overallPercent, stages[{ id, name, percent, sortOrder, updatedAt, photos[{ url, caption, takenAt }] }] }`. Ссылки на фото подписаны на 15 минут |
| POST | `/buildings/{id}/stages`, PUT `/stages/{id}` | Content, Admin | `{ name, percent, sortOrder }` |
| POST | `/stages/{id}/photos` | Content, Admin | form-data: `file` (PNG или JPG до 10 МБ), `caption?`, `takenAt?` |

**Хаб чата** `/hubs/chat` (SignalR):
- токен передаётся в `accessTokenFactory`;
- методы `JoinDeal(dealId)`, `LeaveDeal(dealId)` и `SendMessage(dealId, text)`, `dealId` — строка GUID;
- событие `messageReceived` с сообщением приходит и на сообщения, отправленные через REST;
- если хаб недоступен, чат опрашивает REST раз в 9 секунд.

## Администрирование

| Метод | Путь | Роли | Примечание |
| --- | --- | --- | --- |
| GET | `/users` | Admin | страница `{ id, email, phone, fullName, roles[], isActive, createdAt }` |
| POST | `/users` | Admin | `{ email, fullName, role, password, phone? }` |
| PATCH | `/users/{id}/status` | Admin | `{ isActive }` |
| GET | `/audit?entity=&from=&to=` | Admin | страница `{ userEmail, entity, entityId, action, before, after, at }`. `before` и `after` — JSON-строки. Имена сущностей — имена классов: `Deal`, `Payment`, `Apartment`, `AppUser`… |
| PUT | `/buildings/{id}` | Content, Admin | полная замена: `{ nameRu, nameTg, address, floorsCount, descriptionRu, descriptionTg, modelUrl }`. Поля, которых нет в форме, `api.js` берёт из текущей версии |
| PUT | `/apartments/{id}`, POST `/apartments` | Content, Admin | `{ number, rooms, area, pricePerM2, planSlot, model3dId }` (+ `floorId` при создании) |
| PATCH | `/apartments/{id}/on-request` | Content, Admin | `{ onRequest }` |
| POST | `/apartments/import?buildingId=` | Content, Admin | form-data `file` (.xlsx) |

## Что было не так в web/js/api.js и вьюхах (исправлено)

**Не отправлялись или искажались поля запросов.** Создание сделки отправляло `+GUID`, то есть `NaN`, и клиент с квартирой не доходили до сервера. Остальные запросы шли со старыми именами полей:

| Запрос | Что отправлялось | Что ждёт API |
| --- | --- | --- |
| график | `downPaymentPercent`, без даты начала | `downPaymentPct` и `startDate` |
| платёж | `date`, без способа оплаты | `paidAt` и `method` |
| курс | `rate` / `value` | `rateToTjs` и `currency` |
| клиент | `passport` | `passportData` |
| этап стройки | `title` / `description` | `name` / `sortOrder` |
| блокировка сотрудника | `blocked` | `isActive` |
| заявка | `assignee` | `managerId` |

**Неверно разбирались ответы.**
- Курс читался как `rate` и всегда был 0.
- Этапы стройки читались списком, хотя приходит объект `{ stages }`, поэтому раздел был пуст.
- Итоги графика читались как `totalDue/totalPaid` вместо `total/paid`.
- Отчёт по долгам ждал `since/days`, а приходят `oldestOverdueDate/daysOverdue`.
- Статус сотрудника ждал `status`, а приходит `isActive`.
- Отправитель в чате определялся по `sender`, а приходят `fromCustomer/senderId`.
- Итоги отчёта по платежам ожидались объектом, а приходят массивом.
- Карточки воронки ждали `kind` строчными буквами и `title`, а приходят `Lead|Booking|Deal` и `clientName`.

**Лишние адреса.** В `api.js` были методы для `GET /rates`, `GET /bookings` и `GET /deals/{id}/payments`. Таких адресов в API нет. История платежей теперь берётся из графика.

**Пагинация.** Все списки шли без `pageSize` и обрезались до 20 записей.

**Чат.** `JoinDeal(Number(dealId))` превращал GUID в `NaN`, и живые обновления не приходили.

**Ошибки.**
- 400 с ошибками полей показывал английский `title`, а не русские тексты.
- 422 импорта со списком строк превращался в «[object Object]».
- Ключи ошибок в PascalCase не находили свои поля в форме.

**Документы** загружались без `type`. Теперь тип выбирается при загрузке.

**Мелочи.**
- `/health/ready` вызывался по пути `/api/v1/health/ready`.
- `S.today()` брал дату UTC: до 5 утра по Душанбе получался вчерашний день.
- Номера сделок показывались одинаковыми: у всех одно начало UUIDv7.

## Что было не так в бэкенде (исправлено, с тестами)

1. **Права бухгалтера.** Бухгалтер вносит платежи, но не мог прочитать список сделок (`GET /deals` — только Manager и Admin). Новую сделку без оплат и просрочек ему было не найти. Теперь список и карточку читают Manager, Accountant и Admin, а создают сделки и меняют этап только Manager и Admin. Тест `Accountant_ReadsDeals_ButCannotCreateOrMoveThem`.
2. **Английские имена полей в ошибках.** `.WithMessage` у FluentValidation относится только к последнему правилу цепочки. Поэтому нулевой курс давал «'Rate To Tjs' должно быть больше '0'.». То же было у площади, цены за м² и взноса. Теперь сообщения стоят у каждой границы, а стандартным сообщениям заданы русские имена полей (`Validators/ValidationSetup.cs`). Тест `SetRate_ZeroRate_ExplainsInRussian` (ru и tg).
3. **Шум в логах.** Журнал запросов Serilog стоял внутри обработчика исключений и писал каждый ожидаемый 404, 409 или 422 как «responded 500» уровня Error со стеком. Теперь он стоит снаружи и пишет настоящий код ответа. Настоящие 500 по-прежнему пишет `DomainExceptionHandler`.
