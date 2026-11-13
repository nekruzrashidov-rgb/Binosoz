# ВАЗИФАҲОИ ЗУДИ ОЗМУН

## РАНГРО ТАҒЙИР ДИҲ
Файл: frontend/css/styles.css
--gold: #c9a227 (тиллоӣ)
--bg: #060d1f (замина)

## ТУГМАРО ТАҒЙИР ДИҲ
HTML: class="btn btn-gold"
Стил: frontend/css/styles.css → .btn-gold { }

## САҲИФАИ АСОСӢ HERO
Файл: frontend/index.html → <section class="hero">
Стил: frontend/css/styles.css → .hero { }

## СЧЁТЧИКҲО
HTML: data-target="160"
JS: frontend/js/site.js → animateCounter()

## КВАРТИРАҲО
Филтр: frontend/apartments.html → .filter-bar + js/house3d.js
Карточка: .apt-card + ApartmentsController.cs
3D модел: frontend/nekruz3d.html

## КАБИНЕТ
Вкладка: frontend/cabinet.html → .tab-btn + js/cabinet.js
API: CabinetController.cs

## CRM
KPI: frontend/crm.html → .kpi-card + js/crm.js
Лид: Entities/Lead.cs + CrmController.cs

## ПАРДОХТҲО (МУҲИМ!)
Controller: PaymentsController.cs
МУҲИМ: decimal(18,2), пардохт НЕСТШУДАН МУМКИН НЕСТ (танҳо сторно)

## AI АССИСТЕНТ
Service: Services/AiAssistant.cs
JS: frontend/js/ai.js

## ТАРҶУМА
Файл: frontend/js/i18n.js
Забонҳо: ru, tg, en

## ТЕСТҲО
dotnet test backend/Sitora.sln
Натиҷа бояд: 312 passed

## GIT
git add -A && git commit -m "fix: тавсиф"
git push
