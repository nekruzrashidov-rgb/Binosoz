# Контракт API и фронтенд frontend/

Сверка реального API (`/swagger/v1/swagger.json`, 26.10.2026) со слоем `frontend/js/api.js`. Расхождения исправлены во фронтенде. Найденные ошибки бэкенда исправлены в бэкенде и перечислены в конце.

## Общее

- **Адрес.** `frontend/js/config.js` → `API_BASE`, например `http://localhost:5194/api/v1`. Все пути ниже — относительно него. Исключения — `/health*` и хаб `/hubs/chat`: они от корня сервера.
- **Демо-режим.** Пустой `API_BASE` включает встроенный бэкенд `frontend/js/mock.js`. Мапперы в `api.js` принимают и его формат.
- **CORS.** Адрес сайта должен быть в `Cors__AllowedOrigins`. В Development уже разрешены `http://localhost:5500` и `http://127.0.0.1:5500`.
- **Форматы данных.** Перечисления — строками (`"Free"`, `"Booked"`). Деньги — числа. Даты: `DateOnly` — `YYYY-MM-DD`, время — ISO в UTC. Идентификаторы — GUID (UUIDv7). Короткий номер в интерфейсе — последние 8 символов: начало UUIDv7 — метка времени, она у соседних записей совпадает.
- **Версия.** `GET /health/live` (от корня сервера, публичный): `{ status, version, builtAt }` — короткий SHA коммита сборки (`GIT_SHA` образа из CI, иначе `RENDER_GIT_COMMIT`, иначе `dev`) и время сборки (ISO 8601, UTC). Сайт показывает свою версию из `/version.txt` в футере и в консоли.
- **Язык.** `Accept-Language: ru|tg` влияет на название и описание дома и на тексты ошибок (`title`, `detail`, сообщения в `errors`): при `tg` — таджикский, иначе русский. Тексты с подставленными значениями (номер этажа, дата) пока остаются русскими.
- **Списки.** Списки отдаются страницами: `{ items, page, pageSize, totalCount }`. `page` — от 1, `pageSize` — по умолчанию 20, не больше 100. Значения вне границ не дают ошибку, а тихо исправляются: `page < 1` → 1, `pageSize < 1` → 20, `pageSize > 100` → 100; в ответе `page` и `pageSize` — уже исправленные. `api.js` забирает все страницы по 100.
- **Кэш.** Публичные GET без входа (дома, этажи, квартиры, отзывы, `/public/brand`, `/rates/current`) кэшируются сервером на 15–30 с (ключ — путь, строка запроса, `Accept-Language`) и отдаются с `Cache-Control: public, max-age=15`. Изменение каталога, статуса, брони, цены сбрасывает кэш сразу. Ответы на запросы со входом не кэшируются: `Cache-Control: private, no-store`.
- **Refresh-токен.** Ответы входа и `/auth/refresh` ставят cookie `sitora_rt` (HttpOnly, SameSite=Strict, `Path=/api/v1/auth`) и по-прежнему отдают `refreshToken` в теле. `/auth/refresh` и `/auth/logout` берут токен из тела, а если его нет — из cookie; тело можно не присылать. Режим — `Auth__RefreshCookie__Mode`: `Dual` (по умолчанию), `Cookie` (в теле пустая строка), `Body` (без cookie).

## Роли и организации

- **Организация** — застройщик. Ей принадлежат дома, сотрудники, клиенты, заявки, ручной курс и аудит. Данные из базы фильтруются по организации и домам из токена. Чужое отвечает 404, как несуществующее.
- **SuperAdmin** (раньше PlatformOwner) — создатель платформы, выше всех ролей. Создаётся из `ADMIN_EMAIL` и `ADMIN_PASSWORD`, в Development ещё сидер `superadmin@binosoz.tj` (пароль — `SEED_SUPERADMIN_PASSWORD`). Ведёт организации и может войти «как владелец» любой из них. Входит, как сотрудники, по почте и паролю; если включена 2FA — ещё кодом из приложения.
- **Owner** — владелец организации (бывшая роль `Admin`). Ведёт все свои дома, создаёт дома, назначает админов объектов и сотрудников, видит аудит и отчёты.
- **ObjectAdmin** — админ одного дома. Ведёт свой дом, его сделки и сотрудников этого дома.
- **Manager, Accountant, Content** — должности внутри организации. Без домов в `buildingIds` должность действует во всей организации, с домами — только в них.
- **Customer** — покупатель, видит только свои сделки.

В таблицах ниже «руководители» — это Owner и ObjectAdmin, а также SuperAdmin в режиме «как владелец».

Сотрудников не регистрируют сами: их заводит руководитель (`/invitations` или `/users`), а логин и пароль приходят письмом «Вы приглашены в Binosoz».

## Вход и сессия

Два способа входа: **сотрудники** (Owner, ObjectAdmin, Manager, Accountant, Content, SuperAdmin) — по почте и паролю, **покупатели** — по почте и своему 6-значному коду (PIN), регистрация и сброс кода подтверждаются кодом из письма. Раздел интерфейса выбирается по `user.role`.

| Метод | Путь | Тело | Ответ |
| --- | --- | --- | --- |
| GET | `/auth/methods` | — | 200 `{ email, sms, demo, pin, telegram }` (`pin` — вход покупателя по своему коду, всегда `true`; `demo` — включён вход демо-покупателя паролем, `Auth__DemoPasswordLogin`) — какие способы входа реально работают: `email` — письма уходят (Development или `Email__Provider` = Smtp/Resend, или Gmail со всеми тремя `Email__Gmail__*`), `sms` — вход по SMS включён (`Auth__SmsLoginEnabled`) и SMS-провайдер подключён, `telegram` — вход по телефону кодом в Telegram (задан `Telegram__Gateway__Token`). Публичный |
| POST | `/auth/customer/register` | `{ name, email, phone, pin, organization? }` | 200 `{ message, expiresInSeconds, resendAfterSeconds }`. Покупатель сам придумывает 6-значный код входа (PIN): ровно 6 цифр, не одинаковые и не подряд (`123456`, `654321`, `012345`…) — иначе 422 `pin_format` / `pin_weak`. Создаются карточка клиента у застройщика `organization` (slug; без него — единственный застройщик платформы) и учётная запись с неподтверждённой почтой, PIN — хешем Identity; на почту уходит код подтверждения. Неподтверждённую регистрацию можно повторить (новое имя и PIN). Почта подтверждена и есть PIN — 409 `email_taken`; подтверждена без PIN — 409 `pin_not_set`; почта сотрудника — 409 `email_is_staff`; телефон из карточки с другой почтой — 409; неверный телефон, имя короче 2 символов — 422; неизвестный застройщик — 404; без почтового сервиса — 503 `email_not_configured` до создания карточки |
| POST | `/auth/customer/confirm/resend` | `{ email }` | 200, как `register`, — одинаковый для любой почты; письмо — только если регистрация ждёт подтверждения |
| POST | `/auth/customer/confirm` | `{ email, code }` | 200 `AuthResponse`: почта подтверждена, карточки клиента с этой почтой привязаны. Неверный, устаревший (10 мин) или исчерпанный (5 попыток) код — 401 `invalid_code` |
| POST | `/auth/customer/login` | `{ email, pin }` | 200 `AuthResponse`. Неверный PIN, незнакомая почта и почта сотрудника — одинаково 401 `invalid_pin` + `attemptsLeft`; 5 ошибок — 429 `pin_locked` + `lockedUntil`, `retryAfterSeconds` (15 минут, Identity lockout). Верный PIN, но почта не подтверждена — 403 `email_not_confirmed`; покупатель, которого завёл менеджер (почта есть, PIN нет), — 409 `pin_not_set` (дальше — «Забыли код?») |
| POST | `/auth/customer/pin/reset-request` | `{ email }` | 200 `{ message, expiresInSeconds, resendAfterSeconds }` — одинаковый для любой почты; код для сброса уходит только покупателю (есть учётная запись или карточка клиента). Не чаще раза в минуту — 429 `code_cooldown` |
| POST | `/auth/customer/pin/reset` | `{ email, code, newPin }` | 200 `AuthResponse`: новый PIN, блокировка снята, **все refresh-токены покупателя отозваны**, выданы новые; почта считается подтверждённой. Слабый PIN — 422 (до проверки кода, попытка не тратится); неверный или устаревший код — 401 `invalid_code`. Код регистрации для сброса не подходит (и наоборот) |
| GET | `/auth/invitations/{token}` | — | `{ organizationName, fullName, email, role, expiresAt }` — что показать на странице приглашения; 404 — недействительно, использовано или просрочено |
| POST | `/auth/invitations/accept` | `{ token, password }` | только ссылки из старых писем: `AuthResponse`; 401 — приглашение недействительно или использовано. Пароль обязателен (от 8 символов, цифра, строчная и заглавная буквы; иначе 422) |
| POST | `/auth/refresh` | `{ refreshToken }` или без тела (токен из cookie `sitora_rt`) | `AuthResponse`. Сессия **скользящая**: новый refresh-токен живёт ещё `Jwt__RefreshTokenDays` (30) дней от этого обновления и наследует вход (устройство, дату входа). Отозванный выходом или «закрыть вход» токен — 401; повторно предъявленный токен, уже **заменённый ротацией**, — 401 и отзыв всех сессий пользователя (кража) |
| POST | `/auth/logout` | `{ refreshToken }` или без тела (cookie) | 204: отозван refresh-токен этого устройства, cookie стёрта |
| GET | `/auth/sessions` | — | любой вошедший: «Устройства» — активные входы `[{ id, device, startedAt, lastActiveAt, expiresAt, current }]`; `device` — «Chrome · Windows», «Safari · iPhone» по User-Agent (null — не распознано); `current` — вход этого access-токена (claim `sid`); текущий первый |
| DELETE | `/auth/sessions/{id}` | — | 204: вход закрыт (его refresh-токены отозваны, устройство выйдет не позже чем через 15 минут); чужой или уже закрытый — 404 `session_not_found`. Аудит `SessionRevoked` |
| POST | `/auth/sessions/revoke-all` | — | 200 `{ revoked }`: «Выйти на всех устройствах» — отозваны все refresh-токены пользователя, включая этот; cookie стёрта. Аудит `SessionsRevokedAll` |
| GET | `/auth/me` | — | `CurrentUser` |
| POST | `/auth/login` | `{ email, password }` | сотрудник: 200 `AuthResponse`, с включённой 2FA — 200 `TwoFactorRequired`. Покупатель — 400 «Используйте вход по коду». Неверный пароль или незнакомая почта — 401 «Неверная почта или пароль»; после 5 ошибок подряд — блокировка на 15 минут (401 «Слишком много неудачных попыток…», даже с верным паролем) |
| POST | `/auth/2fa/verify` | `{ tempToken, code }` | `AuthResponse`. `tempToken` живёт 5 минут, 5 попыток; неверный код — 401 |
| POST | `/auth/password/reset-request` | `{ email }` | 200 `{ message }` — всегда одинаковый. Письмо со ссылкой `…/login#reset=<token>` уходит только активному сотруднику; ссылка живёт 24 часа, новая отменяет прежние |
| POST | `/auth/password/reset` | `{ token, newPassword }` | 204: пароль сменён, все сессии сотрудника закрыты, блокировка входа снята. Пароль — от 8 символов, с цифрой, строчной и заглавной; слабый пароль, использованная или устаревшая ссылка — 422 |
| GET | `/auth/2fa/setup` | — | сотрудник: `{ qrCodeUri, manualKey }`, `qrCodeUri = otpauth://totp/Binosoz:{email}?secret={key}&issuer=Binosoz`. 2FA уже включена — 409; покупатель — 403 |
| POST | `/auth/2fa/enable` | `{ code }` | 204; дальше вход по паролю спрашивает код. Неверный код — 422. Аудит `TwoFactorEnabled` |
| POST | `/auth/2fa/disable` | `{ code }` | 204, ключ сбрасывается. Неверный код или 2FA не включена — 422. Аудит `TwoFactorDisabled` |
| POST | `/auth/sms/send`, `/auth/sms/verify` | `{ phone }`, `{ phone, code }` | вход покупателя по телефону из договора. С `Telegram__Gateway__Token` код приходит **в Telegram** (Telegram Gateway): 6 цифр, 10 минут (`expiresInSeconds: 600`), 5 попыток, не чаще раза в минуту (429 `code_cooldown`) и 5 в час (429 `code_rate_limited`) на номер и на IP; незнакомый номер — тот же ответ без отправки; номер без Telegram — 422 `telegram_no_account` «На этот номер нет Telegram — войдите по почте»; суточный лимит `Telegram__Gateway__DailyLimit` (200) — 503 `telegram_gateway_limit`; сбой шлюза — 503 `telegram_gateway_failed`. Без Gateway — SMS: выключены (404), включаются `Auth__SmsLoginEnabled=true`; без SMS-провайдера (заглушка вне Development) `/sms/send` — 503 `sms_not_configured` |
| POST | `/telegram/gateway/report` | отчёт Telegram Gateway | только с верной подписью `X-Request-Timestamp` + `X-Request-Signature` (HMAC-SHA256, ключ — SHA-256 токена), иначе 404; статус доставки пишется к коду. Адрес задаётся `Telegram__Gateway__CallbackUrl` |

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
  role: "Owner",                    // главная: SuperAdmin > Owner > ObjectAdmin > Manager > Accountant > Content > Customer
  organizationId: "…" | null,       // null у SuperAdmin вне режима и у покупателя
  organizationName: "…" | null,
  buildingIds: [],                  // пусто — все дома организации; у ObjectAdmin ровно один
  impersonation: null | { impersonatedBy, organizationId }
}

