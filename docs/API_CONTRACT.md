# Контракт API и фронтенд web/

Сверка реального API (`/swagger/v1/swagger.json`, 26.10.2026) со слоем `web/js/api.js`. Расхождения исправлены во фронтенде. Найденные ошибки бэкенда исправлены в бэкенде и перечислены в конце.

## Общее

- **Адрес.** `web/js/config.js` → `API_BASE`, например `http://localhost:5194/api/v1`. Все пути ниже — относительно него. Исключения — `/health*` и хаб `/hubs/chat`: они от корня сервера.
- **Демо-режим.** Пустой `API_BASE` включает встроенный бэкенд `web/js/mock.js`. Мапперы в `api.js` принимают и его формат.
- **CORS.** Адрес сайта должен быть в `Cors__AllowedOrigins`. В Development уже разрешены `http://localhost:5500` и `http://127.0.0.1:5500`.
- **Форматы данных.** Перечисления — строками (`"Free"`, `"Booked"`). Деньги — числа. Даты: `DateOnly` — `YYYY-MM-DD`, время — ISO в UTC. Идентификаторы — GUID (UUIDv7). Короткий номер в интерфейсе — последние 8 символов: начало UUIDv7 — метка времени, она у соседних записей совпадает.
- **Язык.** `Accept-Language: ru|tg` влияет на название и описание дома. Тексты ошибок всегда на русском.
- **Списки.** Списки отдаются страницами: `{ items, page, pageSize, totalCount }`, `pageSize` не больше 100. `api.js` забирает все страницы по 100. Без этого при сдаче было бы видно только первые 20 записей.

## Роли и организации

- **Организация** — застройщик. Ей принадлежат дома, сотрудники, клиенты, заявки, ручной курс и аудит. Данные из базы фильтруются по организации и домам из токена. Чужое отвечает 404, как несуществующее.
- **PlatformOwner** — создатель платформы, скрытая роль. Создаётся только из `ADMIN_EMAIL` и `ADMIN_PASSWORD`. Ведёт организации и может войти «как владелец» любой из них. На странице входа его нет: он входит той же формой по почте, а потом вводит второй фактор.
- **Owner** — владелец организации (бывшая роль `Admin`). Ведёт все свои дома, создаёт дома, назначает админов объектов и сотрудников, видит аудит и отчёты.
- **ObjectAdmin** — админ одного дома. Ведёт свой дом, его сделки и сотрудников этого дома.
- **Manager, Accountant, Content** — должности внутри организации. Без домов в `buildingIds` должность действует во всей организации, с домами — только в них.
- **Customer** — покупатель, видит только свои сделки.

В таблицах ниже «руководители» — это Owner и ObjectAdmin, а также PlatformOwner в режиме «как владелец».

Сотрудников не регистрируют сами: их приглашают по почте (`/invitations`) или заводят сразу (`/users`).

## Вход и сессия

Вход для всех — по коду из почты. Отдельных кнопок по ролям нет: интерфейс выбирает раздел по `user.role`.

| Метод | Путь | Тело | Ответ |
| --- | --- | --- | --- |
| POST | `/auth/email/send` | `{ email }` | 202 `{ message, expiresInSeconds }` — всегда одинаковый, даже если почты нет |
| POST | `/auth/email/verify` | `{ email, code }` | 200 `AuthResponse` или 202 `SecondFactorChallenge` (только PlatformOwner) |
| POST | `/auth/second-factor` | `{ challengeToken, password? , totpCode? }` | `AuthResponse` |
| POST | `/auth/invitations/accept` | `{ token }` | `AuthResponse`; 401 — приглашение недействительно или использовано |
| POST | `/auth/refresh` | `{ refreshToken }` | `AuthResponse` |
| POST | `/auth/logout` | `{ refreshToken }` | 204 |
| GET | `/auth/me` | — | `CurrentUser` |
| POST | `/auth/login` | `{ email, password }` | `AuthResponse`; выключается `Auth__PasswordLoginEnabled=false` (тогда 404). PlatformOwner так не входит |
| POST | `/auth/sms/send`, `/auth/sms/verify` | — | выключены (404), включаются `Auth__SmsLoginEnabled=true` |

