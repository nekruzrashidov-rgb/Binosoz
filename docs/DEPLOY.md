# Деплой на VPS

Инструкция для одного сервера с Ubuntu 24.04 LTS. В итоге на сервере работают четыре контейнера: PostgreSQL 18, Redis, API (готовый образ из GHCR) и Caddy, который выдаёт HTTPS-сертификат Let's Encrypt. Автоматический деплой из GitHub Actions — [VPS_SETUP.md](VPS_SETUP.md).

> **Статус:** инструкция целиком не проверена на чистой машине: при разработке не было Docker (нет WSL) и VPS. Без Docker проверены compose-файлы (`docker compose config`), опубликованная сборка в режиме Production за прокси и команды `pg_dump`/`pg_restore`/`tar` из скриптов бэкапа — подробности в README, раздел «Известные ограничения». При первом прохождении отметьте, что сработало, и поправьте шаги, которые разошлись с реальностью.

## Что нужно заранее

- VPS: 2 vCPU, 4 ГБ RAM и 40 ГБ диска для начала. Ubuntu 24.04, доступ по SSH под пользователем с sudo.
- Домен для API, например `api.sitora.tj`. Его A-запись (и AAAA, если есть IPv6) должна указывать на IP сервера. Без этого Caddy не получит сертификат.
- Адреса фронтендов (сайт и CRM) для CORS.

## 1. Подготовка сервера

```bash
sudo apt update && sudo apt upgrade -y
sudo apt install -y ca-certificates curl git ufw
```

Файрвол: открыты только SSH, HTTP и HTTPS (HTTP нужен Caddy для выпуска сертификата и редиректа):

```bash
sudo ufw allow OpenSSH
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw allow 443/udp
sudo ufw enable
```