TwoFactorRequired = { requiresTwoFactor: true, tempToken, expiresAt }
```

В access-токене те же данные — в claims `role`, `org`, `bld` (по одному на дом) и `imp`. Если владелец меняет сотруднику дома, все сессии сотрудника отзываются: новые права действуют со следующего входа.

## AI-помощник

| Метод | Путь | Тело | Ответ |
| --- | --- | --- | --- |
| POST | `/ai/chat` | `{ message, history: [{ role: "user"|"assistant", content }] }` | `{ reply }`. Только для вошедших. Промпт выбирается по роли из токена: покупателю — вопросы о квартире, сделке и сроках (в начало промпта — данные его действующих сделок, до трёх, и тот же публичный контекст ЖК, что у гостя на сайте: удобства и контакты — только из базы), сотрудникам — документы, CRM, письма. Оба промпта: Таджикистан, валюта TJS/USD, ответ на языке вопроса (русский или таджикский). Модель `claude-haiku-5-5`, до 1000 токенов. История хранится только в браузере (до 20 реплик). 503 — нет ключа `Claude:ApiKey` («AI помощник не настроен») или Claude недоступен. Не больше 60 сообщений в час на пользователя |
| GET | `/public/{orgSlug}/ai/status` | — | Без входа. `{ enabled }`: `false` — не задан `Claude:ApiKey`, сайт не показывает пузырь помощника. Неизвестная или приостановленная организация — 404 |
| POST | `/public/{orgSlug}/ai/chat` | `{ message, history: [{ role: "user"|"assistant", content }] }` | Без входа (`[AllowAnonymous]`; токен не нужен и не учитывается) — помощник гостя на публичном сайте застройщика: только вопросы о покупке квартиры в этом ЖК, на остальное — вежливый отказ. В промпте — только публичные данные организации из базы (контакты, дома, свободные квартиры и цены от/до по числу комнат, удобства, эко-паспорт, рассрочка, готовность стройки, курс НБТ; кэш 60 с), без клиентов, сделок и броней. `message` ≤ 500 символов (больше — 400 `ai_question_too_long`, пустой — 400 `question_required`); из `history` берутся 10 последних реплик, только роли user/assistant; ответ до 400 токенов. 429 `ai_public_busy` «Помощник сейчас занят, напишите менеджеру» + `contacts: { phone?, telegram?, email?, address? }` — лимит по IP (`AiPublic`: 6 в минуту и 40 в сутки, `RateLimiting__AiPublicPerMinute`/`AiPublicPerDay`) или общий суточный лимит организации (`Claude:PublicDailyLimit`, по умолчанию 300). 404 — нет организации; 503 `ai_not_configured` — нет ключа |
| POST | `/ai/chat?dealId=` | то же | Необязательный `dealId` (только сотрудникам): в начало промпта сотрудника — покупатель, квартира и этаж, дом (плановый год сдачи, готовность стройки по этапам), цена, этап, оплачено и остаток, закрыто платежей из всех, просрочка (число, сумма, самая ранняя дата) и следующий платёж (дата, сумма). Чужая сделка — 404; у покупателя параметр не учитывается |
| POST | `/documents/{id}/verify?expectedFullName=` | — | Manager, руководители. `{ isValid, extractedFullName, expiryDate, nameMatches, isExpired, issue, rawResponse }` — Claude читает загруженный документ (изображение или PDF); ФИО по умолчанию — покупателя сделки. `isValid` = ФИО совпадает и срок не истёк (срок сверяется с сегодняшней датой на сервере). Аудит `Document` / `AiVerified` — только вывод проверки. 503 — ИИ не настроен или недоступен; лимит как у чата |
| POST | `/deals/{dealId}/documents/verify` | multipart `file` (JPG, PNG, GIF, WebP до 5 МБ или PDF до 10 МБ), `expectedFullName?` | Manager, руководители. То же для нового файла; файл не сохраняется, аудит `Deal` / `AiDocumentVerified` |
| POST | `/deals/{dealId}/payments/read-receipt` | multipart `file` | Manager, Accountant, руководители. `{ receipt: { isReadable, amount, currency, paymentDate, description, bankName, rawResponse }, suggestedScheduleItemId, suggestedDueDate, suggestedAmount, suggestedCurrency }`. Подходящий пункт графика — неоплаченный, остаток отличается от суммы чека меньше чем на 1 %, срок в пределах 30 дней от даты оплаты, та же валюта. Платёж не создаётся: его вносит бухгалтер обычным `POST /deals/{id}/payments` |

## Платформа (только SuperAdmin)

| Метод | Путь | Тело / ответ |
| --- | --- | --- |
| GET | `/platform/dashboard` | `{ totalOrganizations, activeOrganizations, totalBuildings, totalDeals, totalPaymentsSum, totalCustomers, totalStaff, platformRevenueMonth, newOrganizationsMonth, newDealsMonth, topOrganizations: [{ id, name, dealsCount, paymentsSum }], dealsByMonth: [{ month: "2026-11", count, sum }] }`. Суммы в сомони (USD — по последнему курсу НБТ); сделки без отменённых; «месяц» — с 1-го числа по Душанбе; доход платформы — оплаченные тарифы, действующие в этом месяце; `dealsByMonth` — 12 месяцев по текущий, пустые с нулями; топ-5 — по числу сделок |
| GET | `/platform/organizations` | `[{ id, name, slug, isActive, buildingsCount, usersCount, createdAt, customDomain, logoUrl, ownerEmails, buildingNames, dealsCount, customersCount, lastActivityAt, tariff, owners: [{ id, fullName, email, phone }] }]`. `lastActivityAt` — последняя запись журнала организации; `tariff` — действующий тариф или null |
| GET | `/platform/organizations/{id}` | `{ organization, paymentsSumTjs, apartmentsCount, freeApartments, soldApartments, buildings: [{ id, name, apartmentsCount, freeApartments }], tariffHistory[] }`; неизвестная — 404 |
| PUT | `/platform/organizations/{id}` | `{ name, slug? }` → карточка. Короткое имя занято — 409, пустое название — 422. Аудит `Updated` |
| DELETE | `/platform/organizations/{id}/suspend` | 204: приостановить (данные не удаляются) — как `PATCH …/status { isActive: false }`, аудит `Suspended` |
| POST | `/platform/organizations/{id}/activate` | 204: снова открыть, аудит `Activated` |
| POST | `/platform/organizations/{id}/invite-owner` | `{ email, firstName, lastName }` → 201 `UserResponse`: учётная запись Owner, пароль приходит письмом. Почта занята — 409, без имени или фамилии — 422, лимит тарифа — 402 |
| PATCH | `/platform/organizations/{id}/status` | `{ isActive }` → 204. Приостановка (`false`): вход сотрудникам и сайт застройщика закрыты, refresh-токены сотрудников отзываются; `true` — снова работает. В аудите `Suspended` / `Activated` |
| POST | `/platform/organizations` | `{ name, slug, ownerEmail, ownerFullName }` → 201. Владельцу уходит письмо с логином и сгенерированным паролем. `slug`: латиница, цифры, дефис |
| POST | `/platform/organizations/{id}/owners` | `{ email, fullName, password? }` → 201 `UserResponse`: ещё один владелец организации. Без пароля он генерируется и уходит письмом |
| POST | `/platform/impersonate/{id}` | `id` — организация: вход «как владелец» (`user.role = "Owner"`). `id` — пользователь: вход под ним самим, с его ролью и домами. Ответ — `AccessTokenResponse = { accessToken, accessTokenExpiresAt, user }` без refresh-токена, `user.impersonation` заполнено. Под создателем платформы войти нельзя (422) |
| POST | `/platform/impersonate/stop` | `AccessTokenResponse` — обычный токен создателя платформы |
| GET | `/platform/applications?status=&page=&pageSize=` | `PagedResult` заявок застройщиков: `{ id, companyName, contactEmail, contactPhone, ownerFullName, comment, status, reviewNote, createdAt, reviewedAt, organizationId }`. `status`: `Pending`, `Approved`, `Rejected`; без него — все |
| POST | `/platform/applications/{id}/approve` | `{ organizationSlug }` → `{ organizationId, message }`: создаётся организация с владельцем (как `POST /platform/organizations`), владельцу уходит письмо со входом. Уже рассмотренная заявка или занятое короткое имя — 409 |
| POST | `/platform/applications/{id}/reject` | `{ reason }` → 204, заявителю уходит письмо с причиной. Уже рассмотренная — 409 |
| GET | `/platform/users?role=&organizationId=&search=&page=&pageSize=` | `PagedResult` пользователей всей платформы: `{ id, fullName, email, phone, role, roles[], organizationId, organizationName, createdAt, lastLoginAt, status: "Active" \| "Blocked", dealsCount }`. `lastLoginAt` — последний вход по журналу; `dealsCount` — сделки покупателя. Поиск по имени, почте, телефону; неизвестная роль — 422 |
| GET | `/platform/users/{id}` | тот же объект; неизвестный — 404 |
| POST | `/platform/users/{id}/block` | заблокировать: учётная запись выключена, блокировка входа на 100 лет, сессии отозваны. Себя и суперадмина — 422. Аудит `Blocked` |
| POST | `/platform/users/{id}/unblock` | разблокировать, счётчик неудачных попыток сброшен. Аудит `Unblocked` |
| POST | `/platform/users/{id}/reset-password` | 204: новый пароль (8 букв и цифр) приходит письмом «Binosoz: новый пароль», прежний не действует, сессии закрыты. Покупателю — 422 (он входит по коду). Аудит `PasswordResetBySuperAdmin` |
| GET | `/platform/tariffs` | `[{ id, name, price, currency, maxBuildings, maxUsers, features[], isActive, organizationsCount }]`; `maxBuildings`/`maxUsers` = null — без ограничения. Базовые: Free (0 TJS, 1 дом, 5 сотрудников), Basic (500 TJS, 3 / 20), Pro (1500 TJS, без ограничений) |
| POST | `/platform/tariffs` | `{ name, price, currency?, maxBuildings?, maxUsers?, features?, isActive? }` → 201. Название занято — 409; цена < 0, лимит < 1 — 422 |
| PUT | `/platform/tariffs/{id}` | то же тело → 200. Отключённый тариф нельзя назначить, у кого он есть — остаётся |
| POST | `/platform/organizations/{id}/tariff` | `{ tariffPlanId, endsAt?, isPaid? }` → `{ id, organizationId, tariffPlanId, tariffName, price, currency, startsAt, endsAt, isPaid }`. Тариф действует с текущего момента, прежний закрывается. Отключённый тариф — 422. Аудит `TariffAssigned` |

| GET | `/platform/activity?organizationId=&action=&from=&to=&page=&pageSize=` | `PagedResult` журнала всех организаций, новые первыми: `{ id, at, action, entity, entityId, organizationId, organizationName, userEmail, ip, impersonated }`. `from`/`to` — UTC, интервал `[from; to)`; `pageSize` до 100 (по умолчанию 50). `userEmail` у входа и других действий до аутентификации — почта самого пользователя из записи. `ip` — адрес запроса (за Caddy — из X-Forwarded-For), у старых записей и фоновых задач — null |
| GET | `/platform/activity/actions` | `string[]` — действия, которые уже есть в журнале (для фильтра) |
| POST | `/platform/notifications` | `{ organizationId?, type: "info" \| "warning" \| "error", title, message }` → `{ recipients }`. Уведомление в колокольчик (`kind = "platform_" + type`) каждому активному пользователю организации; без `organizationId` — пользователям всех организаций (покупатели без организации не получают). Неизвестный тип, пустой заголовок/текст, заголовок > 200 или текст > 2000 — 422; неизвестная организация — 404. Аудит `PlatformNotification` / `Sent` |
| GET | `/platform/settings` | `{ maxOrganizations, demoMode, version, contactEmail, organizations }`. `version` — версия сборки и коммит («1.0.0 (9b8b29d)»), только чтение; `organizations` — сколько их сейчас |
| PATCH | `/platform/settings` | `{ maxOrganizations?, clearMaxOrganizations?, demoMode?, contactEmail? }` → те же настройки. Меняются только переданные поля; `clearMaxOrganizations: true` снимает лимит, пустой `contactEmail` — удаляет почту. Лимит < 1, > 100 000 или меньше числа организаций, неверная почта — 422. Аудит `PlatformSettings` / `Updated`. Заданный лимит проверяется при создании организации и одобрении заявки (422) |
| GET | `/platform/reviews?page=&pageSize=` | `PagedResult` всех отзывов платформы (`ReviewResponse` с `buildingName`, `organizationName`) |
| DELETE | `/platform/reviews/{id}` | 204: удалить навсегда; копия — в журнале (`Review` / `Deleted`) |

## Отзывы о доме

| Метод | Путь | Кто | Тело / ответ |
| --- | --- | --- | --- |
| GET | `/buildings/{id}/reviews` | все, без входа | опубликованные: `[{ id, buildingId, rating, text, authorName, ownerReply, createdAt, repliedAt }]`; автор — только имя |
| POST | `/buildings/{id}/reviews` | Customer | `{ rating: 1–5, text: до 1000 }` → 201. Нужна сделка в этом доме (не отменённая), иначе 403; второй отзыв на дом — 409; неизвестный дом — 404 |
| PUT | `/buildings/{id}/reviews/{rid}` | Customer (свой) | то же тело; после публикации — 422, чужой — 404 |
| GET | `/buildings/{id}/reviews/all` | Manager, руководители дома | все отзывы с `isPublished`, `aiReply`, `customerId`, `customerFullName` |
| POST | `/buildings/{id}/reviews/{rid}/reply` | Manager, руководители | `{ text }` (до 2000) → отзыв с `ownerReply`, `repliedAt` |
| POST | `/buildings/{id}/reviews/{rid}/ai-reply` | Manager, руководители | черновик ответа от Claude (`claude-haiku-5-5`) в `aiReply`, не публикуется; нет ключа Claude — 503 |
| POST | `/buildings/{id}/reviews/{rid}/publish`, `…/hide` | Manager, руководители | показать или скрыть на сайте. Аудит `Published` / `Hidden` |

**Лимиты тарифа.** Создание дома (`POST /buildings`) и сотрудника (`POST /invitations`, `POST /users`, `POST /platform/organizations/{id}/owners`) сверяются с действующим тарифом организации: дома — все дома организации, сотрудники — активные учётные записи организации. Лимит исчерпан — **402** «Тариф «…» позволяет не больше N … Перейдите на тариф выше». У организации без назначенного тарифа ограничений нет.

Заявка застройщика без входа (страница `/register`):

| Метод | Путь | Тело / ответ |
| --- | --- | --- |
| POST | `/public/applications` | `{ companyName, contactEmail, contactPhone, ownerFullName, comment? }` → `{ applicationId, message }`. Телефон приводится к `+992…`, неверный — 422; неверный формат почты или названия — 400; ещё одна заявка с той же почты, пока прежняя на рассмотрении (24 часа), — 409. Не больше 3 заявок в час с одного IP (`RateLimiting:ApplicationsPerHour`), дальше 429. Заявителю уходит HTML-письмо «Заявка принята — Binosoz» |

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

Любая ошибка — Problem Details (RFC 9457): `status`, русский `title` и `detail` (таджикский при `Accept-Language: tg`), машиночитаемый `code` (snake_case, стабильный) и `traceId` — тот же TraceId, что в записи лога о запросе. Ответ 500 — без стектрейса и внутренних сообщений, только `traceId`.

```json
{ "status": 409, "title": "Конфликт", "detail": "Квартира забронирована другим покупателем",
  "code": "apartment_already_booked", "traceId": "4bf92f3577b34da6a3ce929d0e0e4736" }
