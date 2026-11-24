# Вход под каждой ролью (прод и локальный стенд)

Как владельцу получить логин и пароль для каждой роли: Owner, ObjectAdmin, Manager, Accountant, Content, Customer,
SuperAdmin. **Паролей в этом файле нет и не будет.** Пароли прода вы придумываете и вводите сами в GitHub Secrets —
их знаете только вы. В лог CI, в git и в чат они не попадают.

## Таблица ролей (прод: https://binosoz.vercel.app)

Почты — примеры на одном ящике Gmail через «+»: все письма придут в `binosoz27@gmail.com`, а для системы это разные
пользователи. Можно взять любые свои почты, но **одна почта — одна роль**.

| Роль | Почта (пример) | Секрет почты | Секрет пароля | Где входить | Что увидит |
| --- | --- | --- | --- | --- | --- |
| Owner (владелец) | `binosoz27+owner@gmail.com` | `OWNER_EMAIL` | `OWNER_PASSWORD` | `/login` → «Сотрудник» | CRM целиком: все дома, лиды, сделки, платежи, пользователи, журнал (аудит), настройки |
| ObjectAdmin (руководитель дома) | `binosoz27+objectadmin@gmail.com` | `SEED_STAFF_OBJECTADMIN_EMAIL` | `SEED_STAFF_OBJECTADMIN_PASSWORD` | `/login` → «Сотрудник» | CRM только дома «Некруз Резиденс»: его сделки, клиенты, сотрудники; журнала организации нет |
| Manager (менеджер) | `binosoz27+manager@gmail.com` | `SEED_STAFF_MANAGER_EMAIL` | `SEED_STAFF_MANAGER_PASSWORD` | `/login` → «Сотрудник» | Лиды, брони, сделки, клиенты, документы, чат; без пользователей и журнала |
| Manager «только чтение» (для жюри) | `demo-manager@binosoz.tj` (постоянная) | — (`SEED_DEMO_ACCOUNTS` = `true`) | `SEED_DEMO_MANAGER_PASSWORD` | `/login` → «Сотрудник» | То же, что менеджер, но изменить ничего нельзя — только смотреть и писать в чат |
| Accountant (бухгалтер) | `binosoz27+buh@gmail.com` | `SEED_STAFF_ACCOUNTANT_EMAIL` | `SEED_STAFF_ACCOUNTANT_PASSWORD` | `/login` → «Сотрудник» | Платежи, сторно, акты сверки, курсы, отчёты; без лидов и каталога |
| Content (контент-менеджер) | `binosoz27+content@gmail.com` | `SEED_STAFF_CONTENT_EMAIL` | `SEED_STAFF_CONTENT_PASSWORD` | `/login` → «Сотрудник» | Каталог (дома, этажи, квартиры), ход стройки, фото; без платежей и лидов |
| Customer (покупатель, демо) | `demo-buyer@binosoz.tj` (постоянная) | — (`SEED_DEMO_ACCOUNTS` = `true`) | `SEED_DEMO_BUYER_PASSWORD` | `/login` → «Войти как демо-покупатель» (нужно `Auth__DemoPasswordLogin` = `true` на Render) | Кабинет: одна сделка, график платежей, два платежа, документы (акт сверки в PDF), чат с менеджером |
| Customer (покупатель, свой) | своя почта, например `binosoz27+buyer@gmail.com` | — | — (6-значный PIN задаёте сами) | `/register` → код из письма → дальше `/login` → «Покупатель» (почта + PIN) | Пустой кабинет: сделок нет, пока менеджер не заведёт сделку на эту почту |
| SuperAdmin (создатель платформы) | `binosoz27+super@gmail.com` | `SUPERADMIN_EMAIL` | `SUPERADMIN_PASSWORD` | `/login` → «Сотрудник» (откроется `/platform`) | Платформа: все организации, тарифы, заявки, пользователи всех организаций, «войти как владелец» |

Как ведут себя секреты при повторном запуске «Seed prod»:

- **ObjectAdmin, Manager, Accountant, Content и демо-аккаунты** — нужна пара (почта + пароль; у демо — `SEED_DEMO_ACCOUNTS`
  и пароль). Нет пары — роль не создаётся. Есть — пользователь создаётся один раз, а при каждом следующем запуске его
  пароль **приводится к значению из секрета**: все его сессии на всех устройствах закрываются, в журнал пишется
  «пароль задан из секрета сидера». Значит, пароль, сменённый в интерфейсе, следующий запуск вернёт к секрету.
- **Owner** создаётся, только пока у организации **нет ни одного владельца**. Если владелец с той же почтой уже есть, его пароль приводится к `OWNER_PASSWORD` (как у сотрудников); смена почты в секрете
  `BOOTSTRAP_ADMIN_EMAIL` / `BOOTSTRAP_ADMIN_PASSWORD` ничего не меняет — пароль меняйте в интерфейсе («Забыли пароль?»).
- **SuperAdmin** создаётся один раз; при следующих запусках его пароль приводится к `SUPERADMIN_PASSWORD`.
- Почта из секрета уже занята пользователем **другой роли** — шаг «Сотрудники по ролям» падает с понятной ошибкой
  («почта … уже занята другим пользователем»), остальные роли всё равно создаются. Выберите другую почту.
- Пароль не проходит правила (от 8 символов, цифра, строчная и заглавная буквы) — ошибка без значения пароля, старый
  пароль остаётся.