Docker Engine и Compose из официального репозитория Docker (https://docs.docker.com/engine/install/ubuntu/):

```bash
sudo install -m 0755 -d /etc/apt/keyrings
sudo curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo $VERSION_CODENAME) stable" | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null
sudo apt update
sudo apt install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
sudo usermod -aG docker $USER
```

Перезайдите по SSH, чтобы группа `docker` применилась. Проверка:

```bash
docker compose version
```

## 2. Код и настройки

```bash
sudo mkdir -p /opt/sitora && sudo chown $USER /opt/sitora
git clone https://github.com/nekruzrashidov-rgb/Binosoz.git /opt/sitora
cd /opt/sitora
cp .env.example .env
chmod 600 .env
```

Заполните в `.env` блок «Продакшен» и общие ключи:

| Переменная | Что указать |
| --- | --- |
| `POSTGRES_PASSWORD` | длинный случайный пароль: `openssl rand -base64 32` |
| `JWT_KEY` | `openssl rand -base64 48` |
| `ENCRYPTION_KEY` | `openssl rand -base64 32`. **Сохраните копию вне сервера:** без ключа паспортные данные не расшифровать |
| `API_DOMAIN` | домен API без `https://`, например `api.sitora.tj` |
| `SITE_ORIGIN`, `CRM_ORIGIN` | адреса фронтендов с `https://` |
| `BOOTSTRAP_ADMIN_EMAIL`, `BOOTSTRAP_ADMIN_PASSWORD` | первый владелец организации по умолчанию. Пароль от 8 символов, с цифрой, строчной и заглавной буквой |
| `ADMIN_EMAIL`, `ADMIN_PASSWORD` | создатель платформы (скрытая роль); пароль — второй фактор его входа |
| `EMAIL_PROVIDER`, `EMAIL_FROM`, `EMAIL_INVITE_URL` | почта для кодов входа и приглашений: `Smtp` или `Resend`, адрес отправителя, страница приглашения во фронтенде |
| `SMTP_HOST`, `SMTP_PORT`, `SMTP_USERNAME`, `SMTP_PASSWORD` или `RESEND_API_KEY` | доступ к почтовому сервису |

Строки `SEED_*` и `ASPNETCORE_ENVIRONMENT` в продакшене не используются.

## 3. Запуск

Образ API готовый: его собирает и публикует CI в GHCR (`ghcr.io/nekruzrashidov-rgb/binosoz-api`) на каждый push в `main`. Скачайте его и примените миграции отдельной командой. Сервис `migrate` применяет миграции, создаёт роли и первого администратора (из `BOOTSTRAP_ADMIN_*`), после чего завершается:

```bash
docker compose -f docker-compose.prod.yml pull
docker compose -f docker-compose.prod.yml run --rm migrate
```

Если пакет в GHCR приватный, сначала войдите: `docker login ghcr.io -u <логин GitHub>` с токеном, у которого есть право `read:packages`. Собрать образ на самом сервере, как раньше, тоже можно: `docker compose -f docker-compose.prod.yml build migrate` (он получит то же имя и заменит скачанный).

Дальнейшие обновления можно не делать руками: после настройки из [VPS_SETUP.md](VPS_SETUP.md) их выполняет CI.

Затем запустите всё остальное:

```bash
docker compose -f docker-compose.prod.yml up -d
docker compose -f docker-compose.prod.yml ps
docker compose -f docker-compose.prod.yml logs -f webapi
```

`up` тоже запускает `migrate` перед API и стартует API только после его успешного завершения. Сам API в Production миграции не применяет: если база не обновлена, он не стартует и пишет в лог команду, которую нужно выполнить.

После первого успешного входа администратора удалите строки `BOOTSTRAP_ADMIN_*` из `.env`. Повторно админ всё равно не создаётся, пока в базе есть хотя бы один.

## 4. Проверка после деплоя

```bash
curl -i https://api.sitora.tj/health/ready
curl -I http://api.sitora.tj/health
curl -i https://api.sitora.tj/api/v1/buildings
```

Ожидаемый результат:

1. Первая команда отвечает `200` и JSON с `"status":"Healthy"` у `database`, `storage` и `background-jobs`.
2. Вторая — редиректом на `https://`.
3. Третья — `200`, а в заголовках есть `Strict-Transport-Security`.
4. `https://api.sitora.tj/swagger` отвечает 404: в продакшене Swagger выключен.
5. Вход администратора через `POST /api/v1/auth/login` выдаёт JWT.
6. С адреса фронтенда (`SITE_ORIGIN`) запросы проходят CORS, с чужого адреса — нет.
7. Порт 5432 снаружи закрыт: `nc -zv <IP сервера> 5432` с другой машины не подключается.

## 5. Бэкапы

Ручной бэкап базы и загруженных файлов:

```bash
cd /opt/sitora && ./backend/scripts/backup.sh
```

Бэкапы ложатся в `/opt/sitora/backups`. Старше 14 дней удаляются, срок меняется переменной `KEEP_DAYS`.

Ежедневно в 03:00 через cron (`crontab -e`):

```
0 3 * * * cd /opt/sitora && ./backend/scripts/backup.sh >> /opt/sitora/backups/backup.log 2>&1
```

Бэкапы на том же сервере не спасут при потере сервера. Копируйте папку `backups` на другой сервер или в хранилище, например через `rsync` или `rclone`. Ключ `ENCRYPTION_KEY` и файл `.env` храните отдельно от бэкапов.

Хотя бы раз проверьте, что бэкап восстанавливается. Лучше всего — на отдельной тестовой машине.

## 6. Восстановление

```bash
cd /opt/sitora
./backend/scripts/restore.sh backups/sitora-db-ДАТА.dump backups/sitora-files-ДАТА.tar.gz
```

Скрипт спросит подтверждение, остановит API, заменит базу и файлы и снова запустит API.

## 7. Обновление

Автоматически: push в `main` → тесты → образ в GHCR → задание «Деплой на VPS» делает на сервере то же, что ниже (настройка — [VPS_SETUP.md](VPS_SETUP.md)). Вручную:

```bash
cd /opt/sitora
git pull
docker compose -f docker-compose.prod.yml pull
./backend/scripts/backup.sh
docker compose -f docker-compose.prod.yml run --rm migrate
docker compose -f docker-compose.prod.yml up -d
docker compose -f docker-compose.prod.yml logs -f webapi
```

Если `migrate` завершился с ошибкой, API продолжает работать на старой версии. Разберитесь с ошибкой по выводу команды, прежде чем запускать `up`. Бэкап перед обновлением обязателен: миграцию нельзя отменить без восстановления базы.

## 8. Откат

Каждый образ в GHCR помечен коммитом. Если новая версия не стартует, верните предыдущую:

```bash
git log --oneline -5
WEBAPI_IMAGE=ghcr.io/nekruzrashidov-rgb/binosoz-api:<полный sha предыдущего коммита> docker compose -f docker-compose.prod.yml up -d
```

Так API остаётся на старом образе до следующего деплоя. Без GHCR — `git checkout <предыдущий коммит>` и `docker compose -f docker-compose.prod.yml up -d --build`.

Если новая версия успела применить миграцию, которая несовместима со старым кодом, восстановите базу из бэкапа, сделанного перед обновлением (раздел 6).

## Мониторинг

- `GET /health/live` — процесс отвечает. По нему docker проверяет контейнер (`docker compose ps` показывает `healthy`), и Caddy ждёт его перед стартом.
- `GET /health/ready` (и `/health`) — база, запись в хранилище файлов, сервер фоновых задач и Redis (если задан `Redis__ConnectionString`). Ответ: JSON со статусом каждой проверки; при проблеме с базой или файлами — 503, при остановленных фоновых задачах или недоступном Redis — `Degraded` с кодом 200.
- Раз в 5 минут API сам проверяет `ready` и при смене состояния («не работает» / «снова работает») шлёт уведомление в канал менеджеров через `INotificationSender`. Пока Telegram — заглушка, сообщения видны только в логе и в таблице `Notifications`.
- Если API упал целиком, он сам ни о чём не сообщит. Поэтому нужна внешняя проверка `/health/live` раз в 1–5 минут — сервисом мониторинга доступности (UptimeRobot, Better Stack и т. п., с оповещением в Telegram) или cron-задачей на другом сервере.
- `/health/ready` и `/health` подробно отвечают только из внутренней сети (localhost, docker, частные адреса) или с заголовком `X-Health-Key: <Health__Key>`. Снаружи без ключа — 404. `/health/live` открыт всегда.
- Workflow **Smoke** (`.github/workflows/smoke.yml`) раз в час и вручную (Actions → Smoke → Run workflow) проверяет `/health/live` и что в каталоге организации есть дом (`backend/scripts/smoke.sh`, только GET). Адрес и организация — переменные репозитория `SMOKE_API_URL`, `SMOKE_ORG`. Падение — письмо от GitHub.
- Ошибки уровня Error уходят в Sentry/GlitchTip, если задан `Sentry__Dsn` (`SENTRY_DSN` в `.env`): без почт, телефонов, токенов и cookie. В каждом ответе-ошибке есть `traceId` — по нему запись ищется в логе и в Sentry.
- Почта: `Email__Provider` должен быть `Smtp` или `Resend` — с `Dev` вне Development письма с кодами никуда не уходят и сотрудники не войдут.

## Полезное

- Логи API — JSON. Они идут в stdout (`docker compose -f docker-compose.prod.yml logs --since 1h webapi`) и в файлы `/app/logs/sitora-ГГГГММДД.log` на томе `logs`: новый файл каждый день или при 100 МБ, хранятся 14 файлов. Посмотреть: `docker compose -f docker-compose.prod.yml exec webapi ls /app/logs`. В каждой записи о запросе есть `ClientIp` и `UserId`; запросы к `/health` в обычный лог не пишутся. Ротацию логов stdout настраивают в `/etc/docker/daemon.json` (`"log-opts": {"max-size": "50m", "max-file": "5"}`).
- Подключиться к базе: `docker compose -f docker-compose.prod.yml exec db psql -U sitora -d sitora`.
- Статус фоновых задач: в логах API (снятие броней каждые 5 минут, курс — раз в день в 10:00 по Душанбе).

## Файлы на Render

Диск Render временный: после каждого деплоя загруженные договоры и фото пропадают. API при старте в Production с `Storage__Provider=Local` без `Storage__RootPath` пишет в лог ошибку уровня Critical, а `/health/ready` отвечает `Degraded`. Варианты:

1. **S3-совместимое хранилище** (Cloudflare R2, Backblaze B2, AWS S3, свой MinIO). Создать бакет и ключ доступа только к нему, затем в панели Render: `Storage__Provider=S3`, `Storage__S3__Endpoint` (для R2 — `https://<account>.r2.cloudflarestorage.com`, для AWS — пусто), `Storage__S3__Bucket`, `Storage__S3__AccessKey`, `Storage__S3__SecretKey`, `Storage__S3__Region` (R2 — `auto`).
2. **Render Disk** (платный тариф): подключить диск, например, в `/var/data` и задать `Storage__RootPath=/var/data/storage`.

После смены — `/health/ready` (с `X-Health-Key`) показывает `Healthy` у проверки `storage`: она пишет, читает и удаляет пробный файл.

## Демо-данные в пустой базе (Render)

Сидер каталога работает только в Development, поэтому продакшен-база на Render после миграций пустая.
Заполнить её домом «Некруз Резиденс» (20 этажей + коммерческий, 160 квартир: 6 продано, 5 в брони, остальные свободны):

```bash
psql "postgresql://USER:PASSWORD@HOST/DB?sslmode=require" -f backend/scripts/seed.sql
```

Строка подключения — External Database URL в панели Render (её не коммитить). Скрипт повторно ничего не добавляет:
если дом уже есть, он только пишет сообщение. Брони — настоящие (с демо-заявками, на 30 дней), сам запуск — в AuditLog.
**Без psql на своём компьютере — через GitHub Actions** (`.github/workflows/seed-prod.yml`):

1. Settings → Secrets and variables → Actions → New repository secret: имя `PROD_DATABASE_URL`, значение — External Database URL из Render.
2. Actions → **Seed prod** → Run workflow, в поле `confirm` ввести `nekruz` (защита от случайного запуска).
3. Workflow ставит `postgresql-client`, выполняет `backend/scripts/seed.sql` (с `ON_ERROR_STOP`, по TLS) и затем `backend/scripts/smoke.sh`.
   Строка подключения в лог не выводится. Без секрета или с неверным подтверждением workflow сразу останавливается, база не тронута.

## Render: деплой

Как понять, что прод отстаёт: `GET https://binosoz-1.onrender.com/health/live` отвечает
`{"status":"Healthy","version":"<короткий SHA>","builtAt":"…"}` — `version` должен совпадать с последним коммитом `main`
(`git log -1 --format=%h`). Старая сборка без этого поля — значит, Render не выкатывал новые коммиты. Smoke раз в час
сверяет версию сам и падает с кодом 3 («Прод отстаёт от main»), если последнему коммиту в `backend/` или `frontend/`
больше 30 минут, а на проде другая версия. Версию сайта видно мелко в футере (`v <SHA>`) и в консоли браузера —
так можно сравнить, что отдают Render и Vercel.

Шаги для владельца (названия пунктов в панели Render могут отличаться — см. [документацию Render](https://render.com/docs/deploys)):

1. **Проверить, почему не было деплоя.** Панель Render → сервис `binosoz-1` → список событий (Events) и логи сборки.
   Частые причины: выключен автодеплой из `main`, сборка .NET упала по памяти на бесплатном тарифе, сервис привязан
   к другой ветке. Включить автодеплой или вручную запустить деплой последнего коммита.
2. **Надёжнее — готовый образ вместо сборки на Render.** CI на каждый push в `main` собирает Docker-образ, проверяет тесты
   и публикует его в GHCR: `ghcr.io/nekruzrashidov-rgb/binosoz-api:<SHA коммита>` (и `:latest`). Пакет публичный —
   Render скачивает его без логина. Если пакет станет приватным: в Render добавить учётные данные реестра
   `ghcr.io` — логин GitHub и Personal Access Token с правом `read:packages`.
   В Render создать сервис из существующего образа ([Deploy a prebuilt image](https://render.com/docs/deploying-an-image))
   с теми же переменными окружения, что у текущего, или переключить текущий сервис на образ, если панель это позволяет.
3. **Deploy Hook.** В настройках сервиса Render скопировать Deploy Hook URL ([Deploy Hooks](https://render.com/docs/deploy-hooks))
   и положить его в GitHub: Settings → Secrets and variables → Actions → секрет `RENDER_DEPLOY_HOOK_URL`.
   Для сервиса из образа ещё переменная репозитория (вкладка Variables) `RENDER_DEPLOY_MODE=image`: тогда CI передаёт
   в hook образ именно этого коммита (`?imgURL=…`). Без переменной hook просто запускает сборку последнего коммита.
   Hook вызывается в job «Деплой» только после зелёных тестов. Нет секрета — шаг пишет сообщение и CI остаётся зелёным.
4. После деплоя открыть `/health/live` и сверить `version` с последним коммитом `main`.

Сборка на Render из репозитория тоже показывает версию: Render передаёт `RENDER_GIT_COMMIT`, и API берёт его, если
образ собран без `GIT_SHA`.

## Render: холодный старт

Бесплатный Render усыпляет сервис после 15 минут без запросов; первый запрос после сна ждёт до минуты.

- Workflow **Keepalive** (`.github/workflows/keepalive.yml`) каждые 10 минут с 03:00 до 18:00 UTC (08:00–23:00 по Душанбе)
  запрашивает `/health/live`, и днём сервис не засыпает. GitHub может задерживать запуски по расписанию — это не гарантия.
  Ночью запросов нет, чтобы не тратить лимит часов бесплатного тарифа.
- Сайт: если первый запрос к API идёт дольше 3 секунд, сверху появляется плашка «Сервер просыпается, это займёт до
  минуты…»; GET-запросы, которые упёрлись в спящий шлюз (нет соединения, 502/504), повторяются каждые 5 секунд до
  90 секунд. После первого ответа плашка исчезает.
- Убрать задержку полностью можно только платным тарифом Render (сервис не засыпает) или своим сервером (VPS, разделы 1–8).

## Перед показом жюри

Чек-лист владельца: всё делается в панелях GitHub и Render, агент этого не делает. Значения секретов никуда не
копировать — ни в репозиторий, ни в чат.

1. **Каталог.** GitHub → Settings → Secrets and variables → Actions → секрет `PROD_DATABASE_URL` (External Database URL
   из Render). Затем Actions → **Seed prod** → Run workflow, в поле `confirm` — `nekruz`. После этого на сайте появятся
   дом, 3D-модель, квартиры и цены (до этого каталог показывает карточку «Продажи скоро откроются»).
2. **Ключ проверок.** Render → Environment → `Health__Key` = случайная строка (например, `openssl rand -hex 24`).
   Без него `/health/ready` снаружи отвечает 404, а при старте в логе — предупреждение.
3. **Файлы (по желанию).** Cloudflare R2 (бесплатный тариф) → бакет и ключ доступа → в Render `Storage__Provider=S3` и
   `Storage__S3__Endpoint`, `Storage__S3__Bucket`, `Storage__S3__AccessKey`, `Storage__S3__SecretKey`,
   `Storage__S3__Region=auto`. Без этого загруженные файлы пропадают при перезапуске (`/health/ready` → storage Degraded).
4. **Почта.** Выпустить **новый** ключ Resend (старый был виден на скриншоте — отозвать его в панели Resend) и задать в
   Render `Email__Provider=Resend`, `Email__Resend__ApiKey`. Без почты покупатели не получат коды входа: сайт честно
   скажет, что почта не настроена (503 `email_not_configured`).
5. **Проверка.** `https://binosoz-1.onrender.com/health/live` → `version` совпадает с последним коммитом `main`;
   на https://binosoz.vercel.app — каталог с квартирами, вход покупателя по коду из почты, CRM для сотрудника.
   За 10–15 минут до показа открыть сайт, чтобы Render проснулся (днём его держит Keepalive).

## Почта через Gmail API

Зачем: SMS-провайдера нет; Resend без своего домена доставляет письма только на адрес владельца аккаунта; бесплатный
Render закрывает исходящий SMTP (порты 587/465), поэтому `Smtp` с Gmail там не работает. Gmail API идёт по HTTPS (443)
и отправляет письма с binosoz27@gmail.com на любой адрес — покупателям и жюри. Отправитель — `GmailEmailSender`
(`Sitora.Infrastructure/Integrations/GmailEmailSender.cs`): refresh token → access token (кэш в памяти) → письмо в MIME.

Шаги владельца (названия пунктов в консоли Google могут немного отличаться — см.
[Gmail API](https://developers.google.com/workspace/gmail/api/guides) и [OAuth для desktop-приложений](https://developers.google.com/identity/protocols/oauth2/native-app)):

1. https://console.cloud.google.com → новый проект «Binosoz» → APIs & Services → Library → **Gmail API** → Enable.
2. **OAuth consent screen**: тип External, имя приложения «Binosoz», почта поддержки binosoz27@gmail.com, scope
   `https://www.googleapis.com/auth/gmail.send`, в Test users — binosoz27@gmail.com. Затем **Publish app**: в режиме
   Testing refresh token живёт только 7 дней. Проверку Google проходить не нужно — входит только владелец, он один раз
   увидит «приложение не проверено» → «Дополнительно» → «Перейти».
3. **Credentials** → Create credentials → OAuth client ID → тип **Desktop app** → скопировать Client ID и Client Secret.
4. На своём ПК (Node 18+): `node backend/scripts/gmail-token.mjs` → ввести Client ID и Secret (секрет не отображается) →
   открыть ссылку, войти как binosoz27@gmail.com → скрипт напечатает refresh token. Он ничего не сохраняет в файлы.
5. Render → Environment:

   | Переменная | Значение |
   | --- | --- |
   | `Email__Provider` | `Gmail` |
   | `Email__From` | `binosoz27@gmail.com` |
   | `Email__FromName` | `Некруз Резиденс` |
   | `Email__Gmail__ClientId` | Client ID из шага 3 |
   | `Email__Gmail__ClientSecret` | Client Secret из шага 3 |
   | `Email__Gmail__RefreshToken` | refresh token из шага 4 |

   Удалить `Smtp__*`, если они есть: короткие SMTP-переменные включают SMTP принудительно. Переменные Resend можно
   оставить — работает тот провайдер, что указан в `Email__Provider`. Без любой из трёх `Email__Gmail__*` почта считается
   ненастроенной: `/auth/methods` → `email: false`, `/auth/email/send` → 503 `email_not_configured`.
6. Проверка: `/health/ready` (с `X-Health-Key`) → проверка `email`: «Почта: Gmail настроен». Затем `/login` → «Покупатель» →
   почта **другого** адреса (не binosoz27) → код приходит → вход работает; то же в TJ.

Ограничения:
- у обычного Gmail суточный лимит отправки — несколько сотен писем; для конкурса достаточно;
- письма с нового адреса могут попасть в «Спам» — проверить самому и попросить жюри заглянуть в «Спам»;
- после смены пароля Google или отзыва доступа нужен новый refresh token (в логе API: «Refresh token Gmail отозван или
  истёк — получите новый (backend/scripts/gmail-token.mjs)»).