```

| Код | Тело | Что показывает интерфейс |
| --- | --- | --- |
| 400 | `ValidationProblemDetails`: `title: "Проверьте введённые данные"`, `code: "validation_failed"`, `errors: { "phone": ["…"] }` — ключи в camelCase, по одному сообщению на поле, без английских текстов. Битый JSON или пустое тело — `code: "malformed_request"`, `errors: { "body": ["Некорректный формат запроса"] }` без текста парсера | Текст под соответствующим полем; если поле не найдено — тексты из `errors` над формой |
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

### Коды ошибок

По коду ответа (если у ошибки нет своего): `bad_request` 400, `unauthorized` 401 («Требуется вход»), `tariff_limit` 402, `forbidden` 403 («Недостаточно прав»), `not_found` 404, `route_not_found` 404 (адреса нет), `method_not_allowed` 405, `conflict` 409, `payload_too_large` 413, `unsupported_media_type` 415, `business_rule` 422, `rate_limited` 429, `not_implemented` 501, `service_unavailable` 503, `internal_error` 500.

Свои коды ошибок домена (полный список с таджикскими текстами — `backend/src/Sitora.WebApi/Errors/ErrorCatalog.cs`):

| code | Русский текст (примеры) |
| --- | --- |
| `account_blocked` | Учётная запись заблокирована |
| `telegram_not_configured` | Бот Telegram не настроен: уведомления в Telegram пока недоступны (503) |
| `telegram_no_account` | На этот номер нет Telegram — войдите по почте (422) |
| `telegram_gateway_limit` | Коды в Telegram на сегодня закончились. Войдите по почте (503) |
| `telegram_gateway_failed` | Telegram не принял код. Попробуйте позже или войдите по почте (503) |
| `reminder_too_soon` | Напоминание по этому платежу уже отправляли сегодня… (429) |
| `reminder_item_paid` | Этот платёж уже оплачен или его нет в графике (422) |
| `reminder_no_cabinet` | У покупателя нет входа в кабинет: напоминание некуда отправить (422) |
| `session_not_found` | Вход не найден или уже закрыт (404) |
| `address_required` | Укажите адрес |
| `ai_no_answer` | AI не прислал ответ, попробуйте ещё раз |
| `ai_not_configured` | AI помощник не настроен; Проверка документов ИИ не настроена |
| `ai_unavailable` | Проверка документов ИИ временно недоступна, попробуйте позже |
| `apartment_already_booked` | Квартира забронирована другим покупателем; Квартира уже забронирована или недоступна |
| `apartment_not_found` | Квартира не найдена |
| `apartment_not_sellable` | Квартиру в текущем статусе нельзя продать |
| `apartment_required` | Укажите квартиру |
| `apartment_sold` | Квартира уже продана |
| `application_not_found` | Заявка не найдена |
| `application_pending` | Заявка с этой почты уже на рассмотрении. Мы свяжемся с вами |
| `application_reviewed` | Заявка уже рассмотрена |
| `bad_request` | Неверный запрос |
| `booking_conflict` | На этот номер уже есть активная бронь. Чтобы забронировать другую квартиру, свяжитесь с менеджером |
| `booking_inactive` | Бронь уже не активна |
| `booking_ip_limit` | С этого адреса уже есть несколько активных броней. Чтобы забронировать ещё, свяжитесь с менеджером (429) |
| `booking_not_found` | Бронь не найдена |
| `building_not_found` | Дом не найден |
| `building_required` | Укажите дом, в котором заводите клиента; Укажите дом или квартиру, к которой относится заявка … |
| `business_rule` | Нарушено бизнес-правило |
| `cannot_block_self` | Нельзя заблокировать самого себя |
| `cannot_block_superadmin` | Суперадмина заблокировать нельзя |
| `cannot_impersonate_owner` | Нельзя войти под создателем платформы |
| `challenge_expired` | Время на подтверждение входа истекло, войдите заново |
| `code_cooldown` | Код уже отправлен. Запросить новый можно через минуту |
| `code_rate_limited` | Слишком много запросов кода. Попробуйте позже |
| `code_required` | Укажите код из SMS |
| `comment_too_long` | Комментарий — не длиннее 1000 символов |
| `concurrency_conflict` | Платёж уже изменился, повторите запрос |
| `conflict` | Конфликт |
| `customer_account_failed` | Не удалось создать учётную запись покупателя:  |
| `customer_no_password` | Покупатели входят по коду из почты — пароль им не нужен |
| `customer_not_found` | Клиент не найден |
| `customer_phone_taken` | Клиент с таким телефоном уже есть у этого застройщика |
| `customer_required` | Укажите клиента |
| `date_required` | Укажите дату курса; Укажите дату начала графика … |
| `deal_cancelled` | Сделка отменена |
| `deal_closed` | Сделка закрыта, этап менять нельзя |
| `deal_exists` | По этой квартире уже есть открытая сделка |
| `deal_not_found` | Сделка не найдена; Сделка не найдена в этом доме |
| `deal_paid` | По сделке всё оплачено |
| `deal_same_stage` | Сделка уже на этом этапе |
| `description_too_long` | Описание на русском не длиннее 4000 символов; Описание на таджикском не длиннее 4000 символов |
| `discount_reason_required` | Укажите причину скидки |
| `document_not_found` | Документ не найден |
| `domain_taken` | Этот домен уже занят другой организацией |
| `email_not_configured` | Письма с кодами не отправляются: почтовый сервис не настроен (503) |
| `email_is_staff` | Эта почта принадлежит сотруднику. Войдите на вкладке «Сотрудник» |
| `email_required` | Укажите почту |
| `email_taken` | Пользователь с такой почтой уже есть; Почта уже зарегистрирована — войдите или восстановите код |
| `error` | Ошибка |
| `file_not_found` | Файл не найден |
| `floor_not_found` | Этаж не найден |
| `floor_plan_not_found` | План этажа не задан |
| `floor_required` | Укажите этаж |
| `forbidden` | Недостаточно прав; Доступ запрещён |
| `full_name_required` | Укажите ФИО |
| `gateway_not_found` | Платёжный шлюз не найден |
| `gateway_payment_not_found` | Платёж не найден в платёжном шлюзе |
| `impersonation_disabled` | Режим «войти как владелец» не включён |
| `import_conflict` | Пока шёл импорт, такой телефон завели вручную. Загрузите файл ещё раз |
| `import_empty` | В файле нет строк с квартирами |
| `import_forbidden` | Импорт доступен сотрудникам со всеми домами организации или с одним домом |
| `internal_error` | Внутренняя ошибка сервера; Произошла ошибка. Мы уже разбираемся; если повторится, сообщите номер ошибки (traceId) |
| `invalid_amount` | Сумма договора должна быть больше нуля; Сумма оплаты должна быть больше нуля … |
| `invalid_apartment_number` | Номер квартиры должен быть от 1 до 99999 |
| `invalid_apartments_count` | Квартир в доме — от 1 до 5000 |
| `invalid_area` | Площадь должна быть больше нуля |
| `invalid_code` | Неверный или устаревший код; Код состоит из 6 цифр |
| `invalid_pin` | Неверная почта или код входа. Осталось попыток: N (`attemptsLeft`) |
| `pin_locked` | Вход по коду временно заблокирован. Минут до разблокировки: N (`lockedUntil`, `retryAfterSeconds`) |
| `pin_not_set` | Код входа ещё не задан. Нажмите «Забыли код?»…; Эта почта уже есть в кабинете — задайте код входа через «Забыли код?» |
| `pin_format` | Код входа — ровно 6 цифр |
| `pin_weak` | Код слишком простой: не используйте одинаковые цифры; …цифры подряд, как 123456 или 654321 |
| `pin_required` | Придумайте код входа; Введите код входа |
| `code_required` | Введите код из письма |
| `email_not_confirmed` | Почта не подтверждена. Введите код из письма или запросите новый |
| `invalid_color` | Цвет — в формате #RRGGBB, например #1f6feb |
| `invalid_credentials` | Неверная почта или пароль |
| `invalid_currency` | Курс задаётся для иностранной валюты, сомони — базовая валюта; Курс задаётся только для USD |
| `invalid_date` | Дата окончания тарифа должна быть в будущем; Дата оплаты не может быть в будущем … |
| `invalid_discount` | Скидка должна быть не меньше нуля и меньше цены; Скидка не может быть отрицательной |
| `invalid_domain` | Домен — например richtower.tj, без https:// и пути; Поддомен платформы задаётся коротким именем, а не своим доменом |
| `invalid_down_payment` | Первоначальный взнос должен быть от 0 до 100% (не включая 100) |
| `invalid_email` | Неверный адрес почты владельца; Неверный адрес почты для связи … |
| `invalid_file` | Не удалось прочитать файл Excel; Не удалось прочитать файл. Нужен файл Excel в формате .xlsx … |
| `invalid_floor_number` | Номер этажа должен быть от 0 до 100 |
| `invalid_floor_plan` | Пустой шаблон плана; Шаблон должен начинаться с <svg> |
| `invalid_floors_count` | Этажность должна быть от 1 до 100 |
| `invalid_limit` | Максимум организаций — от 1 до 100 000; Лимит — не меньше 1; пусто — без ограничения |
| `invalid_manager` | Ответственным можно назначить только активного менеджера этого застройщика |
| `invalid_name` | Название организации — от 1 до 200 символов; Название тарифа — от 1 до 50 символов … |
| `invalid_period` | Дата окончания периода раньше даты начала |
| `invalid_phone` | Неверный номер телефона; Неверный номер телефона, пример: +992 90 123 45 67 |
| `invalid_price` | Цена тарифа — от 0 до 1 000 000 000; Цена за м² должна быть больше нуля |
| `invalid_progress` | Процент готовности должен быть от 0 до 100 |
| `invalid_rating` | Оценка — от 1 до 5 |
| `invalid_role` | Роль должна быть одной из:  |
| `invalid_rooms` | Комнат должно быть от 0 (студия) до 10 |
| `invalid_stage` | Новую сделку нельзя создать сразу завершённой или отменённой |
| `invalid_telegram_chat` | Неверный chat id Telegram |
| `invalid_term` | Срок слишком большой для такой суммы |
| `invalid_text` | Ответ — от 1 до 2000 символов; Текст шаблона — от 1 до 1000 символов |
| `invalid_totp` | Неверный код из приложения |
| `invalid_type` | Тип уведомления — информация, предупреждение или ошибка; Тип шаблона — платёж, бронь или приветствие |
| `invalid_url` | Ссылка на 3D-модель должна быть полным адресом http(s) |
| `invalid_year` | Год сдачи — от 2000 до 2100 |
| `invitation_accepted` | Приглашение уже принято |
| `invitation_invalid` | Приглашение недействительно или уже использовано |
| `invitation_not_found` | Приглашение не найдено |
| `lead_accepted` | Заявка принята. Мы свяжемся с вами в ближайшее время. |
| `link_invalid` | Ссылка недействительна или устарела |
| `login_locked` | Слишком много неудачных попыток входа, попробуйте позже |
| `logo_not_found` | Логотип не найден |
| `malformed_request` | Некорректный формат запроса |
| `manager_only` | Бронировать на клиента может только менеджер |
| `message_required` | Напишите сообщение |
| `method_not_allowed` | Метод не поддерживается для этого адреса; Метод не поддерживается |
| `name_required` | Укажите имя и фамилию (вместе до 200 символов); Укажите имя |
| `name_ru_required` | Укажите название на русском |
| `name_tg_required` | Укажите название на таджикском |
| `not_found` | Не найдено; Объект не найден … |
| `notification_not_found` | Уведомление не найдено |
| `object_admin_one_building` | Админу объекта назначается ровно один дом |
| `object_admin_scope` | Админ объекта назначает сотрудников только в свой дом; Админ объекта управляет только сотрудниками своего дома |
| `online_payment_not_found` | Онлайн-оплата не найдена |
| `operation_already_reversed` | Операция уже сторнирована |
| `operation_not_found` | Операция не найдена |
| `organization_choice_required` | Выберите организацию: войдите как её владелец |
| `organization_disabled` | Организация отключена. Обратитесь к владельцу платформы; Организация отключена |
| `organization_mismatch` | Клиент и квартира принадлежат разным застройщикам |
| `organization_not_found` | Организация не найдена |
| `organization_required` | Укажите организацию; Укажите застройщика (organization) |
| `owner_all_buildings` | Владельцу доступны все дома организации |
| `owner_name_required` | Укажите имя владельца; Укажите имя и фамилию владельца |
| `password_required` | Задайте пароль: сотрудники входят по почте и паролю; Укажите пароль |
| `password_reset_sent` | Если почта принадлежит сотруднику, мы отправили на неё ссылку для смены пароля |
| `password_too_short` | Пароль — не короче 8 символов |
| `payload_too_large` | Слишком большой запрос; Размер запроса превышает допустимый |
| `payment_already_reversed` | Платёж уже сторнирован |
| `payment_not_found` | Платёж не найден |
| `payment_processed` | Платёж уже обработан банком |
| `plan_slot_swap` | Квартиры меняются позициями на плане. Загрузите изменение в два шага: сначала на свободные позиции |
| `platform_owner_only` | Доступно только создателю платформы |
| `question_required` | Напишите вопрос |
| `rate_limited` | Слишком много запросов; Слишком много запросов. Попробуйте позже |
| `rate_missing` | Нет курса USD. Бухгалтер должен ввести курс, прежде чем оформлять сделку в долларах; Курс ещё не задан |
| `reason_required` | Для возврата сделки на прошлый этап или отмены укажите причину; Для отмены сделки укажите причину … |
| `refresh_token_required` | Укажите refresh-токен |
| `report_not_found` | Отчёт не найден |
| `reset_link_invalid` | Ссылка для смены пароля недействительна или устарела. Запросите новую |
| `reversal_of_reversal` | Сторно нельзя сторнировать |
| `review_exists` | Вы уже оставили отзыв об этом доме; Вы уже оставили отзыв об этом доме. Его можно изменить, пока он не опубликован |
| `review_forbidden` | Оставить отзыв может только покупатель со сделкой в этом доме |
| `review_not_found` | Отзыв не найден |
| `review_published` | Опубликованный отзыв изменить нельзя |
| `role_required` | Укажите роль |
| `route_not_found` | Такого адреса нет |
| `sms_not_configured` | SMS с кодами пока не отправляются. Войдите по почте (503) |
| `schedule_locked` | По сделке уже есть платежи, график менять нельзя |
| `schedule_missing` | График платежей ещё не составлен; Сначала составьте график платежей |
| `service_unavailable` | Сервис недоступен |
| `session_expired` | Сессия истекла, войдите заново |
| `site_not_found` | Сайт застройщика не найден |
| `slug_required` | Укажите короткое имя организации |
| `slug_taken` | Организация с таким коротким именем уже есть; Это короткое имя уже занято другой организацией |
| `staff_use_password` | Сотрудники входят по паролю |
| `stage_name_required` | Укажите название этапа |
| `stage_not_found` | Этап стройки не найден |
| `tariff_disabled` | Тариф отключён: назначить его нельзя |
| `tariff_limit` | Нужен тариф выше |
| `tariff_name_taken` | Тариф с таким названием уже есть |
| `tariff_not_found` | Тариф не найден |
| `template_name_taken` | Шаблон с таким названием уже есть |
| `template_not_found` | Шаблон не найден |
| `text_required` | Введите текст уведомления |
| `title_required` | Введите заголовок уведомления |
| `two_factor_disabled` | 2FA не включена |
| `two_factor_enabled` | 2FA уже включена; 2FA уже включена. Чтобы получить новый ключ, сначала отключите её |
| `two_factor_impersonation` | В режиме «войти как владелец» 2FA не настраивается |
| `two_factor_setup_required` | Сначала получите ключ: GET /auth/2fa/setup |
| `two_factor_staff_only` | 2FA — для сотрудников: покупатели входят по коду из почты |
| `unauthorized` | Требуется вход |
| `unknown_role` | Неизвестная роль:  |
| `unsupported_media_type` | Неподдерживаемый тип содержимого; Отправьте данные в формате JSON |
| `use_code_login` | Используйте вход по коду |
| `user_blocked` | Пользователь заблокирован |
| `user_not_found` | Пользователь не найден |
| `user_without_email` | У пользователя нет почты: новый пароль некуда отправить |
| `validation_failed` | Проверьте введённые данные; Заполните обязательное поле … |

## Каталог (без входа)

- `GET /buildings` без входа требует `organization` (короткое имя застройщика): без него — 400 `organization_required` «Укажите организацию», неизвестная или отключённая организация — 404 `organization_not_found`. Сотрудник со входом без `organization` видит дома своей организации.
- Квартира, бронь которой истекла, а фоновая задача её ещё не сняла, в каталоге уже `Free`; новая бронь снимает старую сама.
- `POST /payment/webhook/{provider}` — только `alif` и `korti-milli` (пока 501); другие провайдеры — 404.

| Метод | Путь | Ответ | `api.js` |
| --- | --- | --- | --- |
| GET | `/buildings?organization=slug` | страница `BuildingResponse`. Сайт застройщика передаёт свой `slug` (у организации по умолчанию — `nekruz`). Без параметра — 400 `organization_required`, неизвестный `slug` — 404 `organization_not_found`. С токеном сотрудника — только его дома | `buildings()` |
| GET | `/buildings/{id}` | `BuildingResponse` | `building()` |
| GET | `/buildings/{id}/floors` | страница `FloorResponse` | `floors()` |
| GET | `/floors/{id}/apartments` | страница `ApartmentResponse` | `floorApartments()` |
| GET | `/apartments/{id}` | `ApartmentResponse` | `apartment()` |
| GET | `/rates/current?currency=USD&organization=slug` (или `&organizationId=`) | `{ date, currency, rateToTjs, source }`. Ручной курс у каждой организации свой, поэтому сайт передаёт slug или id организации; неизвестный slug — 404. Без него — только общий курс НБТ. Сотруднику организация берётся из токена. 404, если курса нет | `rate()` → `{ rate, date }` |
| POST | `/leads` | `{ name, phone, apartmentId?, buildingId?, source, comment? }`. Организация берётся из квартиры или дома. Если на платформе несколько застройщиков, без них — 422 | `createLead()` |
| POST | `/apartments/{id}/bookings` | `{ name, phone }` (гость) или `{ customerId }` (менеджер) → `BookingResponse`; 409, если квартира занята. Бронь с сайта (гость или покупатель): лимит формы — 30 в час на IP, не больше одной активной брони на телефон и `Booking__GuestActivePerIp` (3) активных на IP — иначе 429 `booking_ip_limit` | `book()` |

Состав ответов:
- `ApartmentResponse`: `id, floorId, buildingId, floorNumber, number, rooms, area, pricePerM2, price, currency, status, planSlot (0–99), model3dId, kind, terraceArea?, blockCode?`. В блоке ЖК номер — в своей серии блока: квартиры 1…N, магазины (`kind: Commercial`) — Н-1…, машино-места (`Parking`) — М-1…; показ — «Блок A, кв. 45» / «A-45». Номер уникален в доме и серии: повтор при создании или правке — 409 `apartment_number_taken`.
- Номер с блоком в других ответах: сделки (`DealResponse`), брони, кабинет (`CabinetDealResponse`), чат (`ChatConversationResponse`), воронка CRM (`PipelineCard`), отчёты платежей и долгов — рядом с `apartmentNumber` поля `blockCode` (null у дома без блока) и `apartmentKind`; журнал аудита — `apartmentCode` («A-45»).
- `BuildingResponse`: `name` на языке запроса, `nameRu/Tg`, `descriptionRu/Tg`, `totalApartments`, `freeApartments`.

### Удобства дома и условия рассрочки

| Метод | Путь | Кто | Ответ |
| --- | --- | --- | --- |
| GET | `/buildings/{id}`, `/buildings` | все | у дома поле `amenities` — ключи удобств (только непустой список) и `installment` — `{ downPaymentMinPct?, maxMonths?, noOverpayment }` (только если условия заданы) |
| PUT | `/buildings/{id}/location` | Content, руководители | `{ latitude?, longitude?, floorHeight? }` — точка дома на карте (WGS 84, парой: обе или ни одной) и высота этажа в метрах (2,4–6); пустое — «не задано». Ошибки: `location_pair_required`, `invalid_latitude`, `invalid_longitude`, `invalid_floor_height`; аудит `LocationUpdated`. В GET дома — поля `latitude`, `longitude`, `floorHeight`, только заданные. Без координат сайт не показывает окружение и расстояния |
| GET | `/layouts` | Content, руководители | планировки своей организации `[{ code, name, layout, updatedAt }]` |
| PUT | `/layouts/{code}` | Content, руководители | `{ name?, layout }` — JSON формата planirovki-data.js: `outline` (3–64 вершины), `rooms` `[[ключ, многоугольник], …]` (1–30, внутри контура, сумма 10–1 000 м²), `balcony?`. 400: `invalid_layout_code`, `invalid_layout`, `invalid_layout_outline`, `invalid_layout_rooms`, `invalid_layout_room`, `layout_room_outside`, `invalid_layout_area`, `invalid_layout_balcony`. Аудит Created/Updated |
| DELETE | `/layouts/{code}` | Content, руководители | 204; нет — 404 `layout_not_found` |
| GET | `/public/{slug}/layouts` | гость | планировки застройщика для сайта; у квартиры — `layoutCode` |
| PUT | `/buildings/{id}/shape` | Content, руководители | `{ blockCode?, sortOrder, footprint?, sitePosition?, setbacks? }` — блок ЖК целиком: буква (1–4 латинские буквы/цифры), порядок, контур в плане (метры, `[[x, y], …]` или `[x, y, r]` — угол со скруглением r, 3–64 вершины, без самопересечений), место на участке `{ x, y, rotationDeg }`, уступы `[{ fromFloor, footprint }]`. Пустой контур — обычный дом. Ошибки 400: `polygon_vertex_count`, `polygon_invalid_point`, `polygon_self_intersection`, `polygon_zero_area`, `invalid_block_code`, `invalid_setback_floor`, `setback_above_roof`, `setback_outside_block`, `blocks_overlap`, `blocks_too_close` (разрыв меньше `Catalog__MinBlockGapMeters`, 20 м); 409 `block_code_taken`. Аудит `ShapeUpdated`. В GET дома — `blockCode`, `sortOrder`, `footprint`, `sitePosition`, `setbacks`, только заданные |
| PUT | `/buildings/{id}/published` | руководители | `{ isPublished }` — показать дом на публичном сайте или скрыть. Скрытого нет в `/buildings?organization=…` для гостя и покупателя, в `/public/brand`, AI-помощнике гостя и `/public/green-impact`; сотрудники видят его (`isPublished: false`), покупатель — свою сделку. Аудит `Published`/`Unpublished` |
| PUT | `/buildings/{id}/price-rule` | руководители | `{ currency, steps: [{ fromFloor, toFloor, pricePerM2, pricePerM2To? }], penthousePricePerM2? }` — правило цены блока: цена м² по этажам (с `pricePerM2To` — ровно по этажам ступени, до 1), пентхаусы последнего этажа — своя цена. Квартиры не меняет. Ошибки 400: `price_steps_overlap`, `price_step_reversed`, `invalid_price_step_floor`, `invalid_price_per_m2`, `too_many_price_steps`; аудит `PriceRuleUpdated`. В GET дома — `priceSteps`, `penthousePricePerM2`, `priceCurrency` |
| POST | `/buildings/{id}/price-rule/apply` | руководители | «Пересчитать цены»: цена = площадь × цена м² этажа (округление до сотни) всем **свободным** квартирам блока, брони, проданные и «под запрос» не меняются → `{ updated, unchanged, skipped }`. Каждое изменение — `PriceChanged` в AuditLog в той же транзакции, кэш каталога сбрасывается. Правила нет — 422 `price_rule_missing` |
| GET/PUT | `/owner/site-plan` | руководители | План участка ЖК своей организации: `{ boundary: [[x, y], …], zones: [{ kind, polygon }] }`, `kind` — `playground`, `football_field`, `parking_ramp`, `path`, `lawn`; пустой контур — плана нет (204). Ошибки: правила контура, `unknown_site_zone`, `too_many_site_zones`; аудит `SitePlanUpdated`. На сайте — `sitePlan` в `GET /public/brand` |
| PUT | `/buildings/{id}/amenities` | Content, руководители | `{ amenities: string[] }` — список целиком; ключи: `underground_parking, guest_parking, closed_yard, playground, sports_ground, security, cctv, concierge, elevators, landscaping, commercial_floor, storage_rooms, barrier_free, fitness, pool` (`BuildingAmenities.Keys`), порядок — как в этом списке, повторы убираются; неизвестный ключ — 400 «Неизвестное удобство дома» (`invalid_amenity`); аудит `AmenitiesUpdated`, сброс кэша каталога |
| GET/PUT | `/owner/installment-settings` | руководители организации | `{ downPaymentMinPct? (0–100), maxMonths? (1–360), noOverpayment }`; пустое — «не задано», сайт это условие не пишет; аудит `InstallmentSettingsUpdated`, сброс кэша каталога. Коды: `invalid_down_payment`, `invalid_installment_term` |

Сайт пишет удобства и условия рассрочки только из этих полей: чего нет — того на сайте нет.

| GET | `/public/buildings/{id}/progress` | все, кэш каталога | ход стройки для сайта: `{ overallPercent, stages: [{ name, percent, updatedAt }] }` без фото; неизвестный дом или неактивная организация — 404 |

### Планы этажей

| Метод | Путь | Роли | Примечание |
| --- | --- | --- | --- |
| GET | `/buildings/{id}/floorplan` | Manager, Accountant, Content, руководители | Номера этажей, для которых есть план: `[1, 5]` |
| GET | `/buildings/{id}/floorplan/{floor}` | то же | `image/svg+xml`: у `<rect id="apt-{apartmentId}">` — `fill` по статусу (Free `#22c55e`, Booked `#f59e0b`, Sold `#ef4444`, OnRequest `#6b7280`), `<title>` «Кв. N, площадь м², цена валюта», `class="fp-apt"`, `data-apartment-id`, `data-number`, `data-status` (`free`/`booked`/`sold`/`onrequest`), `data-deal-id` (действующая сделка, у брони и продажи). Квартира другого дома в шаблоне — серая, без данных. Нет плана или этажа — 404 |
| PUT | `/buildings/{id}/floorplan/{floor}` | Owner, ObjectAdmin, SuperAdmin | `{ svgTemplate }` (до 200 тыс. символов) → 204. Разрешены `svg, g, rect, text, tspan, title, desc, line, path, polygon, polyline, circle, ellipse`; `<script>`, `<foreignObject>` и прочее — 422; атрибуты `on*`, `href` и `url()` в `style` удаляются. Этаж должен существовать (иначе 404). Аудит `FloorPlan` / `Updated` |

