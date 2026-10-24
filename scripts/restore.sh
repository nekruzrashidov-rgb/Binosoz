#!/usr/bin/env bash
# Восстановление из бэкапа scripts/backup.sh. ТЕКУЩИЕ ДАННЫЕ БУДУТ ЗАМЕНЕНЫ.
# Запуск из корня репозитория:
#   ./scripts/restore.sh backups/sitora-db-ДАТА.dump [backups/sitora-files-ДАТА.tar.gz]
set -euo pipefail

COMPOSE_FILE="${COMPOSE_FILE:-docker-compose.prod.yml}"
compose() { docker compose -f "$COMPOSE_FILE" "$@"; }

db_file="${1:-}"
files_file="${2:-}"
if [ -z "$db_file" ] || [ ! -f "$db_file" ]; then
    echo "Укажите файл дампа базы: ./scripts/restore.sh backups/sitora-db-ДАТА.dump [файлы.tar.gz]" >&2
    exit 1
fi
if [ -n "$files_file" ] && [ ! -f "$files_file" ]; then
    echo "Файл с документами не найден: $files_file" >&2
    exit 1
fi

read -r -p "База (и файлы, если указаны) будут заменены данными из бэкапа. Продолжить? Введите «да»: " answer
if [ "$answer" != "да" ]; then
    echo "Отменено"
    exit 1
fi

# API останавливаем, чтобы во время восстановления никто не писал в базу.
compose stop webapi
compose exec -T db pg_restore -U sitora -d sitora --clean --if-exists --no-owner < "$db_file"

if [ -n "$files_file" ]; then
    compose run --rm --no-deps -T --entrypoint sh webapi \
        -c 'find /app/storage -mindepth 1 -delete && tar -xzf - -C /app' < "$files_file"
fi

compose start webapi
echo "Восстановление завершено"
