# Деплой на VPS

Инструкция для одного сервера с Ubuntu 24.04 LTS. В итоге на сервере работают три контейнера: PostgreSQL 18, API и Caddy, который выдаёт HTTPS-сертификат Let's Encrypt.

> **Статус:** инструкция пока не проверена на чистой машине. Её писали без доступа к Docker и VPS. При первом прохождении отметьте, что сработало, и поправьте шаги, которые разошлись с реальностью.

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
git clone <адрес репозитория> /opt/sitora
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
| `BOOTSTRAP_ADMIN_EMAIL`, `BOOTSTRAP_ADMIN_PASSWORD` | первый администратор. Пароль от 8 символов, с цифрой, строчной и заглавной буквой |

Строки `SEED_*` и `ASPNETCORE_ENVIRONMENT` в продакшене не используются.

## 3. Запуск

Сначала соберите образ и примените миграции отдельной командой. Сервис `migrate` применяет миграции, создаёт роли и первого администратора (из `BOOTSTRAP_ADMIN_*`), после чего завершается:

```bash
docker compose -f docker-compose.prod.yml build migrate
docker compose -f docker-compose.prod.yml run --rm migrate
```

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
curl -i https://api.sitora.tj/health
curl -I http://api.sitora.tj/health
curl -i https://api.sitora.tj/api/v1/buildings
```

Ожидаемый результат:

1. Первая команда отвечает `200` и `Healthy`.
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

```bash
cd /opt/sitora
./scripts/backup.sh
git pull
docker compose -f docker-compose.prod.yml build migrate
docker compose -f docker-compose.prod.yml run --rm migrate
docker compose -f docker-compose.prod.yml up -d
docker compose -f docker-compose.prod.yml logs -f webapi
```

Если `migrate` завершился с ошибкой, API продолжает работать на старой версии. Разберитесь с ошибкой по выводу команды, прежде чем запускать `up`. Бэкап перед обновлением обязателен: миграцию нельзя отменить без восстановления базы.

## 8. Откат

Если новая версия не стартует:

```bash
git log --oneline -5
git checkout <предыдущий коммит>
docker compose -f docker-compose.prod.yml up -d --build
```

Если новая версия успела применить миграцию, которая несовместима со старым кодом, восстановите базу из бэкапа, сделанного перед обновлением (раздел 6).

## Полезное

- Логи API — JSON в stdout: `docker compose -f docker-compose.prod.yml logs --since 1h webapi`. Ротацию логов docker настраивают в `/etc/docker/daemon.json` (`"log-opts": {"max-size": "50m", "max-file": "5"}`).
- Подключиться к базе: `docker compose -f docker-compose.prod.yml exec db psql -U sitora -d sitora`.
- Статус фоновых задач: в логах API (снятие броней каждые 5 минут, курс — раз в день в 10:00 по Душанбе).