## «Зелёная» часть

| Метод | Путь | Кто | Ответ |
| --- | --- | --- | --- |
| GET | `/buildings/{id}`, `/buildings` | все | у дома поле `eco` (эко-паспорт, только заполненные поля; нет данных — поля нет) и `isDemo` (данные застройщика демонстрационные) |
| PUT | `/buildings/{id}/eco` | Content, руководители | `{ energyClass?: APlusPlus|APlus|A|B|C|D, wallInsulationRu/Tg?, windowsRu/Tg?, solarPanelsKw?, hasHeatMeters?, hasWaterMeters?, hasLedCommonAreas?, hasEvCharging?, hasRainwaterReuse?, hasWasteSorting?, greenAreaM2?, heatingType?: Central|IndividualGas|HeatPump|Electric, estimatedKwhPerM2Year? }` — паспорт целиком, пустое — «не заполнено»; аудит `EcoUpdated`, сброс кэша каталога |
| GET/PUT | `/owner/green-settings` | руководители организации | `{ tariffKwhTjs?, baselineKwhPerM2Year?, baselineSource?, co2KgPerKgPaper?, co2Source?, isDemo }`; у типового дома и CO₂ источник обязателен |
| GET | `/apartments/{id}/utility` | все | расходы на энергию квартиры: кВт·ч и TJS в год и месяц, сравнение с типовым домом (если задан) и его источник; 204 — нет тарифа или расчётного потребления дома |
| GET | `/public/green-impact?organization=slug` | все, кэш 5 мин | `{ documentsDigital, pagesSaved, paperKgSaved, co2KgSaved?, co2Source?, onlineActions, isDemo }`; без organization — 400, неизвестная — 404 |
| GET | `/stats/green` | менеджер, руководители | `{ totals, onlineBookings, onlinePayments, chatMessages, months: [{ month, documents, pages, paperKg }] }` за 12 месяцев |

