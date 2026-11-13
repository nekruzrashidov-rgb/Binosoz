## Что изменилось

Фронтенд разделён на пять страниц вместо одной SPA. API отдаёт их сам с того же адреса, что и `/api/v1`.

| Адрес | Файл | Скрипты |
|---|---|---|
| `/` | `index.html` | Three.js, config, i18n, ui, api, house3d, site, page |
| `/login` | `login.html` | config, i18n, ui, api, cabinet, page |
| `/cabinet` | `cabinet.html` | SignalR, config, i18n, ui, api, panels, cabinet, page |
| `/crm` | `crm.html` | SignalR, config, i18n, ui, api, panels, cabinet, crm, page |
| `/platform` | `platform.html` | config, i18n, ui, api, panels, cabinet, platform, page |

- **Существующие JS-файлы не менялись.** Роутинг, шапку, тему, язык и проверку входа из `app.js` на новых страницах заменяет новый файл `js/page.js`. Старые вызовы `S.go('crm')` ведут на `/crm`.
- **Прежняя SPA сохранена** как `frontend/spa.html`. Её можно удалить, когда новые страницы проверят.
- **Старые ссылки работают:** `/#crm` и `/#invite=…` (из писем-приглашений) перенаправляются на новые адреса.
- **Program.cs:** API берёт сайт из `wwwroot` в Docker и из `frontend/` при `dotnet run` и в тестах. Каждая страница отдаётся по своему адресу, неизвестные адреса API по-прежнему отвечают 404 (Problem Details), а не `index.html`.
- **CSP:** у страниц своя политика (свои скрипты и cdnjs, без встроенных скриптов). У `/api`, `/hubs` и `/health` осталась строгая `default-src 'none'`.
- **Docker:** контекст сборки теперь корень репозитория, `frontend/` копируется в `wwwroot`. Обновлены Dockerfile, оба compose-файла и CI. `.dockerignore` перенесён в корень, `.env` по-прежнему исключён.

- **Превью на Vercel:** `frontend/vercel.json` ведёт `/login`, `/cabinet`, `/crm`, `/platform` на свои `.html`. Превью — https://binosoz-preview.vercel.app (данные с локального API через временный туннель; подробности в `docs/RESULT.md`, раздел «Preview 🌐»).
- **`docs/RESULT.md`:** раздел о текущем этапе (сделано / в работе / дальше) и о превью.

## Зачем

Это следующий шаг из плана в CLAUDE.md: мультистраничный фронтенд, который отдаёт backend, без Vercel. Каждая страница грузит только нужные ей скрипты.

## Для ревьюера

- **Отличия от исходного ТЗ:** наборы скриптов шире, потому что `crm.js` и `platform.js` используют `S.homeFor` из `cabinet.js` и `S.workspace` из `panels.js`, а чату нужен SignalR. `mock.js` на новых страницах не грузится, поэтому `page.js` по умолчанию ставит `API_BASE = '/api/v1'`.
- **На новых страницах нет** AI-помощника (`ai.js`) и кнопки настроек API.
- **Ссылку в письмах-приглашениях** лучше поменять на `…/login#invite=`: пример в `docker-compose.prod.yml` обновлён, но старые ссылки тоже работают.
- **Корень `/`** теперь отдаёт сайт, а не Swagger. Swagger остаётся на `/swagger` вне Production.

## Проверка

- `dotnet build -warnaserror`: 0 предупреждений, 0 ошибок.
- `dotnet test`: 189 из 189, на локальном PostgreSQL через `SITORA_TEST_DB`. Новые тесты в `FrontendTests`.
- **В браузере (Development):**
  - главная показывает 3D-дом и квартиры из API;
  - `/crm` без входа уводит на `/login`;
  - вход менеджера ведёт в CRM, открываются сделка и чат (SignalR подключается);
  - переключаются RU и TJ, выход ведёт на главную;
  - старые хеш-ссылки перенаправляются.
- **Не проверено:**
  - кабинет покупателя и платформа после входа (проверены только загрузка и редирект);
  - сборка Docker-образа с новым контекстом: локально Docker не был запущен, её проверит CI.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
