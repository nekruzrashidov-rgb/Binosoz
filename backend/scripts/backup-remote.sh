#!/usr/bin/env bash
# Бэкап внешней продакшен-базы (Render) из GitHub Actions: pg_dump → сразу шифрование age открытым ключом.
# Открытого дампа на диске нет; расшифровать может только владелец закрытого ключа (он хранится вне GitHub).
# Репозиторий публичный, артефакты Actions видят все — поэтому без шифрования бэкап не создаётся.
#
#   PROD_DATABASE_URL=postgresql://… BACKUP_AGE_RECIPIENT=age1… ./backend/scripts/backup-remote.sh pre-deploy
#
# Переменные: PROD_DATABASE_URL (секрет), BACKUP_AGE_RECIPIENT (открытый ключ age — переменная репозитория),
# OUT_DIR (по умолчанию backups). Аргумент — метка в имени файла (daily, pre-deploy…).
# Восстановление — docs/DEPLOY.md, «Бэкапы базы на Render».
set -euo pipefail

: "${PROD_DATABASE_URL:?Не задан PROD_DATABASE_URL}"
: "${BACKUP_AGE_RECIPIENT:?Не задан BACKUP_AGE_RECIPIENT (открытый ключ age)}"
case "$BACKUP_AGE_RECIPIENT" in
    age1*) ;;
    *) echo "Ошибка: BACKUP_AGE_RECIPIENT должен быть открытым ключом age (age1…)" >&2; exit 1 ;;
esac

OUT_DIR="${OUT_DIR:-backups}"
LABEL="${1:-manual}"
mkdir -p "$OUT_DIR"
out="$OUT_DIR/sitora-db-$(date -u +%Y%m%d-%H%M%S)-$LABEL.dump.age"
trap 'rm -f "$out.tmp"' EXIT

if ! command -v age >/dev/null; then
    sudo apt-get update -qq
    sudo apt-get install -y -qq age >/dev/null
fi

# pg_dump той же версии, что и сервер (PostgreSQL 18): клиент раннера может быть старше и откажется делать дамп.
# Строка подключения передаётся через окружение, а не аргументом команды. Render принимает внешние подключения по TLS.
export DB_URL="$PROD_DATABASE_URL"
docker run --rm -e DB_URL -e PGSSLMODE=require -e PGCONNECT_TIMEOUT=30 postgres:18 \
    sh -c 'exec pg_dump --format=custom --no-owner --no-privileges "$DB_URL"' \
    | age --encrypt --recipient "$BACKUP_AGE_RECIPIENT" --output "$out.tmp"

# Зашифрованный пустой поток — пара сотен байт; настоящий дамп даже пустой схемы больше.
size=$(stat -c %s "$out.tmp")
if [ "$size" -lt 4096 ]; then
    echo "Ошибка: дамп подозрительно мал ($size байт)" >&2
    exit 1
fi
mv "$out.tmp" "$out"
echo "Бэкап: $out ($size байт, зашифрован age)"
if [ -n "${GITHUB_OUTPUT:-}" ]; then
    echo "file=$out" >> "$GITHUB_OUTPUT"
fi