## Шаги на проде

1. **Придумайте пароли** — по одному на роль, 16+ символов, разные. Храните в менеджере паролей (Bitwarden, 1Password,
   «Пароли» Google). Никуда больше не пишите.
2. **GitHub → репозиторий → Settings → Secrets and variables → Actions → New repository secret.** Добавьте по одному:
   - `SEED_STAFF_OBJECTADMIN_EMAIL`, `SEED_STAFF_OBJECTADMIN_PASSWORD`
   - `SEED_STAFF_MANAGER_EMAIL`, `SEED_STAFF_MANAGER_PASSWORD`
   - `SEED_STAFF_ACCOUNTANT_EMAIL`, `SEED_STAFF_ACCOUNTANT_PASSWORD`
   - `SEED_STAFF_CONTENT_EMAIL`, `SEED_STAFF_CONTENT_PASSWORD`
   - для демо: `SEED_DEMO_ACCOUNTS` = `true`, `SEED_DEMO_BUYER_PASSWORD`, `SEED_DEMO_MANAGER_PASSWORD`
   - если их ещё нет: `OWNER_EMAIL`, `OWNER_PASSWORD`, `SUPERADMIN_EMAIL`, `SUPERADMIN_PASSWORD` (старые имена `BOOTSTRAP_ADMIN_*` и `ADMIN_*` тоже работают)
   - `PROD_DATABASE_URL` уже должен быть (External Database URL из Render, см. `docs/DEPLOY.md`).
3. **Render → binosoz-1 → Environment** (только для демо-покупателя): `Auth__DemoPasswordLogin` = `true`.
4. **GitHub → Actions → «Seed prod» → Run workflow**, в поле `confirm` — `nekruz`. Шаг «Сотрудники по ролям» напишет по
   строке на роль: «создан», «пароль обновлён», «уже есть, пароль совпадает» или «пропущен (нет секрета)» — с почтой,
   без пароля. Те же строки — в Summary запуска.
5. **Войдите на https://binosoz.vercel.app/login** под каждой ролью: «Сотрудник» — почта и пароль; «Покупатель» — почта
   и PIN; демо-покупатель — кнопка «Войти как демо-покупатель».

## 2FA (вход с кодом из приложения)

- **SuperAdmin:** войти → `/platform` → блок «Приложение-аутентификатор» → отсканировать QR-код в Google Authenticator
  или Microsoft Authenticator → ввести 6 цифр → «Включить». Дальше вход по паролю спрашивает и код из приложения.
  Отключить — там же, кодом из приложения.
- **Owner и другие сотрудники:** сервер 2FA поддерживает (`/auth/2fa/setup`, `/auth/2fa/enable`), но кнопки в CRM пока
  нет — это следующий шаг. До тех пор защита владельца — длинный уникальный пароль.
- Пароль, заданный из секрета сидера, 2FA не выключает: сначала пароль, потом код.

## Как сменить пароль

- **Роли из `SEED_STAFF_*` и демо:** поменяйте значение секрета в GitHub → снова «Seed prod» (confirm `nekruz`). Старый
  пароль перестаёт работать, все сессии этого пользователя закрываются. Забыли пароль — делаете то же самое.
- **Owner и SuperAdmin:** `/login` → «Сотрудник» → «Забыли пароль?» → ссылка на почту (24 часа, один раз). Нужен
  почтовый провайдер на Render (`Email__Provider`, см. `docs/DEPLOY.md`). Все сессии закрываются.
- **Покупатель с PIN:** `/login` → «Покупатель» → «Забыли код?» → код на почту → новый PIN.

## После конкурса

1. GitHub → Secrets: удалить `SEED_DEMO_BUYER_PASSWORD`, `SEED_DEMO_MANAGER_PASSWORD` и все `SEED_STAFF_*`, которые больше
   не нужны; `SEED_DEMO_ACCOUNTS` = `false` (или удалить).
2. Render → Environment: `Auth__DemoPasswordLogin` = `false`, `Payments__MockEnabled` = `false`.
3. Тем, кто остаётся: сменить пароль через «Забыли пароль?» (удалённый секрет больше не вернёт старый пароль — сидер без
   пары ничего не трогает). Лишних — заблокировать в CRM: «Пользователи» → «Заблокировать» (сессии закрываются сразу).
4. Owner и SuperAdmin: сменить пароли, включить 2FA у SuperAdmin.

## Локальный стенд (http://localhost)

Те же имена переменных — в `dotnet user-secrets` (проект `backend/src/Sitora.WebApi`), значения — только у вас:

```powershell
dotnet user-secrets set "SEED_STAFF_ACCOUNTANT_EMAIL" "buh@binosoz.local" --project backend/src/Sitora.WebApi
```

В Development есть ещё пользователи разработки `admin@sitora.local` (Owner), `manager@sitora.local`,
`accountant@sitora.local`, `content@sitora.local`, `superadmin@binosoz.tj` — пароли из `SEED_ADMIN_PASSWORD`,
`SEED_MANAGER_PASSWORD`, `SEED_ACCOUNTANT_PASSWORD`, `SEED_CONTENT_PASSWORD`, `SEED_SUPERADMIN_PASSWORD`; при смене
секрета пароль обновляется при следующем старте API. Таблица «роль — почта — пароль» для своего компьютера — в
`.local/test-accounts.md` (папка `.local/` в `.gitignore`, в git не попадает).