Код из почты:
- 6 цифр, действует 10 минут;
- 5 неверных попыток — и код сгорает;
- код одноразовый, действует только последний запрошенный;
- новый код — не чаще раза в минуту и не больше 5 в час, отдельно на почту и на IP (429 с русским текстом);
- в базе хранится только хеш;
- почта нормализуется: регистр и пробелы по краям не важны.

Покупатель входит по почте, указанной в карточке клиента. Учётная запись создаётся при первом входе и привязывается ко всем карточкам с этой почтой.

```
AuthResponse = { accessToken, accessTokenExpiresAt, refreshToken, refreshTokenExpiresAt, user: CurrentUser }

CurrentUser = {
  id, email, phone, fullName,
  roles: ["Owner"],                 // все роли
  role: "Owner",                    // главная: PlatformOwner > Owner > ObjectAdmin > Manager > Accountant > Content > Customer
  organizationId: "…" | null,       // null у PlatformOwner вне режима и у покупателя
  organizationName: "…" | null,
  buildingIds: [],                  // пусто — все дома организации; у ObjectAdmin ровно один
  impersonation: null | { impersonatedBy, organizationId }
}

SecondFactorChallenge = { challengeToken, expiresAt, methods: ["password", "totp"?], message }
```

В access-токене те же данные — в claims `role`, `org`, `bld` (по одному на дом) и `imp`. Если владелец меняет сотруднику дома, все сессии сотрудника отзываются: новые права действуют со следующего входа.

## Платформа (только PlatformOwner)

| Метод | Путь | Тело / ответ |
| --- | --- | --- |
| GET | `/platform/organizations` | `[{ id, name, slug, isActive, buildingsCount, usersCount, createdAt }]` |
| POST | `/platform/organizations` | `{ name, slug, ownerEmail, ownerFullName }` → 201. Владельцу уходит письмо, он входит по коду из почты. `slug`: латиница, цифры, дефис |
| POST | `/platform/impersonate/{organizationId}` | `AccessTokenResponse = { accessToken, accessTokenExpiresAt, user }` без refresh-токена; `user.role = "Owner"`, `user.impersonation` заполнено |
| POST | `/platform/impersonate/stop` | `AccessTokenResponse` — обычный токен создателя платформы |
| GET | `/platform/totp` | `{ sharedKey, authenticatorUri }` для приложения-аутентификатора |
| POST | `/platform/totp/enable` | `{ code }` → 204; после этого вторым фактором подходит и код из приложения |

В режиме «как владелец»:
- в каждом ответе есть заголовок `X-Impersonated-By: <id создателя платформы>`;
- каждый запрос, кроме GET, пишется в аудит (`entity = "Impersonation"`, `action = "POST /api/v1/…"`, `after = { status }`). Пишутся и неудачные запросы;
- записи самих сервисов получают `impersonatedById`;
- интерфейс показывает плашку «Вы вошли как владелец …» и кнопку «Выйти», которая вызывает `/platform/impersonate/stop`. Токен режима истекает через 15 минут, и обновить его нельзя.

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
| GET | `/buildings?organization=slug` | страница `BuildingResponse`. Сайт застройщика передаёт свой `slug` (у организации по умолчанию — `sitora`). Без параметра — дома всех активных организаций. С токеном сотрудника — только его дома | `buildings()` |
| GET | `/buildings/{id}` | `BuildingResponse` | `building()` |
| GET | `/buildings/{id}/floors` | страница `FloorResponse` | `floors()` |
| GET | `/floors/{id}/apartments` | страница `ApartmentResponse` | `floorApartments()` |
| GET | `/apartments/{id}` | `ApartmentResponse` | `apartment()` |
| GET | `/rates/current?currency=USD&organization=slug` (или `&organizationId=`) | `{ date, currency, rateToTjs, source }`. Ручной курс у каждой организации свой, поэтому сайт передаёт slug или id организации; неизвестный slug — 404. Без него — только общий курс НБТ. Сотруднику организация берётся из токена. 404, если курса нет | `rate()` → `{ rate, date }` |
| POST | `/leads` | `{ name, phone, apartmentId?, buildingId?, source, comment? }`. Организация берётся из квартиры или дома. Если на платформе несколько застройщиков, без них — 422 | `createLead()` |
| POST | `/apartments/{id}/bookings` | `{ name, phone }` (гость) или `{ customerId }` (менеджер) → `BookingResponse`; 409, если квартира занята | `book()` |

