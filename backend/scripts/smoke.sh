#!/usr/bin/env bash
# Smoke-проверка после деплоя: API живой, и каталог организации не пустой.
#   ./backend/scripts/smoke.sh                                  # прод на Render, организация nekruz
#   API_URL=https://api.sitora.tj ORG=nekruz ./backend/scripts/smoke.sh
# Только чтение (GET), на проде ничего не создаёт.
# EXPECTED_SHA=<коммит> — сверить версию прода (/health/live → version) с ожидаемым коммитом main.
# Код выхода: 0 — всё хорошо; 1 — API не отвечает; 2 — API живой, но в каталоге нет домов (громкое предупреждение;
# STRICT_CATALOG=0 превращает его в код 0, если пустой каталог ожидаем); 3 — прод отстаёт от main (версия не та).
set -u
API_URL="${API_URL:-https://binosoz-1.onrender.com}"
ORG="${ORG:-nekruz}"
STRICT_CATALOG="${STRICT_CATALOG:-1}"
EXPECTED_SHA="${EXPECTED_SHA:-}"

# Render будит заснувший сервис до ~50 секунд: ждём живой ответ до 3 минут.
status=000
for attempt in $(seq 1 18); do
    live=$(curl -s -w '\n%{http_code}' --max-time 20 "$API_URL/health/live" || true)
    status=$(printf '%s' "$live" | tail -n1)
    [ "$status" = "200" ] && break
    echo "health/live: $status, попытка $attempt — жду 10 с"
    sleep 10
done
if [ "$status" != "200" ]; then
    echo "ОШИБКА: $API_URL/health/live отвечает $status" >&2
    exit 1
fi
echo "OK: $API_URL/health/live = 200"

# Версия прода: короткий SHA коммита, из которого собран работающий API.
version=$(printf '%s' "$live" | sed -n 's/.*"version":"\([^"]*\)".*/\1/p')
echo "Версия на проде: ${version:-неизвестна}"
# В GitHub Actions — понятная строка в summary запуска (поведение — коды выхода — не меняется).
summary() { [ -n "${GITHUB_STEP_SUMMARY:-}" ] && echo "$1" >> "$GITHUB_STEP_SUMMARY"; return 0; }
if [ -n "$EXPECTED_SHA" ]; then
    expected=$(printf '%s' "$EXPECTED_SHA" | cut -c1-7)
    # Прод может быть и новее ожидаемого: CI деплоит каждый коммит main, в том числе только с документами — тогда
    # ожидаемый коммит (последний с кодом) — предок версии прода, и это не отставание.
    newer=0
    if [ -n "$version" ] && [ "$version" != "$expected" ] && command -v git >/dev/null \
        && git merge-base --is-ancestor "$EXPECTED_SHA" "$version" 2>/dev/null; then
        newer=1
        echo "OK: на проде $version — новее ожидаемого $expected (после него только коммиты без кода)"
    fi
    if [ "$version" != "$expected" ] && [ "$newer" = "0" ]; then
        echo "##############################################################################" >&2
        echo "Прод отстаёт от main: на проде ${version:-неизвестно}, ожидается $expected" >&2
        echo "Проверьте деплой на Render (docs/DEPLOY.md, «Render: деплой»)." >&2
        echo "##############################################################################" >&2
        summary "### ❌ На Render старая версия ${version:-неизвестна}, в main — $expected: задеплойте (docs/DEPLOY.md, «Render: деплой»)"
        exit 3
    fi
    [ "$newer" = "0" ] && echo "OK: версия совпадает с main ($expected)"
fi

body=$(curl -s --max-time 30 "$API_URL/api/v1/buildings?organization=$ORG")
count=$(printf '%s' "$body" | sed -n 's/.*"totalCount":\([0-9]*\).*/\1/p')
if [ -z "$count" ]; then
    echo "ОШИБКА: /api/v1/buildings?organization=$ORG ответил неожиданно: $body" >&2
    exit 1
fi
if [ "$count" -lt 1 ]; then
    echo "##############################################################################" >&2
    echo "ВНИМАНИЕ: в каталоге организации «$ORG» нет ни одного дома — сайт показывает пустой каталог." >&2
    echo "Заполните базу (docs/DEPLOY.md, «Демо-данные в пустой базе»): psql \"\$DATABASE_URL\" -f backend/scripts/seed.sql" >&2
    echo "##############################################################################" >&2
    summary "### ⚠️ База пустая: запустите Actions → Seed prod (docs/DEPLOY.md)"
    [ "$STRICT_CATALOG" = "0" ] && exit 0
    exit 2
fi
echo "OK: домов в каталоге «$ORG»: $count"
