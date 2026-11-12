#!/bin/sh
# Сборка на Vercel: в version.txt — короткий SHA коммита (футер и консоль сайта показывают, какая версия отдаётся).
# Без VERCEL_GIT_COMMIT_SHA (деплой через CLI) остаётся «dev». Больше ничего не собирается.
sha=$(printf '%s' "${VERCEL_GIT_COMMIT_SHA:-}" | cut -c1-7)
[ -n "$sha" ] && printf '%s\n' "$sha" > version.txt
exit 0
