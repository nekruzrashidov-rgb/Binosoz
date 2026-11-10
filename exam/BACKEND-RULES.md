# ҚОИДАҲОИ БЭКЕНД (МУҲИМ БАРОИ ОЗМУН!)

## ҚОИДАҲОИ АСОСӢ — ВАЙРОН НАКУН!
1. Пул танҳо decimal(18,2) + код асъор (USD, TJS, RUB)
2. Пардохтҳо НЕСТШУДАН МУМКИН НЕСТ — танҳо СТОРНО
3. Як бронирование фаъол барои як квартира
4. Статуси квартира танҳо тавассути бронирование/муомила тағйир меёбад
5. Ҳар тағйир → AuditLog
6. Хатогиҳои чужих → 404 (НЕ 403!)
7. Секретҳо танҳо аз environment variables

## СОХТОРИ ENTITIES
Apartment.cs → статусҳо: Available, Booked, Sold
Building.cs → 20 ошёна, 160 квартира
Booking.cs → бронирование
Deal.cs → муомила (сотиш)
Payment.cs → пардохт (decimal(18,2)!)

## НАҚШҲО (UserRole)
Customer, Manager, Admin, Accountant, Content, SuperAdmin

## ТЕСТҲО ГУЗАРОНИДАН
dotnet test backend/Sitora.sln
Бояд: 312 passed, 0 failed

## МИГРАЦИЯ
dotnet ef migrations add NomMigration --project backend/src/Sitora.Infrastructure
dotnet ef database update --project backend/src/Sitora.Infrastructure