Состав ответов:
- `ApartmentResponse`: `id, floorId, buildingId, floorNumber, number, rooms, area, pricePerM2, price, currency, status, planSlot (0–7), model3dId`.
- `BuildingResponse`: `name` на языке запроса, `nameRu/Tg`, `descriptionRu/Tg`, `totalApartments`, `freeApartments`.

## CRM

| Метод | Путь | Роли | Примечание для фронтенда |
| --- | --- | --- | --- |
| GET | `/crm/summary` | Manager, руководители | `newLeads, leadsInProgress, activeBookings, bookingsExpiringIn24Hours, dealsByStage{}, apartmentsByStatus{}, soldAmount[{currency, amount}]`. Сумма продаж на дашборде — в сомони, USD по текущему курсу |
| GET | `/crm/pipeline` | Manager, руководители | `columns[{ stage, count, cards[{ kind: Lead\|Booking\|Deal, id, clientName, phone, apartmentId, apartmentNumber, amount, currency, expiresAt }] }]` |
| GET | `/leads?status=` | Manager, руководители | `status`: `New, InProgress, Converted, Rejected`. Интерфейс показывает `Converted` и `Rejected` как «Закрыта», а «Закрыть» отправляет `Rejected` |
| PATCH | `/leads/{id}` | Manager, руководители | `{ status, managerId, comment }` («Взять себе» — `managerId` текущего пользователя) |
| POST | `/bookings/{id}/extend` | Manager, руководители | +48 часов |
| POST | `/bookings/{id}/cancel` | Manager, руководители | `{ reason }` |
| GET | `/customers?search=` | Manager, руководители | страница `CustomerListItem` |
| GET | `/customers/{id}` | Manager, руководители | `passportData` — только здесь. Сделки клиента — `GET /deals?customerId=` |
| POST | `/customers` | Manager, руководители | `{ fullName, phone, email?, passportData?, leadId?, buildingId? }`. Телефон уникален в пределах организации. `email` нужен покупателю для входа. `buildingId` обязателен, если у сотрудника несколько домов |
| GET | `/deals?stage=&customerId=` | Manager, Accountant, руководители | страница `DealResponse` |
| GET | `/deals/{id}` | Manager, Accountant, руководители | `DealResponse` без этажа, площади и оплат: `api.deal()` дополняет их из `/apartments/{id}` и `/deals/{id}/schedule` |
| POST | `/deals` | Manager, руководители | `{ apartmentId, customerId, stage?, currency?, discount?, discountReason? }` |
| PATCH | `/deals/{id}/stage` | Manager, руководители | `{ stage, reason }`; назад и в отмену — только с причиной (иначе 422) |

## Деньги

| Метод | Путь | Роли | Тело / ответ |
| --- | --- | --- | --- |
| POST | `/deals/{id}/plan` | Manager, руководители | `{ downPaymentPct, months, paymentDay (1–28), startDate }` → `ScheduleResponse` |
| GET | `/deals/{id}/schedule` | владелец, сотрудник сделки | `{ currency, total, paid, remaining, overdue, downPaymentPct, months, paymentDay, startDate, items[], payments[], equivalent }`; 404, если графика ещё нет |
| GET | `/deals/{id}/schedule.pdf`, `/statement.pdf?date=` | владелец, сотрудник сделки | PDF |
| POST | `/deals/{id}/payments` | Accountant, руководители | `{ amount, currency, paidAt, method: Cash\|BankTransfer\|Card\|Other, comment }` |
| POST | `/payments/{id}/reverse` | Accountant, руководители | `{ reason }` → обратная запись: суммы со знаком минус, `reversalOfId` указывает на исходный платёж, у исходного `isReversed = true` |
| PUT | `/rates` | Accountant, руководители | `{ date, currency: "USD", rateToTjs }` |
| GET | `/reports/payments?from=&to=` | Accountant, руководители | `{ rows[], totalsByCurrency[], totalTjs }`, Excel — `.xlsx` |
| GET | `/reports/debts?asOf=` | Accountant, руководители | `rows[{ dealId, customerName, phone, apartmentNumber, currency, overdue, oldestOverdueDate, daysOverdue, remaining }]`, Excel — `.xlsx` |

