# BINOSOZ — КАРТА ЛОИҲА БАРОИ ОЗМУН

## САҲИФАҲОИ ФРОНТЕНД

| Файл | Мазмун | JS файл |
|------|--------|---------|
| frontend/index.html | Саҳифаи асосӣ (лендинг) | js/site.js, js/app.js |
| frontend/apartments.html | Каталоги квартираҳо + 3D модел | js/house3d.js |
| frontend/nekruz3d.html | 3D модели бино (Babylon.js) | дохилӣ |
| frontend/cabinet.html | Кабинети харидор | js/cabinet.js |
| frontend/crm.html | CRM барои менеҷер | js/crm.js, js/panels.js |
| frontend/platform.html | Суперадмин панел | js/platform.js |
| frontend/login.html | Саҳифаи вуруд | js/app.js |

## СТИЛҲО ВА УМУМӢ

| Файл | Мазмун |
|------|--------|
| frontend/css/styles.css | ҲАМАИ стилҳо (--gold #c9a227, --bg #060d1f) |
| frontend/js/config.js | URL-и API, танзимот |
| frontend/js/api.js | Ҳамаи запросҳо ба бэкенд |
| frontend/js/ui.js | Компонентҳои интерфейс (modal, toast, tabs) |
| frontend/js/i18n.js | Тарҷумаҳо (ru/tg/en) — 1266 калид |
| frontend/js/ai.js | AI ассистент панел |
| frontend/js/mock.js | Маълумоти намунавӣ (вақте API нест) |

## КОНТРОЛЛЕРҲОИ БЭКЕНД

| Файл | Endpoint |
|------|----------|
| Controllers/AuthController.cs | /api/auth/* |
| Controllers/ApartmentsController.cs | /api/apartments/* |
| Controllers/BuildingsController.cs | /api/buildings/* |
| Controllers/BookingsController.cs | /api/bookings/* |
| Controllers/DealsController.cs | /api/deals/* |
| Controllers/PaymentsController.cs | /api/payments/* |
| Controllers/CabinetController.cs | /api/cabinet/* |
| Controllers/CrmController.cs | /api/crm/* |
| Controllers/LeadsController.cs | /api/leads/* |
| Controllers/AiController.cs | /api/ai/* |

## CSS ТОКЕНҲО

--gold: #c9a227 (тиллоӣ)
--bg: #060d1f (замина)
--display: 'Unbounded' (шрифти сарлавҳа)
--font: 'Manrope' (шрифти матн)

ДИҚҚАТ: .side (НЕ .sidebar!), .modal (НЕ .modal-box!)

## DEPLOY

| Хизмат | URL |
|--------|-----|
| Vercel | https://binosoz.vercel.app |
| Render API | https://binosoz-1.onrender.com |
