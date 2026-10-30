# VPS и автоматический деплой (Ubuntu 24.04)

Короткий путь от чистого сервера до деплоя по push в `main`. Подробности каждого шага, бэкапы, восстановление и мониторинг — в [DEPLOY.md](DEPLOY.md).

Как это устроено:

1. Push в `main` → GitHub Actions (`.github/workflows/ci.yml`): сборка и тесты, Docker-образ.
2. Образ публикуется в GHCR как `ghcr.io/nekruzrashidov-rgb/binosoz-api:<sha коммита>`. Когда тесты прошли, тот же образ получает тег `latest`.
3. Задание «Деплой на VPS» заходит на сервер по SSH. Там оно обновляет compose-файлы (`git pull`), скачивает образ этого коммита, делает бэкап, применяет миграции (`migrate`) и перезапускает API.
4. На сервере работают PostgreSQL 18, Redis, API и Caddy. Caddy сам получает и продлевает сертификат Let's Encrypt.

Фронтенд по-прежнему деплоится на Vercel (`binosoz.vercel.app`) отдельно. Тот же сайт отдаёт и сам API из своего образа.

## 1. Сервер

DNS: A-запись домена API (например, `api.binosoz.ru`) указывает на IP сервера. Без этого Caddy не получит сертификат.

```bash
sudo apt update && sudo apt upgrade -y
sudo apt install -y git ufw
sudo ufw allow OpenSSH && sudo ufw allow 80/tcp && sudo ufw allow 443/tcp && sudo ufw allow 443/udp && sudo ufw enable

# Docker Engine и Compose (подробнее и через apt — DEPLOY.md, раздел 1)
curl -fsSL https://get.docker.com | sudo sh
```

## 2. Пользователь для деплоя

Отдельный пользователь без sudo, но в группе `docker`. На своём компьютере создайте ключ только для деплоя:

```bash
ssh-keygen -t ed25519 -f binosoz_deploy -C "github-actions deploy"
```

На сервере:

```bash
sudo adduser --disabled-password --gecos "" deploy
sudo usermod -aG docker deploy
sudo mkdir -p /home/deploy/.ssh
sudo nano /home/deploy/.ssh/authorized_keys     # вставьте содержимое binosoz_deploy.pub
sudo chown -R deploy:deploy /home/deploy/.ssh && sudo chmod 700 /home/deploy/.ssh && sudo chmod 600 /home/deploy/.ssh/authorized_keys
```

## 3. Код и `.env`

```bash
sudo mkdir -p /opt/sitora && sudo chown deploy:deploy /opt/sitora
sudo -u deploy git clone https://github.com/nekruzrashidov-rgb/Binosoz.git /opt/sitora
cd /opt/sitora
sudo -u deploy cp .env.example .env && sudo chmod 600 .env
sudo -u deploy nano .env
```

Имена переменных — как в `.env.example`, блок «Продакшен». Главное:

| Переменная | Значение |
| --- | --- |
| `POSTGRES_PASSWORD` | `openssl rand -base64 32` |
| `JWT_KEY` | `openssl rand -base64 48` |
| `ENCRYPTION_KEY` | `openssl rand -base64 32`; **храните копию вне сервера**: без него паспортные данные не расшифровать |
| `API_DOMAIN` | домен API без `https://`, например `api.binosoz.ru` |
| `SITE_ORIGIN` | `https://binosoz.vercel.app` (адрес фронтенда для CORS); второй — в `CRM_ORIGIN` |
| `EMAIL_PROVIDER`, `EMAIL_FROM` | `Smtp` и адрес отправителя |
| `SMTP_HOST`, `SMTP_PORT`, `SMTP_USERNAME`, `SMTP_PASSWORD` | для Gmail: `smtp.gmail.com`, `587`, почта и пароль приложения |
| `EMAIL_INVITE_URL` | `https://binosoz.vercel.app/accept-invite#token=` |
| `ADMIN_EMAIL`, `ADMIN_PASSWORD` | создатель платформы; пароль — второй фактор его входа |
| `BOOTSTRAP_ADMIN_EMAIL`, `BOOTSTRAP_ADMIN_PASSWORD` | первый владелец организации (после первого входа строки можно удалить) |

`Redis__ConnectionString` в `.env` не нужен: в `docker-compose.prod.yml` он уже указывает на контейнер `redis`.

## 4. Первый запуск

```bash
cd /opt/sitora
docker compose -f docker-compose.prod.yml pull
docker compose -f docker-compose.prod.yml run --rm migrate
docker compose -f docker-compose.prod.yml up -d
docker compose -f docker-compose.prod.yml ps        # webapi — healthy
curl -i https://api.binosoz.ru/health/ready
```

Образ появится в GHCR после первого зелёного прогона CI в `main`. Если пакет приватный (Settings пакета на GitHub → Danger Zone → Change visibility), перед `pull` войдите: `docker login ghcr.io -u <логин>` с токеном `read:packages`. Сделать пакет публичным проще, секретов в образе нет.

Сертификат Caddy получит сам при первом запросе к домену. Отдельный certbot и nginx не нужны.

## 5. Секреты в GitHub

Репозиторий → Settings → Secrets and variables → Actions:

| Секрет | Значение |
| --- | --- |
| `VPS_HOST` | IP или домен сервера |
| `VPS_USER` | `deploy` |
| `VPS_SSH_KEY` | содержимое приватного ключа `binosoz_deploy` целиком, вместе со строками `BEGIN` / `END` |
| `VPS_PORT` | необязательно, если SSH не на 22 |

Необязательная переменная (вкладка Variables): `DEPLOY_PATH`, если код лежит не в `/opt/sitora`.

Пока `VPS_HOST` не задан, задание деплоя только ставит образу тег `latest` и пропускает шаг на сервере, CI остаётся зелёным.

## 6. Проверка деплоя

1. Сделайте push в `main` и откройте Actions → CI. Задания «Сборка и тесты», «Docker-образ и compose» и «Деплой на VPS» должны стать зелёными.
2. На сервере выполните `docker compose -f docker-compose.prod.yml ps`. Образ у `webapi` должен быть `binosoz-api:<sha этого коммита>`.
3. Откат на прошлую версию — DEPLOY.md, раздел 8: `WEBAPI_IMAGE=…:<sha> docker compose -f docker-compose.prod.yml up -d`.
