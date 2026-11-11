#!/usr/bin/env bash
# Smoke-проверка после деплоя: API живой, и каталог организации не пустой.
#   ./scripts/smoke.sh                                  # прод на Render, организация nekruz
#   API_URL=https://api.sitora.tj ORG=nekruz ./scripts/smoke.sh
# Только чтение (GET), на проде ничего не создаёт.
# Код выхода: 0 — всё хорошо; 1 — API не отвечает; 2 — API живой, но в каталоге нет домов (громкое предупреждение;
# STRICT_CATALOG=0 превращает его в код 0, если пустой каталог ожидаем).
set -u
API_URL="${API_URL:-https://binosoz-1.onrender.com}"
ORG="${ORG:-nekruz}"
STRICT_CATALOG="${STRICT_CATALOG:-1}"

# Render будит заснувший сервис до ~50 секунд: ждём живой ответ до 3 минут.
status=000
for attempt in $(seq 1 18); do
    status=$(curl -s -o /dev/null -w '%{http_code}' --max-time 20 "$API_URL/health/live" || true)
    [ "$status" = "200" ] && break
    echo "health/live: $status, попытка $attempt — жду 10 с"
    sleep 10
done
if [ "$status" != "200" ]; then
    echo "ОШИБКА: $API_URL/health/live отвечает $status" >&2
    exit 1
fi
echo "OK: $API_URL/health/live = 200"

body=$(curl -s --max-time 30 "$API_URL/api/v1/buildings?organization=$ORG")
count=$(printf '%s' "$body" | sed -n 's/.*"totalCount":\([0-9]*\).*/\1/p')
if [ -z "$count" ]; then
    echo "ОШИБКА: /api/v1/buildings?organization=$ORG ответил неожиданно: $body" >&2
    exit 1
fi
if [ "$count" -lt 1 ]; then
    echo "##############################################################################" >&2
    echo "ВНИМАНИЕ: в каталоге организации «$ORG» нет ни одного дома — сайт показывает пустой каталог." >&2
    echo "Заполните базу (docs/DEPLOY.md, «Демо-данные в пустой базе»): psql \"\$DATABASE_URL\" -f scripts/seed.sql" >&2
    echo "##############################################################################" >&2
    [ "$STRICT_CATALOG" = "0" ] && exit 0
    exit 2
fi
echo "OK: домов в каталоге «$ORG»: $count"
