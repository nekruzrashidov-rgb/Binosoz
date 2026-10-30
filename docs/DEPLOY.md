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
cd /opt/sitora && ./scripts/backup.sh
```

Бэкапы ложатся в `/opt/sitora/backups`. Старше 14 дней удаляются, срок меняется переменной `KEEP_DAYS`.

Ежедневно в 03:00 через cron (`crontab -e`):

```
0 3 * * * cd /opt/sitora && ./scripts/backup.sh >> /opt/sitora/backups/backup.log 2>&1
```

Бэкапы на том же сервере не спасут при потере сервера. Копируйте папку `backups` на другой сервер или в хранилище, например через `rsync` или `rclone`. Ключ `ENCRYPTION_KEY` и файл `.env` храните отдельно от бэкапов.

Хотя бы раз проверьте, что бэкап восстанавливается. Лучше всего — на отдельной тестовой машине.

## 6. Восстановление

```bash
cd /opt/sitora
./scripts/restore.sh backups/sitora-db-ДАТА.dump backups/sitora-files-ДАТА.tar.gz
```

Скрипт спросит подтверждение, остановит API, заменит базу и файлы и снова запустит API.

## 7. Обновление

Автоматически: push в `main` → тесты → образ в GHCR → задание «Деплой на VPS» делает на сервере то же, что ниже (настройка — [VPS_SETUP.md](VPS_SETUP.md)). Вручную:

```bash
cd /opt/sitora
git pull
docker compose -f docker-compose.prod.yml pull
./scripts/backup.sh
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
- Если API упал целиком, он сам ни о чём не сообщит. Поэтому нужна внешняя проверка `https://api.sitora.tj/health/ready` раз в 1–5 минут — сервисом мониторинга доступности или cron-задачей на другом сервере.

## Полезное

- Логи API — JSON. Они идут в stdout (`docker compose -f docker-compose.prod.yml logs --since 1h webapi`) и в файлы `/app/logs/sitora-ГГГГММДД.log` на томе `logs`: новый файл каждый день или при 100 МБ, хранятся 14 файлов. Посмотреть: `docker compose -f docker-compose.prod.yml exec webapi ls /app/logs`. В каждой записи о запросе есть `ClientIp` и `UserId`; запросы к `/health` в обычный лог не пишутся. Ротацию логов stdout настраивают в `/etc/docker/daemon.json` (`"log-opts": {"max-size": "50m", "max-file": "5"}`).
- Подключиться к базе: `docker compose -f docker-compose.prod.yml exec db psql -U sitora -d sitora`.
- Статус фоновых задач: в логах API (снятие броней каждые 5 минут, курс — раз в день в 10:00 по Душанбе).