Подробности:
- Пункт графика: `kind` (`DownPayment` или `Monthly`), `status` (`Pending`, `PartiallyPaid` или `Paid`) и признак `isOverdue`. Интерфейс показывает «Просрочен» по `isOverdue`.
- Отдельного списка платежей сделки нет: история берётся из `schedule.payments`.
- PDF и Excel скачиваются с токеном. PDF открывается в новой вкладке, Excel сохраняется под именем из `Content-Disposition`.

## Документы, файлы, чат, стройка

| Метод | Путь | Роли | Примечание |
| --- | --- | --- | --- |
| GET | `/deals/{id}/documents` | владелец, сотрудник сделки | `{ id, type, fileName, contentType, sizeBytes, signedAt, createdAt }` |
| POST | `/deals/{id}/documents` | Manager, Accountant, руководители | form-data: `file`, `type` (Contract, Annex, Act, Invoice, Receipt, Other), `signedAt?`; PDF, PNG, JPG, DOCX до 20 МБ |
| GET | `/documents/{id}/download` | владелец, сотрудник сделки | `{ url, expiresAt }` — подписанная ссылка на 15 минут. `url` относительный (`/api/v1/files?token=…`), фронтенд добавляет адрес сервера |
| GET | `/files?token=` | все, по ссылке | сам файл; подделанная или просроченная ссылка — 404 |
| GET | `/deals/{id}/messages` | владелец, сотрудник сделки | страница `{ id, dealId, senderId, senderName, fromCustomer, text, createdAt, readAt }`, новые сверху |
| POST | `/deals/{id}/messages` | владелец, сотрудник сделки | `{ text }` |
| GET | `/buildings/{id}/stages` | покупатель с неотменённой сделкой, сотрудники | `{ overallPercent, stages[{ id, name, percent, sortOrder, updatedAt, photos[{ url, caption, takenAt }] }] }`. Ссылки на фото подписаны на 15 минут |
| POST | `/buildings/{id}/stages`, PUT `/stages/{id}` | Content, руководители | `{ name, percent, sortOrder }` |
| POST | `/stages/{id}/photos` | Content, руководители | form-data: `file` (PNG или JPG до 10 МБ), `caption?`, `takenAt?` |

**Хаб чата** `/hubs/chat` (SignalR):
- токен передаётся в `accessTokenFactory`;
- методы `JoinDeal(dealId)`, `LeaveDeal(dealId)` и `SendMessage(dealId, text)`, `dealId` — строка GUID;
- событие `messageReceived` с сообщением приходит и на сообщения, отправленные через REST;
- если хаб недоступен, чат опрашивает REST раз в 9 секунд.

## Администрирование

| Метод | Путь | Роли | Примечание |
| --- | --- | --- | --- |
| GET | `/users` | руководители | страница `{ id, email, phone, fullName, roles[], isActive, createdAt, organizationId, buildingIds[] }`. ObjectAdmin видит только сотрудников своего дома |
| POST | `/users` | руководители | `{ email, fullName, role, password?, phone?, buildingIds? }`. Пароль необязателен: вход по коду из почты |
| PATCH | `/users/{id}/status` | руководители | `{ isActive }`. ObjectAdmin не может блокировать руководителей |
| PUT | `/users/{id}/buildings` | Owner | `{ buildingIds }`: пусто — все дома, у ObjectAdmin ровно один. Сессии сотрудника отзываются |
| POST | `/invitations` | руководители | `{ email, fullName, role, buildingIds? }` → 201 `{ id, email, fullName, role, buildingIds, expiresAt, acceptedAt, createdAt }`. Письмо со ссылкой `EMAIL_INVITE_URL + token` живёт 7 дней |
| GET | `/invitations` | руководители | непринятые приглашения |
| DELETE | `/invitations/{id}` | руководители | 204 |
| POST | `/buildings` | Owner | новый дом организации |