Коды ошибок: `invalid_energy_class`, `invalid_heating_type`, `eco_text_too_long`, `invalid_solar_power`, `invalid_green_area`, `invalid_energy_use`, `invalid_tariff`, `invalid_baseline`, `invalid_co2_factor`, `source_required`, `source_too_long`, `demo_read_only` (демо-менеджер: только просмотр и чат).

## CRM

| Метод | Путь | Роли | Примечание для фронтенда |
| --- | --- | --- | --- |
| GET | `/crm/summary` | Manager, руководители | `newLeads, leadsInProgress, activeBookings, bookingsExpiringIn24Hours, dealsByStage{}, apartmentsByStatus{}, soldAmount[{currency, amount}]`. Сумма продаж на дашборде — в сомони, USD по текущему курсу |
| POST | `/crm/clients/import?dryRun=` | Manager, руководители | multipart, поле `file` (.xlsx, до 5 МБ, до 1000 строк): столбцы Имя \| Телефон \| Email \| Комментарий, первая строка — заголовки. Ответ `{ dryRun, added, skipped, invalid, rows: [{ row, name, phone, email, comment, status: new \| duplicate \| invalid, error }] }`. `dryRun=true` — предпросмотр без записи. Повтор телефона (в организации или выше в файле) — пропуск. Комментарий в карточку не сохраняется. Не .xlsx — 422; сотрудник с несколькими (не всеми) домами — 422. Аудит `Customer` / `Imported` |
| GET | `/crm/templates` | Manager, руководители | `[{ id, name, text, type, updatedAt }]` — шаблоны SMS / WhatsApp организации; в тексте подстановки `{name}`, `{apt}`, `{amount}` |
| POST | `/crm/templates` | Manager, руководители | `{ name, text, type: payment \| booking \| welcome }` → 201. Название 1–120, текст 1–1000, другой тип — 422; название занято в организации — 409. Аудит `MessageTemplate` |
| PUT | `/crm/templates/{id}` | Manager, руководители | то же тело → 200; чужой или неизвестный — 404 |
| DELETE | `/crm/templates/{id}` | Manager, руководители | 204; неизвестный — 404 |
| GET | `/crm/analytics/sales` | Manager, руководители | `{ months: [{ month: "2026-11", deals, completed }] (12 месяцев по текущий, по Душанбе), funnel: [{ key: leads \| bookings \| deals \| completed, count, percent }] }`. Сделки — кроме отменённых, по дате создания; завершённые — по дате последнего изменения. Проценты — от самого большого шага |
| GET | `/crm/analytics/project` | руководители (Owner, ObjectAdmin, SuperAdmin) | `[{ buildingId, name, developer, constructionPercent, nextStage, completionYear, totalApartments, sold, booked, free, revenue, plan, currency: "TJS" }]` по видимым домам. Стройка — среднее по этапам; `nextStage` — первый незавершённый этап; `revenue` — поступления за вычетом сторно; `plan` — стоимость всех квартир по прайсу (USD по текущему курсу) |
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
| POST | `/deals/{id}/payment/initiate` | владелец сделки, сотрудник сделки | Онлайн-оплата картой. `{ amount?, returnUrl? }` → `{ paymentUrl, gatewayPaymentId, amount, currency, provider }`. Без `amount` — ближайший неоплаченный платёж графика; больше остатка, отменённая сделка, нет остатка — 422; нет графика или чужая сделка — 404. `returnUrl` — путь на этом сайте (иначе `/cabinet?success=1`). Аудит `OnlinePayment` / `Initiated` |
| GET | `/deals/{id}/payment/{gatewayPaymentId}/status` | владелец сделки, сотрудник сделки | `{ gatewayPaymentId, status: Pending|Paid|Failed|Refunded, amount, currency, paidAt, paymentId }`. Когда банк подтвердил оплату, один раз создаётся платёж графика `Method = Card` (`paymentId`), аудит `OnlinePayment` / `Paid`, Telegram покупателю |
| POST | `/payment/mock/simulate` | без входа, только Development | `{ gatewayPaymentId, success }` → статус: тестовый банк принял или отклонил платёж. Уже обработанный — 422, неизвестный — 404, вне Development — 404 |
| POST | `/payment/webhook/{provider}` | без входа | Заготовка для уведомлений Alif / Корти Миллӣ: 501 |
| PUT | `/rates` | Accountant, руководители | `{ date, currency: "USD", rateToTjs }` |
| GET | `/reports/payments?from=&to=` | Accountant, руководители | `{ rows[], totalsByCurrency[], totalTjs }`, Excel — `.xlsx` |
| GET | `/reports/debts?asOf=` | Accountant, руководители | `rows[{ dealId, customerName, phone, apartmentNumber, currency, overdue, oldestOverdueDate, daysOverdue, remaining }]`, Excel — `.xlsx` |
| GET | `/stats/dashboard` | Owner, ObjectAdmin, Manager, Accountant | `{ totalApartments, free, booked, sold, onRequest, totalRevenue, collectedRevenue, remaining, overduePayments, overdueDeals, newLeadsThisWeek, leadsThisMonth, newDealsThisMonth, conversionRate, usdRate }`. Деньги — `{ byCurrency: [{ currency, amount }], tjs }`: `totalRevenue` — стоимость договоров (этапы «Договор», «Оплата», «Завершена»), в сомони по текущему курсу (`tjs: null`, если есть USD, а курса нет); `collectedRevenue` — поступления по ним за вычетом сторно, в сомони по курсу на дату платежа; `remaining` — остаток. `overduePayments` — пункты графика со сроком раньше сегодня, оплаченные не полностью; `conversionRate` — сделки месяца / заявки месяца. Кэш 5 минут, сбрасывается сделками, бронями, каталогом, платежами, графиками и заявками |
| GET | `/stats/sales?from=&to=` | Owner, ObjectAdmin, Manager, Accountant | `[{ month: "ГГГГ-ММ", deals, revenue }]` по месяцам подряд (пустые — с нулями): сделки, заключённые в месяце (кроме отменённых), и поступления за месяц. По умолчанию — последние 6 месяцев, больше 24 — 422 |
| GET | `/stats/buildings` | Owner, ObjectAdmin | `[{ buildingId, name, totalApartments, free, booked, sold, onRequest, occupancyRate, revenue, collected }]`; `occupancyRate` = (бронь + продано) / всего. Админ объекта видит свой дом |
| GET | `/stats/export?from=&to=&buildingId=` | Owner, Accountant | `.xlsx`: листы «Сделки» (за период), «График платежей» (по этим сделкам), «Поступления» (за период), «Сводка» (по домам на сегодня). По умолчанию — последние 366 дней, больше — 422 |
| GET | `/buildings/{id}/cash?from=&to=` | Accountant, руководители | Кассовая книга дома: `{ buildingId, buildingName, from, to, totalIncomeTjs, totalExpenseTjs, balanceTjs, transactions[] }`. По умолчанию — текущий месяц по Душанбе, период не больше 366 дней. Операции — от новых к старым: `{ id, amount, currency, amountTjs, direction: Income\|Expense, category, description, dealId, isReversal, reversalOfId, isReversed, operationDate, createdAt, createdByName }`. Приход — сумма со знаком плюс, расход — минус; суммы в USD пересчитаны в сомони по курсу на дату операции (сохраняется в записи). Чужой дом — 404 |
| GET | `/buildings/{id}/cash/export.xlsx?from=&to=` | Accountant, руководители | Лист «Кассовая книга»: дата, направление, статья, основание, сумма, валюта, в сомони, сделка, кто внёс, сторно; приход — зелёный фон, расход — красный, сторно и сторнированные — серый; внизу итоги |
| POST | `/buildings/{id}/cash` | Accountant, руководители | `{ amount (> 0), currency, direction, category, description (до 500), operationDate (не в будущем), dealId? }` → 201 операция. Статьи прихода: `ApartmentPayment`, `Rent`, `OtherIncome`; расхода: `Construction`, `Materials`, `Salaries`, `Marketing`, `Utilities`, `Tax`, `OtherExpense` — статья не того направления, сделка не из этого дома, USD без курса на дату — 422. Аудит `CashTransaction` / `Created` |
| POST | `/buildings/{id}/cash/{txId}/reverse` | Accountant, руководители | `{ reason }` → 201: обратная запись с той же статьёй и направлением, сумма с обратным знаком, дата — сегодня, `reversalOfId` — исходная. Второе сторно и сторно сторно — 422. Аудит `CashTransaction` / `Reversed` |
| GET | `/buildings/{id}/cash/{txId}/receipt.pdf` | Accountant, руководители | Приходный или расходный кассовый ордер A5; у сторно — красная полоса «СТОРНО» |
| GET | `/buildings/{id}/reports/weekly` | Accountant, руководители | Еженедельные отчёты дома, новые сверху: `[{ id, buildingId, periodStart, periodEnd, generatedAt, isManual }]`. Чужой или несуществующий дом — 404 |
| GET | `/buildings/{id}/reports/weekly/{reportId}/excel` | Accountant, руководители | `.xlsx`: «Сводка» (касса за неделю в TJS: поступления, расходы, прибыль; новые сделки; просрочек на понедельник после недели; готовность стройки — среднее по этапам), «Платежи» (оплаты и сторно за неделю), «Касса», «Просрочки». Отчёт другого дома — 404 |
| GET | `/buildings/{id}/reports/weekly/{reportId}/pdf` | Accountant, руководители | PDF A4: «Binosoz» золотом (#c9a227), сводка карточками, топ-5 должников (по давности, затем по сумме) |
| POST | `/buildings/{id}/reports/weekly/generate` | Owner, SuperAdmin | 201 → отчёт за последнюю завершённую неделю пн–вс по Душанбе. Готовый за ту же неделю перезаписывается (один отчёт на дом и неделю). Аудит `WeeklyReport` / `Generate` |

Подробности:
- Пункт графика: `kind` (`DownPayment` или `Monthly`), `status` (`Pending`, `PartiallyPaid` или `Paid`) и признак `isOverdue`. Интерфейс показывает «Просрочен» по `isOverdue`.
- Отдельного списка платежей сделки нет: история берётся из `schedule.payments`.
- Страница тестового банка — `/payment/mock-card?id=&amount=&currency=&returnUrl=&description=` (`frontend/payment.html`). Ожидающие онлайн-оплаты в `schedule.payments` не попадают.
- PDF и Excel скачиваются с токеном. PDF открывается в новой вкладке, Excel сохраняется под именем из `Content-Disposition`.

## Уведомления (колокольчик)

| Метод | Путь | Роли | Примечание |
| --- | --- | --- | --- |
| GET | `/notifications` | любой вошедший | `{ unreadCount, items[] }` — последние 50 своих уведомлений, новые сверху: `{ id, title, text, kind, dealId, isRead, createdAt }`. `kind`: `overdue_payment` (просрочка), `payment_reminder` (за 3 дня до срока) или null (сообщения о платежах, документах, чате) |
| POST | `/notifications/{id}/read` | любой вошедший | 204; чужое уведомление — 404 |
| POST | `/notifications/read-all` | любой вошедший | 204 — все свои отмечены прочитанными |
| POST | `/profile/telegram` | любой вошедший | `{ chatId }` → 204: личный чат Telegram вручную (для сотрудников; chatId — из @userinfobot; сначала написать боту /start). `null` — отвязать, `0` — 422 |
| POST | `/profile/telegram/link` | любой вошедший | 200 `{ url, expiresAt }`: «Получать уведомления в Telegram» — `https://t.me/<Telegram__BotUsername>?start=<токен>`, одноразовый, 15 минут. Бот не настроен (`Telegram__BotToken` + `Telegram__BotUsername`) — 503 `telegram_not_configured`. Язык сообщений — `Accept-Language` |
| DELETE | `/profile/telegram` | любой вошедший | 204: чат отвязан (то же — команда `/stop` в боте). Аудит `TelegramUnlinked` |
| GET | `/profile/notifications` | любой вошедший | `{ telegram, email, events: { bookings, deal, payments, documents, chat }, telegramLinked, telegramAvailable, emailAvailable, language }` — каналы (по умолчанию оба), галочки событий, что подключено |
| PUT | `/profile/notifications` | любой вошедший | `{ telegram, email, events }` → то же; язык сообщений (ru, tg, en) — из `Accept-Language` |
| POST | `/telegram/webhook/{secret}` | Telegram | webhook бота: секрет `Telegram__WebhookSecret` в адресе **и** в заголовке `X-Telegram-Bot-Api-Secret-Token`, иначе 404. `/start <токен>` — привязать чат к покупателю (чужой, просроченный или использованный токен — ответ «Ссылка устарела»), `/stop` — отвязать. Всегда 200 |
| GET, POST | `/public/unsubscribe?t=<токен>` | без входа | ссылка «Отписаться» из письма (подпись HMAC, 90 дней): GET — страница с кнопкой (сканеры почты ничего не выключат), POST — письма-напоминания выключены (кабинет и Telegram остаются). Неверная ссылка — 404 |
| GET | `/deals/{dealId}/reminders` | DealStaff | журнал сообщений покупателю по сделке, новые сверху (200): `[{ id, event, channel, status, dueDate, createdAt, sentAt, error, createdBy, customer, apartment }]`; `channel`: `InApp`, `Telegram`, `Email`; `status`: `Pending`, `Sent`, `Failed`, `Skipped`. Чужая сделка — 404, покупатель — 403 |
| POST | `/deals/{dealId}/reminders` | DealStaff | `{ dueDate }` → 200 `{ queued }`: «Напомнить сейчас» по пункту графика — кабинет, Telegram и почта по настройкам покупателя. Не чаще раза в сутки на пункт — 429 `reminder_too_soon`; оплаченный или несуществующий пункт — 422 `reminder_item_paid`; покупатель без кабинета — 422 `reminder_no_cabinet` |

Покупателю (`BuyerNotifier`, очередь Hangfire `BuyerMessageJob` с повторами через 1, 5, 15 и 60 минут, до 5 попыток; журнал — таблица `BuyerMessages`):
- напоминания о платежах — в кабинет (колокольчик), Telegram и на почту, как выбрал покупатель: 🟡 за 3 дня, 🔔 в день срока
  («Сегодня срок платежа: 12 500 TJS по кв. №…»), 🔴 на 1-й день просрочки и ещё раз через 6 дней (7-й день), 🔔 «Напомнить сейчас»;
  одно событие по одному пункту графика и каналу — один раз (`DedupeKey`);
- только в Telegram: 🏠 бронь создана, ⏳ бронь закончится через 6 часов (задача `booking-expiry-notices` раз в 15 минут),
  🔓 бронь снята, 📋 новый этап сделки, ✅ платёж принят, 📄 новый документ (ссылка на кабинет, не на файл),
  💬 сообщение менеджера, если покупателя нет в кабинете (без текста сообщения);
- на языке покупателя (ru, tg, en), без паспортных данных; письмо — от имени застройщика, кнопка «Открыть кабинет», ссылка «Отписаться»;
- Telegram ответил 403 (бот заблокирован) или «chat not found» — чат отвязывается, сообщение `Skipped`.

Сотрудникам в Telegram (в дополнение к колокольчику, только тем, у кого привязан чат; токен бота — `Telegram__BotToken`, без него сообщения не уходят и ничего не падает):
- 💰 новый платёж — владельцу (Owner) и бухгалтерам дома;
- 🔴 просрочка — бухгалтерам дома, один раз на пункт графика (как в колокольчике);
- 🏠 новая бронь и 📝 новая заявка — владельцу и менеджерам дома;
- 📊 еженедельный отчёт готов (по расписанию) — владельцу, со ссылкой на CRM (адрес сайта — из `Email__InviteUrl`).
«Дома» — сотрудник без закреплённых домов получает по всем домам организации. Markdown; имена экранируются. Сбой сети — 2 повтора через 2 секунды.

Фоновые задачи (Hangfire, время UTC): `update-rates` — 04:00 и 06:00 (09:00 и 11:00 Душанбе), курс USD с nbt.tj (`export_xml.php?date=ГГГГ-ММ-ДД&export=xmlout`), ручной курс не перезаписывается; `overdue-payments` — 05:00 (10:00 Душанбе), просрочка: покупателю на 1-й и 7-й день, бухгалтерам дома — один раз; `payment-reminders` — 05:00, покупателю за 3 дня и в день срока; `booking-expiry-notices` — каждые 15 минут, «бронь закончится через 6 часов». Каждое напоминание — один раз на пункт графика, событие и канал.

Еженедельный отчёт (Hangfire): `weekly-owner-report` — понедельник 04:00 UTC (09:00 Душанбе), за прошлую неделю для каждого дома организации, где есть активный владелец (Owner); дом, у которого отчёт за эту неделю уже есть, пропускается. Excel и PDF хранятся в таблице `WeeklyReports` (`bytea`), диск не нужен.

## Документы, файлы, чат, стройка

| Метод | Путь | Роли | Примечание |
| --- | --- | --- | --- |
| GET | `/cabinet/overview` | Customer | Первый экран кабинета: `{ hasDeals, customerName, contacts { organizationName, phone, email, telegram, address }, deals[] }`. `hasDeals = false` — сделок нет (приветствие и контакты). `deals[]` — свои сделки, новые сверху: `{ dealId, buildingId, buildingName (по Accept-Language), apartmentNumber, floor, rooms, area, stage, finalPrice, currency, paid (за вычетом сторно), remaining, paidPercent, managerName, createdAt, myReview }`; `myReview` — свой отзыв о доме (`ReviewResponse`) или `null`. Сотрудник — 403 |
| GET | `/deals/{id}/documents` | владелец, сотрудник сделки | `{ id, type, fileName, contentType, sizeBytes, signedAt, createdAt }` |
| POST | `/deals/{id}/documents` | Manager, Accountant, руководители | form-data: `file`, `type` (Contract, Annex, Act, Invoice, Receipt, Other), `signedAt?`; PDF, PNG, JPG, DOCX до 20 МБ |
| GET | `/documents/{id}/download` | владелец, сотрудник сделки | `{ url, expiresAt }` — подписанная ссылка на 15 минут. `url` относительный (`/api/v1/files?token=…`), фронтенд добавляет адрес сервера |
| GET | `/files?token=` | все, по ссылке | сам файл; подделанная или просроченная ссылка — 404 |
| GET | `/deals/{id}/messages` | владелец, сотрудник сделки | страница `{ id, dealId, senderId, senderName, fromCustomer, text, createdAt, readAt }`, новые сверху |
| POST | `/deals/{id}/messages` | владелец, сотрудник сделки | `{ text }` |
| GET | `/deals/{id}/messages/unread` | владелец, сотрудник сделки | `{ unread, managerName, managerOnline, managerLastSeenAt }`: сколько сообщений другой стороны не прочитано — без отметки о прочтении; менеджер «в сети», если делал запросы к API за последние 5 минут (хранится в памяти процесса) |
| GET | `/chats?search=` | сотрудники сделок | `[{ dealId, customerName, customerPhone, apartmentNumber, buildingName, lastMessage, lastMessageAt, lastFromCustomer, unread }]` — сделки с перепиской, свежие сверху, до 200; `search` — имя или телефон покупателя |
| GET | `/buildings/{id}/stages` | покупатель с неотменённой сделкой, сотрудники | `{ overallPercent, stages[{ id, name, percent, sortOrder, updatedAt, photos[{ url, caption, takenAt }] }] }`. Ссылки на фото подписаны на 15 минут |
| POST | `/buildings/{id}/stages`, PUT `/stages/{id}` | Content, руководители | `{ name, percent, sortOrder }` |
| POST | `/stages/{id}/photos` | Content, руководители | form-data: `file` (PNG или JPG до 10 МБ), `caption?`, `takenAt?` |

**Хаб чата** `/hubs/chat` (SignalR):
- токен передаётся в `accessTokenFactory`;
- методы `JoinDeal(dealId)`, `LeaveDeal(dealId)`, `SendMessage(dealId, text)` и `SendTyping(dealId)`, `dealId` — строка GUID;
- `SendTyping` — «печатает...»: остальные участники чата сделки получают событие `typing` `{ dealId, fromCustomer }` (отправителю не приходит; клиент шлёт не чаще раза в 3 секунды, подсказка гаснет через 4 секунды). Чужая сделка — ошибка хаба «Сделка не найдена»;
- событие `messageReceived` с сообщением приходит и на сообщения, отправленные через REST;
- если хаб недоступен, чат опрашивает REST раз в 9 секунд.

## Администрирование

| Метод | Путь | Роли | Примечание |
| --- | --- | --- | --- |
| GET | `/users` | руководители | страница `{ id, email, phone, fullName, roles[], isActive, createdAt, organizationId, buildingIds[] }`. ObjectAdmin видит только сотрудников своего дома |
| POST | `/users` | руководители | `{ email, fullName, role, password?, phone?, buildingIds? }`. Без пароля он генерируется (8 букв и цифр) и уходит сотруднику письмом |
| PATCH | `/users/{id}/status` | руководители | `{ isActive }`. ObjectAdmin не может блокировать руководителей |
| PUT | `/users/{id}/buildings` | Owner | `{ buildingIds }`: пусто — все дома, у ObjectAdmin ровно один. Сессии сотрудника отзываются |
| POST | `/invitations` | руководители (ObjectAdmin — только в свой дом) | `{ email, firstName, lastName, role, buildingId?, buildingIds? }` → 201 `{ id, email, fullName, role, buildingIds, expiresAt, acceptedAt, createdAt }`. Учётная запись создаётся сразу; пароль из 8 букв и цифр приходит письмом «Вы приглашены в Binosoz» с логином и ссылкой на вход. Почта занята — 409, без имени или фамилии — 422 |
| GET | `/invitations` | руководители | непринятые приглашения по ссылкам из старых писем (новые принимаются сразу) |
| DELETE | `/invitations/{id}` | руководители | 204 |
| POST | `/buildings` | Owner | новый дом организации: те же поля, что в `PUT`. Этажи сам не создаёт — фронтенд создаёт их сразу после дома (`POST /floors` для 1…N) |

Кого может назначить:
- **Owner** — ObjectAdmin (ровно один дом), Manager, Accountant, Content (дома необязательны, пусто — все);
- **ObjectAdmin** — Manager, Accountant, Content, только в свой дом: если дома не переданы, подставляется его дом;
- владельцев заводит только создатель платформы.
| GET | `/audit?entity=&from=&to=` | Owner | страница `{ userEmail, organizationId, impersonatedById, entity, entityId, action, before, after, at }`. `before` и `after` — JSON-строки. Имена сущностей — имена классов: `Deal`, `Payment`, `Apartment`, `AppUser`… |
| PUT | `/buildings/{id}` | Content, руководители | полная замена: `{ nameRu, nameTg, address, floorsCount, descriptionRu, descriptionTg, modelUrl, completionYear?, plannedApartments? }` (год сдачи 2000–2100, квартир в доме 1–5000; они же приходят в ответе о доме). Поля, которых нет в форме, `api.js` берёт из текущей версии |
| PUT | `/apartments/{id}`, POST `/apartments` | Content, руководители | `{ number, rooms, area, pricePerM2, planSlot, model3dId }` (+ `floorId` при создании) |
| PATCH | `/apartments/{id}/on-request` | Content, руководители | `{ onRequest }` |
| POST | `/apartments/import?buildingId=` | Content, руководители | form-data `file` (.xlsx) |
| GET | `/apartments/import/template` | Content, руководители | файл `shablon-kvartiry.xlsx`: первый лист — заголовки столбцов A–G без строк, второй — инструкция. Заполненный шаблон принимается `/apartments/import` как есть |

## Фронтенд frontend/: вход по почте и организации (сделано)

Всё из списка ниже сделано в `frontend/`: `js/api.js`, `js/cabinet.js` (вход и приглашение), `js/platform.js` (новый раздел), `js/crm.js`, `js/app.js`; демо-бэкенд `js/mock.js` понимает новые адреса. Для сайта застройщика в `config.js` задаётся `ORG_SLUG`.

1. **Страница входа** (`/login`), две вкладки:
   - «Сотрудник»: почта и пароль (кнопка 👁 показывает пароль) → `POST /auth/login`; ответ `requiresTwoFactor` — шаг «Код из приложения» → `POST /auth/2fa/verify`. Ответ 400 (это покупатель) — переход на вкладку «Покупатель» с той же почтой. «Забыли пароль?» → `POST /auth/password/reset-request`; ссылка из письма `/login#reset=<token>` открывает форму нового пароля → `POST /auth/password/reset`.
   - «Покупатель»: почта и **6 ячеек для своего кода** (только цифры, вставка всех 6 цифр из буфера, Backspace — на ячейку назад, «Показать код») → `/auth/customer/login`. Неверный код — «Неверный код, осталось N попыток» и выделенная ссылка «Забыли код?»; `pin_locked` — через сколько минут откроется вход; `email_not_confirmed` — кнопка «Выслать код подтверждения»; `pin_not_set` — сразу «Забыли код?». «Регистрация»: имя, фамилия, почта, телефон, «Придумайте 6-значный код» и «Повторите код» (слабый код объясняется до отправки) → `/auth/customer/register` → «Подтвердите почту»: 6 ячеек (`autocomplete=one-time-code`, полный код отправляется сам), «Отправить ещё раз» через 60 с, «Изменить почту» → `/auth/customer/confirm`. «Забыли код?»: почта → `/auth/customer/pin/reset-request` → код из письма → новый код дважды → `/auth/customer/pin/reset` → кабинет. «По телефону» (SMS) — только когда `GET /auth/methods` говорит `sms: true`. Ошибки `email_not_configured` / `sms_not_configured` показываются понятным текстом.
2. **QR-код 2FA** в разделе платформы рисует `qrcode.min.js` (cdnjs, qrcodejs 1.0.0) из `qrCodeUri`.
3. **Таймер и ошибки кода.** Повторная отправка — не раньше чем через 60 секунд. На 429 показать `detail`. На 401 показать «Неверный или устаревший код» и предложить запросить новый.
4. **Выбор раздела** — по `user.role`:
   - `SuperAdmin` → раздел платформы;
   - `Owner` / `ObjectAdmin` → админка;
   - `Manager` / `Accountant` / `Content` → CRM с пунктами по должности;
   - `Customer` → кабинет.

   Роль `Admin` больше не приходит, вместо неё `Owner`.
5. **Шапка.** Показывать `organizationName`. У ObjectAdmin — название его дома (`buildingIds[0]` → `/buildings/{id}`).
6. **Плашка режима «как владелец».** Если `user.impersonation` не `null` или в ответе есть заголовок `X-Impersonated-By`, показать заметную полосу «Вы вошли как владелец организации …» с кнопкой «Выйти». Кнопка вызывает `POST /platform/impersonate/stop` и подменяет токен. Refresh-токена в этом режиме нет: на 401 вернуть человека в раздел платформы.
7. **Раздел платформы** (новый): список организаций, «Создать организацию» (`name, slug, ownerEmail, ownerFullName`), кнопка «Войти как владелец», настройка 2FA (`/auth/2fa/*`).
8. **Сотрудники.**
   - «Пригласить сотрудника» (`POST /invitations`): имя, фамилия, почта, должность и дома; пароль приходит сотруднику письмом.
   - Для ObjectAdmin — ровно один дом.
   - Список ожидающих приглашений с кнопкой «Отозвать».
   - Владельцу — «Дома сотрудника» (`PUT /users/{id}/buildings`).
9. **Страница приглашения.** Адрес `/accept-invite#token=<token>` (старые `#invite=<token>` тоже) → `GET /auth/invitations/{token}` → форма с паролем → `POST /auth/invitations/accept { token, password }` → сохранить сессию, как после входа.
10. **Публичный сайт.**
    - Добавить `ORG_SLUG` в `config.js` и передавать `?organization=` в `/buildings`.
    - slug организации — в `/rates/current?organization=` (или её `organizationId`).
    - `buildingId` (или `apartmentId`) в форме заявки.
11. **Клиенты.** В форме клиента почта теперь важна: по ней покупатель входит в кабинет. При нескольких домах у сотрудника нужен выбор дома (`buildingId`).
12. **Аудит.** Показывать `impersonatedById` («через платформу»). Раздел аудита — только Owner.

## Что было не так в frontend/js/api.js и вьюхах (исправлено)

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
