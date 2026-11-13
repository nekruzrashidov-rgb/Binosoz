#!/usr/bin/env bash
# Бэкап базы (pg_dump, формат custom) и загруженных файлов (документы, фото стройки).
# Запуск из корня репозитория на сервере:
#   ./scripts/backup.sh
# Переменные: COMPOSE_FILE (по умолчанию docker-compose.prod.yml), BACKUP_DIR (./backups),
# KEEP_DAYS — сколько дней хранить бэкапы (14).
set -euo pipefail

COMPOSE_FILE="${COMPOSE_FILE:-docker-compose.prod.yml}"
BACKUP_DIR="${BACKUP_DIR:-./backups}"
KEEP_DAYS="${KEEP_DAYS:-14}"
STAMP="$(date +%Y%m%d-%H%M%S)"

compose() { docker compose -f "$COMPOSE_FILE" "$@"; }

mkdir -p "$BACKUP_DIR"
db_file="$BACKUP_DIR/sitora-db-$STAMP.dump"
files_file="$BACKUP_DIR/sitora-files-$STAMP.tar.gz"
# Пишем во временные файлы: недописанный бэкап не должен выглядеть готовым.
trap 'rm -f "$db_file.tmp" "$files_file.tmp"' EXIT

compose exec -T db pg_dump -U sitora -d sitora --format=custom --no-owner > "$db_file.tmp"
if [ ! -s "$db_file.tmp" ]; then
    echo "Ошибка: дамп базы пустой" >&2
    exit 1
fi
mv "$db_file.tmp" "$db_file"

compose exec -T webapi tar -czf - -C /app storage > "$files_file.tmp"
mv "$files_file.tmp" "$files_file"

find "$BACKUP_DIR" -maxdepth 1 -type f -name 'sitora-*' -mtime +"$KEEP_DAYS" -delete

echo "Готово:"
echo "  база:  $db_file ($(du -h "$db_file" | cut -f1))"
echo "  файлы: $files_file ($(du -h "$files_file" | cut -f1))"