Кого может назначить:
- **Owner** — ObjectAdmin (ровно один дом), Manager, Accountant, Content (дома необязательны, пусто — все);
- **ObjectAdmin** — Manager, Accountant, Content, только в свой дом: если дома не переданы, подставляется его дом;
- владельцев заводит только создатель платформы.
| GET | `/audit?entity=&from=&to=` | Owner | страница `{ userEmail, organizationId, impersonatedById, entity, entityId, action, before, after, at }`. `before` и `after` — JSON-строки. Имена сущностей — имена классов: `Deal`, `Payment`, `Apartment`, `AppUser`… |
| PUT | `/buildings/{id}` | Content, руководители | полная замена: `{ nameRu, nameTg, address, floorsCount, descriptionRu, descriptionTg, modelUrl }`. Поля, которых нет в форме, `api.js` берёт из текущей версии |
| PUT | `/apartments/{id}`, POST `/apartments` | Content, руководители | `{ number, rooms, area, pricePerM2, planSlot, model3dId }` (+ `floorId` при создании) |
| PATCH | `/apartments/{id}/on-request` | Content, руководители | `{ onRequest }` |
| POST | `/apartments/import?buildingId=` | Content, руководители | form-data `file` (.xlsx) |

## Фронтенд web/: вход по почте и организации (сделано)

Всё из списка ниже сделано в `web/`: `js/api.js`, `js/cabinet.js` (вход и приглашение), `js/platform.js` (новый раздел), `js/crm.js`, `js/app.js`; демо-бэкенд `js/mock.js` понимает новые адреса. Для сайта застройщика в `config.js` задаётся `ORG_SLUG`.

1. **Страница входа.** Одна форма «Почта → Код» для всех: `POST /auth/email/send`, затем `POST /auth/email/verify`. Кнопки «Покупатель / Менеджер / Админ», вход по паролю и вход по SMS убрать (`/auth/sms/*` теперь отвечают 404).
2. **Второй шаг.** Если verify ответил **202**, показать поле «Пароль или код из приложения» и отправить `POST /auth/second-factor` с `challengeToken`. Это бывает только у создателя платформы; отдельного входа для него нет.
3. **Таймер и ошибки кода.** Повторная отправка — не раньше чем через 60 секунд. На 429 показать `detail`. На 401 показать «Неверный или устаревший код» и предложить запросить новый.
4. **Выбор раздела** — по `user.role`:
   - `PlatformOwner` → раздел платформы;
   - `Owner` / `ObjectAdmin` → админка;
   - `Manager` / `Accountant` / `Content` → CRM с пунктами по должности;
   - `Customer` → кабинет.

   Роль `Admin` больше не приходит, вместо неё `Owner`.
5. **Шапка.** Показывать `organizationName`. У ObjectAdmin — название его дома (`buildingIds[0]` → `/buildings/{id}`).
6. **Плашка режима «как владелец».** Если `user.impersonation` не `null` или в ответе есть заголовок `X-Impersonated-By`, показать заметную полосу «Вы вошли как владелец организации …» с кнопкой «Выйти». Кнопка вызывает `POST /platform/impersonate/stop` и подменяет токен. Refresh-токена в этом режиме нет: на 401 вернуть человека в раздел платформы.
7. **Раздел платформы** (новый): список организаций, «Создать организацию» (`name, slug, ownerEmail, ownerFullName`), кнопка «Войти как владелец», настройка TOTP (`/platform/totp`).
8. **Сотрудники.**
   - Вместо поля «Пароль» — «Пригласить по почте» (`POST /invitations`), выбор должности и домов.
   - Для ObjectAdmin — ровно один дом.
   - Список ожидающих приглашений с кнопкой «Отозвать».
   - Владельцу — «Дома сотрудника» (`PUT /users/{id}/buildings`).
9. **Страница приглашения.** Адрес `#invite=<token>` → `POST /auth/invitations/accept { token }` → сохранить сессию, как после входа.
10. **Публичный сайт.**
    - Добавить `ORG_SLUG` в `config.js` и передавать `?organization=` в `/buildings`.
    - slug организации — в `/rates/current?organization=` (или её `organizationId`).
    - `buildingId` (или `apartmentId`) в форме заявки.
11. **Клиенты.** В форме клиента почта теперь важна: по ней покупатель входит в кабинет. При нескольких домах у сотрудника нужен выбор дома (`buildingId`).
12. **Аудит.** Показывать `impersonatedById` («через платформу»). Раздел аудита — только Owner.

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
