-- Demo catalog for an empty production database (Render): «Rich Tower», Dushanbe, 20 floors, 160 apartments.
--
-- Run once against the Render PostgreSQL (External Database URL from the Render dashboard):
--   psql "postgresql://USER:PASSWORD@HOST/DB?sslmode=require" -f backend/scripts/seed.sql
--
-- * Idempotent: if the organization already has a building named «Rich Tower», nothing is changed.
-- * Atomic: everything is one DO block — on any error nothing is left half-written.
-- * Floors: 0 (commercial, no apartments) and 1–20 (residential), 8 apartments each, numbers floor×100 + slot+1 (101 … 2008).
-- * Types by plan slot: 0–1 — 1 room 42.5 m² at 7 500 TJS/m²; 2–3 — 2 rooms 68.0 m² at 7 000; 4–5 — 3 rooms 92.0 m² at 6 800;
--   6–7 — 4 rooms 118.5 m² at 6 500. Price = ROUND(area × price per m² / 100) × 100, as Apartment.CalculatePrice
--   (PostgreSQL rounds numeric halves away from zero, like MidpointRounding.AwayFromZero). Money is numeric(18,2), currency TJS.
-- * Statuses: floors 1–3, slots 0–1 — Sold (6); floors 1–5, slot 2 — Booked (5); all others — Free (149).
--   A booked apartment gets a real active booking (with a demo lead: a booking needs a customer or a lead) expiring in 48 hours
--   (as the site promises), so managers can cancel, extend or turn it into a deal in the CRM. Sold apartments have no deal: they are catalog data only.
-- * The seed itself is recorded in AuditLog (Building / Seeded).
-- * A second DO block (any run, never overwrites): the demo eco passport and amenities of the building, the organization
--   tariffs and installment terms, marked as demo data — the same values as GreenDemoSeeder.cs. It also replaces the old
--   description «…бизнес-класса…» (an unverified claim) of an already seeded building and fills the Tajik description if empty.
-- * A third DO block (organization marked demo, no issued PDFs yet): demo completed deals for the sold apartments with
--   issued PDFs (3 schedules × 2 pages, 2 statements × 1 page each), so «Зелёный эффект» shows meaningful demo numbers.
-- * A fourth DO block (any run): active demo bookings get ExpiresAt = now() + 48 h again (the CRM funnel shows «47 ч», not
--   «20 дн»).
-- * The generated block «Rich Tower blocks» (once): the blocks A/B/C of the complex as new buildings (all premises free,
--   USD by the price rule), the site plan, and the old buildings without a block letter hidden from the public site
--   (IsPublished = false, AuditLog Unpublished). Their apartments, deals, schedules and payments are not changed.
-- * The first DO block (any run): the default organization still named «Sitora» or «Некруз Резиденс» and the building named
--   «Некруз Резиденс» are renamed to «Rich Tower» (AuditLog Renamed); a name set by the owner is never changed.
-- Первым делом (каждый запуск): ЖК называется «Rich Tower» (docs/DECISIONS.md, «Rich Tower»). Меняются только прежние
-- имена — организации «Sitora» (имя проекта) и «Некруз Резиденс», дома «Некруз Резиденс»; имя, заданное владельцем, не
-- трогается. Каждое переименование — запись Renamed в AuditLog.
DO $$
DECLARE
  org constant uuid := '01a20000-0000-7000-8000-000000000001';
  old_name text;
  b record;
BEGIN
  SELECT "Name" INTO old_name FROM "Organizations" WHERE "Id" = org AND "Name" IN ('Sitora', 'Некруз Резиденс');
  IF old_name IS NOT NULL THEN
    UPDATE "Organizations" SET "Name" = 'Rich Tower', "UpdatedAt" = now() WHERE "Id" = org;
    INSERT INTO "AuditLog" ("Id", "UserId", "OrganizationId", "ImpersonatedById", "Entity", "EntityId", "Action", "Before", "After",
                            "At", "Ip", "CreatedAt", "UpdatedAt")
    VALUES (gen_random_uuid(), NULL, org, NULL, 'Organization', org, 'Renamed', jsonb_build_object('Name', old_name),
            jsonb_build_object('Name', 'Rich Tower', 'source', 'scripts/seed.sql'), now(), NULL, now(), now());
    RAISE NOTICE 'Организация «%» переименована в «Rich Tower»', old_name;
  END IF;

  FOR b IN SELECT "Id", "NameTg", "NameEn" FROM "Buildings" WHERE "OrganizationId" = org AND "NameRu" = 'Некруз Резиденс' LOOP
    UPDATE "Buildings" SET "NameRu" = 'Rich Tower',
      "NameTg" = CASE WHEN "NameTg" IN ('Некруз Резиденс', 'Некрӯз Резиденс') THEN 'Rich Tower' ELSE "NameTg" END,
      "NameEn" = CASE WHEN "NameEn" = 'Nekruz Residence' THEN 'Rich Tower' ELSE "NameEn" END,
      "UpdatedAt" = now()
    WHERE "Id" = b."Id";
    INSERT INTO "AuditLog" ("Id", "UserId", "OrganizationId", "ImpersonatedById", "Entity", "EntityId", "Action", "Before", "After",
                            "At", "Ip", "CreatedAt", "UpdatedAt")
    VALUES (gen_random_uuid(), NULL, org, NULL, 'Building', b."Id", 'Renamed',
            jsonb_build_object('NameRu', 'Некруз Резиденс', 'NameTg', b."NameTg", 'NameEn', b."NameEn"),
            jsonb_build_object('NameRu', 'Rich Tower', 'source', 'scripts/seed.sql'), now(), NULL, now(), now());
    RAISE NOTICE 'Дом «Некруз Резиденс» переименован в «Rich Tower»';
  END LOOP;
END $$;

DO $$
DECLARE
  org        constant uuid := '01a20000-0000-7000-8000-000000000001';
  ts         constant timestamptz := now();
  building   uuid;
  floor_id   uuid;
  apt_id     uuid;
  lead_id    uuid;
  f          int;
  slot       int;
  rooms      int;
  area       numeric(18,2);
  ppm        numeric(18,2);
  status     text;
  apt_number int;
  sold_n     int := 0;
  booked_n   int := 0;
BEGIN
  IF NOT EXISTS (SELECT 1 FROM "Organizations" WHERE "Id" = org) THEN
    RAISE EXCEPTION 'Нет организации по умолчанию %: сначала запустите API, чтобы он создал её', org;
  END IF;

  IF EXISTS (SELECT 1 FROM "Buildings" WHERE "OrganizationId" = org AND "NameRu" = 'Rich Tower') THEN
    RAISE NOTICE 'Дом «Rich Tower» уже есть — заполнение пропущено';
    RETURN;
  END IF;

  building := gen_random_uuid();
  INSERT INTO "Buildings" ("Id", "OrganizationId", "NameRu", "NameTg", "Address", "FloorsCount", "DescriptionRu", "DescriptionTg",
                           "ModelUrl", "CompletionYear", "PlannedApartments", "CreatedAt", "UpdatedAt")
  VALUES (building, org, 'Rich Tower', 'Rich Tower', 'г. Душанбе, пр. Борбад, 15', 20,
          'Жилой дом на 20 этажей с коммерческим первым этажом', 'Бинои истиқоматии 20-ошёна бо ошёнаи якуми тиҷоратӣ', NULL, 2026, 160, ts, ts);

  -- Commercial floor: no apartments.
  INSERT INTO "Floors" ("Id", "BuildingId", "Number", "PlanImageUrl", "CreatedAt", "UpdatedAt")
  VALUES (gen_random_uuid(), building, 0, NULL, ts, ts);

  FOR f IN 1..20 LOOP
    floor_id := gen_random_uuid();
    INSERT INTO "Floors" ("Id", "BuildingId", "Number", "PlanImageUrl", "CreatedAt", "UpdatedAt")
    VALUES (floor_id, building, f, NULL, ts, ts);

    FOR slot IN 0..7 LOOP
      IF slot <= 1 THEN rooms := 1; area := 42.5;  ppm := 7500;
      ELSIF slot <= 3 THEN rooms := 2; area := 68.0;  ppm := 7000;
      ELSIF slot <= 5 THEN rooms := 3; area := 92.0;  ppm := 6800;
      ELSE                 rooms := 4; area := 118.5; ppm := 6500;
      END IF;

      IF f <= 3 AND slot <= 1 THEN status := 'Sold';
      ELSIF f <= 5 AND slot = 2 THEN status := 'Booked';
      ELSE status := 'Free';
      END IF;

      apt_id := gen_random_uuid();
      apt_number := f * 100 + slot + 1;
      INSERT INTO "Apartments" ("Id", "FloorId", "Number", "PlanSlot", "Model3dId", "Rooms", "Area", "PricePerM2", "Price",
                                "Currency", "Status", "CreatedAt", "UpdatedAt")
      VALUES (apt_id, floor_id, apt_number, slot, NULL, rooms, area, ppm, ROUND(area * ppm / 100) * 100,
              'TJS', status, ts, ts);

      IF status = 'Sold' THEN
        sold_n := sold_n + 1;
      ELSIF status = 'Booked' THEN
        booked_n := booked_n + 1;
        lead_id := gen_random_uuid();
        INSERT INTO "Leads" ("Id", "OrganizationId", "BuildingId", "ApartmentId", "Name", "Phone", "Comment", "Source", "Status",
                             "ManagerId", "CreatedAt", "UpdatedAt")
        VALUES (lead_id, org, building, apt_id, 'Демо-покупатель ' || apt_number, '+9929000000' || lpad(f::text, 2, '0'),
                'Демо-данные: бронь квартиры ' || apt_number, 'Office', 'InProgress', NULL, ts, ts);
        INSERT INTO "Bookings" ("Id", "ApartmentId", "CustomerId", "LeadId", "ExpiresAt", "Status", "CancelReason", "CreatedAt", "UpdatedAt")
        VALUES (gen_random_uuid(), apt_id, NULL, lead_id, ts + interval '48 hours', 'Active', NULL, ts, ts);
      END IF;
    END LOOP;
  END LOOP;

  INSERT INTO "AuditLog" ("Id", "UserId", "OrganizationId", "ImpersonatedById", "Entity", "EntityId", "Action", "Before", "After",
                          "At", "Ip", "CreatedAt", "UpdatedAt")
  VALUES (gen_random_uuid(), NULL, org, NULL, 'Building', building, 'Seeded', NULL,
          jsonb_build_object('source', 'scripts/seed.sql', 'floors', 20, 'apartments', 160, 'sold', sold_n, 'booked', booked_n),
          ts, NULL, ts, ts);

  RAISE NOTICE 'Создан дом «Rich Tower»: 21 этаж (0 — коммерческий), 160 квартир, продано %, в брони %', sold_n, booked_n;
END $$;

-- Демо-значения: эко-паспорт и удобства дома, тарифы и условия рассрочки организации — те же, что в GreenDemoSeeder.cs
-- (SeedSqlTests сверяет). Организация помечается демо (GreenIsDemo): на сайте бейдж «Демо-данные».
-- Ничего не перезаписывает: паспорт — только если не заполнен, удобства — только если список пуст, тарифы — только если
-- тариф не задан, рассрочка — только если условия не заданы.
-- Коэффициент CO₂ не задаётся (проверенного источника нет, docs/DECISIONS.md) — без него CO₂ на сайте не показывается.
DO $$
DECLARE
  org constant uuid := '01a20000-0000-7000-8000-000000000001';
  n   int;
BEGIN
  -- Описание без непроверенного «бизнес-класса» и его перевод; описание, заданное застройщиком, не трогаем.
  UPDATE "Buildings" SET "DescriptionRu" = 'Жилой дом на 20 этажей с коммерческим первым этажом', "UpdatedAt" = now()
  WHERE "OrganizationId" = org AND "NameRu" = 'Rich Tower'
    AND "DescriptionRu" = 'Жилой комплекс бизнес-класса с коммерческим первым этажом';
  GET DIAGNOSTICS n = ROW_COUNT;
  IF n > 0 THEN RAISE NOTICE 'Описание дома заменено на нейтральное'; END IF;

  UPDATE "Buildings" SET "DescriptionTg" = 'Бинои истиқоматии 20-ошёна бо ошёнаи якуми тиҷоратӣ', "UpdatedAt" = now()
  WHERE "OrganizationId" = org AND "NameRu" = 'Rich Tower' AND "DescriptionTg" IS NULL AND "DescriptionRu" = 'Жилой дом на 20 этажей с коммерческим первым этажом';
  GET DIAGNOSTICS n = ROW_COUNT;
  IF n > 0 THEN RAISE NOTICE 'Описание дома на таджикском заполнено'; END IF;

  UPDATE "Buildings" SET
    "EcoEnergyClass" = 'A',
    "EcoWallInsulationRu" = 'Минеральная вата 100 мм',
    "EcoWallInsulationTg" = 'Пахтаи маъданӣ 100 мм',
    "EcoWindowsRu" = 'Двухкамерный стеклопакет',
    "EcoWindowsTg" = 'Шишапакети дукамера',
    "EcoHasHeatMeters" = true,
    "EcoHasWaterMeters" = true,
    "EcoHasLedCommonAreas" = true,
    "EcoHasWasteSorting" = true,
    "EcoGreenAreaM2" = 1200,
    "EcoHeatingType" = 'Central',
    "EcoEstimatedKwhPerM2Year" = 120,
    "UpdatedAt" = now()
  WHERE "OrganizationId" = org AND "NameRu" = 'Rich Tower'
    AND "EcoEnergyClass" IS NULL AND "EcoEstimatedKwhPerM2Year" IS NULL;
  GET DIAGNOSTICS n = ROW_COUNT;
  IF n > 0 THEN RAISE NOTICE 'Эко-паспорт дома заполнен демо-значениями'; END IF;

  -- Английские тексты дома и эко-паспорта (сайт на EN, A-10) — перевод тех же текстов; только пустые поля.
  UPDATE "Buildings" SET
    "NameEn" = COALESCE("NameEn", 'Rich Tower'),
    "DescriptionEn" = COALESCE("DescriptionEn", CASE WHEN "DescriptionRu" = 'Жилой дом на 20 этажей с коммерческим первым этажом'
      THEN 'A 20-storey residential building with a commercial ground floor' END),
    "EcoWallInsulationEn" = COALESCE("EcoWallInsulationEn", CASE WHEN "EcoWallInsulationRu" = 'Минеральная вата 100 мм' THEN 'Mineral wool, 100 mm' END),
    "EcoWindowsEn" = COALESCE("EcoWindowsEn", CASE WHEN "EcoWindowsRu" = 'Двухкамерный стеклопакет' THEN 'Double-chamber glazing units' END),
    "UpdatedAt" = now()
  WHERE "OrganizationId" = org AND "NameRu" = 'Rich Tower'
    AND ("NameEn" IS NULL OR "DescriptionEn" IS NULL OR "EcoWallInsulationEn" IS NULL OR "EcoWindowsEn" IS NULL);
  GET DIAGNOSTICS n = ROW_COUNT;
  IF n > 0 THEN RAISE NOTICE 'Английские тексты дома заполнены'; END IF;

  UPDATE "Organizations" SET
    "GreenTariffKwhTjs" = 0.29,
    "GreenBaselineKwhPerM2Year" = 180,
    "GreenBaselineSource" = 'Демо-значение для показа на конкурсе, не официальные данные',
    "GreenIsDemo" = true,
    "UpdatedAt" = now()
  WHERE "Id" = org AND "GreenTariffKwhTjs" IS NULL;
  GET DIAGNOSTICS n = ROW_COUNT;
  IF n > 0 THEN RAISE NOTICE 'Тарифы организации заполнены демо-значениями (помечены как демо)'; END IF;

  UPDATE "Buildings" SET
    "Amenities" = ARRAY['underground_parking', 'closed_yard', 'playground', 'cctv', 'elevators', 'landscaping'],
    "UpdatedAt" = now()
  WHERE "OrganizationId" = org AND "NameRu" = 'Rich Tower' AND cardinality("Amenities") = 0;
  GET DIAGNOSTICS n = ROW_COUNT;
  IF n > 0 THEN RAISE NOTICE 'Удобства дома заполнены демо-значениями'; END IF;

  UPDATE "Organizations" SET
    "InstallmentDownPaymentMinPct" = 10,
    "InstallmentMaxMonths" = 120,
    "InstallmentNoOverpayment" = true,
    "UpdatedAt" = now()
  WHERE "Id" = org AND "InstallmentDownPaymentMinPct" IS NULL AND "InstallmentMaxMonths" IS NULL AND NOT "InstallmentNoOverpayment";
  GET DIAGNOSTICS n = ROW_COUNT;
  IF n > 0 THEN RAISE NOTICE 'Условия рассрочки заполнены демо-значениями'; END IF;
END $$;

-- Демо-сделки для «Зелёного эффекта» (только у организации, помеченной демо: на сайте бейдж «Демо-данные»).
-- У проданных квартир «Rich Tower» без сделки появляется завершённая демо-сделка с демо-клиентом, и по каждой —
-- выданные онлайн PDF: 3 графика платежей (по 2 страницы) и 2 акта сверки (по 1 странице) за последние месяцы.
-- Ничего не перезаписывает: запускается, только если у организации ещё нет ни одного выданного PDF.
-- Менеджер сделки — первый сотрудник организации; если сотрудников нет, блок пропускается.
DO $$
DECLARE
  org      constant uuid := '01a20000-0000-7000-8000-000000000001';
  manager  uuid;
  apt      record;
  cust_id  uuid;
  deal_id  uuid;
  i        int;
  deals_n  int := 0;
BEGIN
  IF NOT EXISTS (SELECT 1 FROM "Organizations" WHERE "Id" = org AND "GreenIsDemo") THEN
    RAISE NOTICE 'Организация не помечена демо — демо-сделки не добавляются'; RETURN;
  END IF;
  -- Повторный запуск: демо-сделки уже добавлены (по ним — StageReason «Демо-данные»). Настоящие PDF (например, акт
  -- сверки демо-покупателя) не мешают: квартиры со сделкой цикл ниже и так пропускает.
  IF EXISTS (SELECT 1 FROM "Deals" d JOIN "Apartments" a ON a."Id" = d."ApartmentId" JOIN "Floors" f ON f."Id" = a."FloorId"
             JOIN "Buildings" b ON b."Id" = f."BuildingId"
             WHERE b."OrganizationId" = org AND d."StageReason" = 'Демо-данные') THEN
    RAISE NOTICE 'Демо-сделки уже есть — не добавляются'; RETURN;
  END IF;
  SELECT u."Id" INTO manager FROM "Users" u
  WHERE u."OrganizationId" = org AND u."IsActive"
    AND NOT EXISTS (SELECT 1 FROM "UserRoles" ur JOIN "Roles" r ON r."Id" = ur."RoleId" WHERE ur."UserId" = u."Id" AND r."Name" = 'Customer')
  ORDER BY u."Email" LIMIT 1;
  IF manager IS NULL THEN RAISE NOTICE 'Нет сотрудников организации — демо-сделки не добавляются'; RETURN; END IF;

  FOR apt IN
    SELECT a."Id", a."Number", a."Price", a."Currency" FROM "Apartments" a
    JOIN "Floors" f ON f."Id" = a."FloorId" JOIN "Buildings" b ON b."Id" = f."BuildingId"
    WHERE b."OrganizationId" = org AND b."NameRu" = 'Rich Tower' AND a."Status" = 'Sold'
      AND NOT EXISTS (SELECT 1 FROM "Deals" d WHERE d."ApartmentId" = a."Id")
    ORDER BY a."Number"
  LOOP
    cust_id := gen_random_uuid();
    deal_id := gen_random_uuid();
    INSERT INTO "Customers" ("Id", "OrganizationId", "FullName", "Phone", "CreatedAt", "UpdatedAt")
    VALUES (cust_id, org, 'Демо-покупатель кв. ' || apt."Number", '+99290009' || lpad(apt."Number"::text, 4, '0'),
            now() - interval '150 days', now());
    INSERT INTO "Deals" ("Id", "ApartmentId", "CustomerId", "ManagerId", "Stage", "StageReason", "Price", "Discount",
                         "Currency", "CreatedAt", "UpdatedAt")
    VALUES (deal_id, apt."Id", cust_id, manager, 'Completed', 'Демо-данные', apt."Price", 0, apt."Currency",
            now() - interval '150 days', now());
    FOR i IN 1..3 LOOP
      INSERT INTO "IssuedPdfs" ("Id", "OrganizationId", "DealId", "Kind", "PageCount", "CreatedAt", "UpdatedAt")
      VALUES (gen_random_uuid(), org, deal_id, 'Schedule', 2, now() - (i * 35 || ' days')::interval, now());
    END LOOP;
    FOR i IN 1..2 LOOP
      INSERT INTO "IssuedPdfs" ("Id", "OrganizationId", "DealId", "Kind", "PageCount", "CreatedAt", "UpdatedAt")
      VALUES (gen_random_uuid(), org, deal_id, 'Statement', 1, now() - (i * 50 || ' days')::interval, now());
    END LOOP;
    INSERT INTO "AuditLog" ("Id", "UserId", "OrganizationId", "ImpersonatedById", "Entity", "EntityId", "Action", "Before", "After",
                            "At", "Ip", "CreatedAt", "UpdatedAt")
    VALUES (gen_random_uuid(), NULL, org, NULL, 'Deal', deal_id, 'Seeded', NULL,
            jsonb_build_object('source', 'scripts/seed.sql', 'demo', true, 'apartment', apt."Number"), now(), NULL, now(), now());
    deals_n := deals_n + 1;
  END LOOP;
  IF deals_n > 0 THEN RAISE NOTICE 'Демо-сделок с выданными PDF: % (по 5 документов, 8 страниц)', deals_n; END IF;
END $$;

-- Каждый запуск: демо-брони снова держатся 48 часов, как обещает сайт (C-05, docs/DECISIONS.md). Трогаются только
-- действующие демо-брони (лид «Демо-данные: бронь квартиры …» организации по умолчанию).
DO $$
DECLARE
  org constant uuid := '01a20000-0000-7000-8000-000000000001';
  n   int;
BEGIN
  UPDATE "Bookings" b SET "ExpiresAt" = now() + interval '48 hours', "UpdatedAt" = now()
  FROM "Leads" l
  WHERE l."Id" = b."LeadId" AND l."OrganizationId" = org AND l."Comment" LIKE 'Демо-данные: бронь квартиры %'
    AND b."Status" = 'Active';
  GET DIAGNOSTICS n = ROW_COUNT;
  IF n > 0 THEN
    INSERT INTO "AuditLog" ("Id", "UserId", "OrganizationId", "ImpersonatedById", "Entity", "EntityId", "Action", "Before", "After",
                            "At", "Ip", "CreatedAt", "UpdatedAt")
    VALUES (gen_random_uuid(), NULL, org, NULL, 'Booking', org, 'DemoBookingsRenewed', NULL,
            jsonb_build_object('source', 'scripts/seed.sql', 'bookings', n, 'hours', 48), now(), NULL, now(), now());
    RAISE NOTICE 'Демо-брони продлены на 48 часов: %', n;
  END IF;

END $$;

-- BEGIN generated: Rich Tower blocks (RichTowerBlocksSql.cs; UPDATE_SEED_SQL=1 dotnet test --filter RichTowerBlocksSql)
-- Rich Tower — 3 блока на одном участке (концепт, docs/DECISIONS.md). Один раз: если у организации по умолчанию ещё
-- нет блоков (BlockCode), создаются блоки A/B/C со всеми этажами и помещениями (свободными, цены в USD по правилу),
-- план участка (если не задан), и прежние дома организации без буквы скрываются с публичного сайта
-- (IsPublished = false, AuditLog Unpublished). Их квартиры, брони, сделки, графики и платежи не меняются —
-- покупатель видит свою сделку в кабинете, сотрудник — дом в CRM. Повторный запуск ничего не меняет.
DO $$
DECLARE
  org constant uuid := '01a20000-0000-7000-8000-000000000001';
  ts  constant timestamptz := now();
  b   uuid;
  old record;
BEGIN
  IF NOT EXISTS (SELECT 1 FROM "Organizations" WHERE "Id" = org) THEN
    RAISE EXCEPTION 'Нет организации по умолчанию %: сначала запустите API, чтобы он создал её', org;
  END IF;
  IF EXISTS (SELECT 1 FROM "Buildings" WHERE "OrganizationId" = org AND "BlockCode" IS NOT NULL) THEN
    RAISE NOTICE 'Блоки ЖК уже есть — пропущено';
    RETURN;
  END IF;

  -- Блок A: 22 этажей, −2/−1 паркинг, 1–2 магазины, последний — пентхаусы.
  b := gen_random_uuid();
  INSERT INTO "Buildings" ("Id", "OrganizationId", "NameRu", "NameTg", "NameEn", "Address", "FloorsCount", "DescriptionRu",
    "DescriptionTg", "DescriptionEn", "ModelUrl", "CompletionYear", "PlannedApartments", "Amenities", "BlockCode", "SortOrder",
    "Footprint", "SitePosition", "Setbacks", "PriceSteps", "PenthousePricePerM2", "PriceCurrency", "CommercialPricePerM2",
    "ParkingSpotPrice", "ElevatorsCount", "IsPublished", "CreatedAt", "UpdatedAt")
  VALUES (b, org, 'Блок A', 'Блоки A', 'Block A', 'г. Душанбе, пр. Борбад, 15', 22,
    'Концепт: форма, расположение и планировки блока — визуализация до проекта застройщика, не проектная документация.',
    'Консепсия: шакл, ҷойгиршавӣ ва нақшаҳои блок — визуализатсия то лоиҳаи сохтмончӣ, ҳуҷҷати лоиҳавӣ нест.',
    'Concept: the block''s shape, placement and layouts are a visualisation before the developer''s project, not design documentation.',
    NULL, NULL, 156, ARRAY['underground_parking', 'closed_yard', 'playground', 'football_field', 'elevators', 'landscaping', 'commercial']::text[], 'A', 1,
    '[[10,0],[38,0],[38,18],[19,18],[19,34],[0,34],[0,0,6]]'::jsonb,
    '{"x":-89,"y":-50,"rotationDeg":0}'::jsonb,
    '[{"fromFloor":22,"footprint":[[10,2.5],[38,2.5],[38,18],[19,18],[19,34],[2.5,34],[2.5,2.5,5]],"planDepth":6.5}]'::jsonb,
    '[{"fromFloor":3,"toFloor":5,"pricePerM2":1200,"pricePerM2To":null},{"fromFloor":6,"toFloor":19,"pricePerM2":1200,"pricePerM2To":900},{"fromFloor":20,"toFloor":21,"pricePerM2":900,"pricePerM2To":null}]'::jsonb,
    1400, 'USD', 1500, 20000, 3, true, ts, ts);
  INSERT INTO "Floors" ("Id", "BuildingId", "Number", "PlanImageUrl", "CreatedAt", "UpdatedAt")
  SELECT gen_random_uuid(), b, n, NULL, ts, ts FROM unnest(ARRAY[-2, -1, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22]) AS n;
  INSERT INTO "Apartments" ("Id", "FloorId", "Number", "PlanSlot", "Model3dId", "Rooms", "Area", "PricePerM2", "Price", "Currency",
    "Status", "Kind", "TerraceArea", "CreatedAt", "UpdatedAt")
  SELECT gen_random_uuid(), f."Id", v.num, v.slot, NULL, v.rooms, v.area, v.ppm, v.price, 'USD', 'Free', v.kind, v.terrace, ts, ts
  FROM (VALUES
    (-2, 201, 0, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 202, 1, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 203, 2, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 204, 3, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 205, 4, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 206, 5, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 207, 6, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 208, 7, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 209, 8, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 210, 9, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 211, 10, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 212, 11, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 213, 12, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 214, 13, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 215, 14, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 216, 15, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 217, 16, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 218, 17, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 219, 18, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 220, 19, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 221, 20, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 222, 21, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 223, 22, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 224, 23, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 225, 24, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 226, 25, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 227, 26, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 228, 27, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 101, 0, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 102, 1, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 103, 2, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 104, 3, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 105, 4, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 106, 5, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 107, 6, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 108, 7, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 109, 8, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 110, 9, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 111, 10, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 112, 11, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 113, 12, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 114, 13, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 115, 14, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 116, 15, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 117, 16, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 118, 17, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 119, 18, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 120, 19, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 121, 20, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 122, 21, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 123, 22, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 124, 23, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 125, 24, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 126, 25, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 127, 26, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 128, 27, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (1, 101, 0, 0, 'Commercial', 162.4, 1500, 243600, NULL::numeric),
    (1, 102, 1, 0, 'Commercial', 108.3, 1500, 162500, NULL::numeric),
    (1, 103, 2, 0, 'Commercial', 135.3, 1500, 203000, NULL::numeric),
    (1, 104, 3, 0, 'Commercial', 162.4, 1500, 243600, NULL::numeric),
    (1, 105, 4, 0, 'Commercial', 108.3, 1500, 162500, NULL::numeric),
    (1, 106, 5, 0, 'Commercial', 135.3, 1500, 203000, NULL::numeric),
    (2, 201, 0, 0, 'Commercial', 162.4, 1500, 243600, NULL::numeric),
    (2, 202, 1, 0, 'Commercial', 108.3, 1500, 162500, NULL::numeric),
    (2, 203, 2, 0, 'Commercial', 135.3, 1500, 203000, NULL::numeric),
    (2, 204, 3, 0, 'Commercial', 162.4, 1500, 243600, NULL::numeric),
    (2, 205, 4, 0, 'Commercial', 108.3, 1500, 162500, NULL::numeric),
    (2, 206, 5, 0, 'Commercial', 135.3, 1500, 203000, NULL::numeric),
    (3, 301, 0, 0, 'Residential', 54.1, 1200, 64900, NULL::numeric),
    (3, 302, 1, 4, 'Residential', 157.9, 1200, 189500, NULL::numeric),
    (3, 303, 2, 1, 'Residential', 72.2, 1200, 86600, NULL::numeric),
    (3, 304, 3, 2, 'Residential', 90.2, 1200, 108200, NULL::numeric),
    (3, 305, 4, 4, 'Residential', 157.9, 1200, 189500, NULL::numeric),
    (3, 306, 5, 0, 'Residential', 54.1, 1200, 64900, NULL::numeric),
    (3, 307, 6, 1, 'Residential', 72.2, 1200, 86600, NULL::numeric),
    (3, 308, 7, 3, 'Residential', 153.4, 1200, 184100, NULL::numeric),
    (4, 401, 0, 0, 'Residential', 54.1, 1200, 64900, NULL::numeric),
    (4, 402, 1, 4, 'Residential', 157.9, 1200, 189500, NULL::numeric),
    (4, 403, 2, 1, 'Residential', 72.2, 1200, 86600, NULL::numeric),
    (4, 404, 3, 2, 'Residential', 90.2, 1200, 108200, NULL::numeric),
    (4, 405, 4, 4, 'Residential', 157.9, 1200, 189500, NULL::numeric),
    (4, 406, 5, 0, 'Residential', 54.1, 1200, 64900, NULL::numeric),
    (4, 407, 6, 1, 'Residential', 72.2, 1200, 86600, NULL::numeric),
    (4, 408, 7, 3, 'Residential', 153.4, 1200, 184100, NULL::numeric),
    (5, 501, 0, 0, 'Residential', 54.1, 1200, 64900, NULL::numeric),
    (5, 502, 1, 4, 'Residential', 157.9, 1200, 189500, NULL::numeric),
    (5, 503, 2, 1, 'Residential', 72.2, 1200, 86600, NULL::numeric),
    (5, 504, 3, 2, 'Residential', 90.2, 1200, 108200, NULL::numeric),
    (5, 505, 4, 4, 'Residential', 157.9, 1200, 189500, NULL::numeric),
    (5, 506, 5, 0, 'Residential', 54.1, 1200, 64900, NULL::numeric),
    (5, 507, 6, 1, 'Residential', 72.2, 1200, 86600, NULL::numeric),
    (5, 508, 7, 3, 'Residential', 153.4, 1200, 184100, NULL::numeric),
    (6, 601, 0, 0, 'Residential', 54.1, 1200, 64900, NULL::numeric),
    (6, 602, 1, 4, 'Residential', 157.9, 1200, 189500, NULL::numeric),
    (6, 603, 2, 1, 'Residential', 72.2, 1200, 86600, NULL::numeric),
    (6, 604, 3, 2, 'Residential', 90.2, 1200, 108200, NULL::numeric),
    (6, 605, 4, 4, 'Residential', 157.9, 1200, 189500, NULL::numeric),
    (6, 606, 5, 0, 'Residential', 54.1, 1200, 64900, NULL::numeric),
    (6, 607, 6, 1, 'Residential', 72.2, 1200, 86600, NULL::numeric),
    (6, 608, 7, 3, 'Residential', 153.4, 1200, 184100, NULL::numeric),
    (7, 701, 0, 0, 'Residential', 54.1, 1177, 63700, NULL::numeric),
    (7, 702, 1, 4, 'Residential', 157.9, 1177, 185800, NULL::numeric),
    (7, 703, 2, 1, 'Residential', 72.2, 1177, 85000, NULL::numeric),
    (7, 704, 3, 2, 'Residential', 90.2, 1177, 106200, NULL::numeric),
    (7, 705, 4, 4, 'Residential', 157.9, 1177, 185800, NULL::numeric),
    (7, 706, 5, 0, 'Residential', 54.1, 1177, 63700, NULL::numeric),
    (7, 707, 6, 1, 'Residential', 72.2, 1177, 85000, NULL::numeric),
    (7, 708, 7, 3, 'Residential', 153.4, 1177, 180600, NULL::numeric),
    (8, 801, 0, 0, 'Residential', 54.1, 1154, 62400, NULL::numeric),
    (8, 802, 1, 4, 'Residential', 157.9, 1154, 182200, NULL::numeric),
    (8, 803, 2, 1, 'Residential', 72.2, 1154, 83300, NULL::numeric),
    (8, 804, 3, 2, 'Residential', 90.2, 1154, 104100, NULL::numeric),
    (8, 805, 4, 4, 'Residential', 157.9, 1154, 182200, NULL::numeric),
    (8, 806, 5, 0, 'Residential', 54.1, 1154, 62400, NULL::numeric),
    (8, 807, 6, 1, 'Residential', 72.2, 1154, 83300, NULL::numeric),
    (8, 808, 7, 3, 'Residential', 153.4, 1154, 177000, NULL::numeric),
    (9, 901, 0, 0, 'Residential', 54.1, 1131, 61200, NULL::numeric),
    (9, 902, 1, 4, 'Residential', 157.9, 1131, 178600, NULL::numeric),
    (9, 903, 2, 1, 'Residential', 72.2, 1131, 81700, NULL::numeric),
    (9, 904, 3, 2, 'Residential', 90.2, 1131, 102000, NULL::numeric),
    (9, 905, 4, 4, 'Residential', 157.9, 1131, 178600, NULL::numeric),
    (9, 906, 5, 0, 'Residential', 54.1, 1131, 61200, NULL::numeric),
    (9, 907, 6, 1, 'Residential', 72.2, 1131, 81700, NULL::numeric),
    (9, 908, 7, 3, 'Residential', 153.4, 1131, 173500, NULL::numeric),
    (10, 1001, 0, 0, 'Residential', 54.1, 1108, 59900, NULL::numeric),
    (10, 1002, 1, 4, 'Residential', 157.9, 1108, 175000, NULL::numeric),
    (10, 1003, 2, 1, 'Residential', 72.2, 1108, 80000, NULL::numeric),
    (10, 1004, 3, 2, 'Residential', 90.2, 1108, 99900, NULL::numeric),
    (10, 1005, 4, 4, 'Residential', 157.9, 1108, 175000, NULL::numeric),
    (10, 1006, 5, 0, 'Residential', 54.1, 1108, 59900, NULL::numeric),
    (10, 1007, 6, 1, 'Residential', 72.2, 1108, 80000, NULL::numeric),
    (10, 1008, 7, 3, 'Residential', 153.4, 1108, 170000, NULL::numeric),
    (11, 1101, 0, 0, 'Residential', 54.1, 1085, 58700, NULL::numeric),
    (11, 1102, 1, 4, 'Residential', 157.9, 1085, 171300, NULL::numeric),
    (11, 1103, 2, 1, 'Residential', 72.2, 1085, 78300, NULL::numeric),
    (11, 1104, 3, 2, 'Residential', 90.2, 1085, 97900, NULL::numeric),
    (11, 1105, 4, 4, 'Residential', 157.9, 1085, 171300, NULL::numeric),
    (11, 1106, 5, 0, 'Residential', 54.1, 1085, 58700, NULL::numeric),
    (11, 1107, 6, 1, 'Residential', 72.2, 1085, 78300, NULL::numeric),
    (11, 1108, 7, 3, 'Residential', 153.4, 1085, 166400, NULL::numeric),
    (12, 1201, 0, 0, 'Residential', 54.1, 1062, 57500, NULL::numeric),
    (12, 1202, 1, 4, 'Residential', 157.9, 1062, 167700, NULL::numeric),
    (12, 1203, 2, 1, 'Residential', 72.2, 1062, 76700, NULL::numeric),
    (12, 1204, 3, 2, 'Residential', 90.2, 1062, 95800, NULL::numeric),
    (12, 1205, 4, 4, 'Residential', 157.9, 1062, 167700, NULL::numeric),
    (12, 1206, 5, 0, 'Residential', 54.1, 1062, 57500, NULL::numeric),
    (12, 1207, 6, 1, 'Residential', 72.2, 1062, 76700, NULL::numeric),
    (12, 1208, 7, 3, 'Residential', 153.4, 1062, 162900, NULL::numeric),
    (13, 1301, 0, 0, 'Residential', 54.1, 1038, 56200, NULL::numeric),
    (13, 1302, 1, 4, 'Residential', 157.9, 1038, 163900, NULL::numeric),
    (13, 1303, 2, 1, 'Residential', 72.2, 1038, 74900, NULL::numeric),
    (13, 1304, 3, 2, 'Residential', 90.2, 1038, 93600, NULL::numeric),
    (13, 1305, 4, 4, 'Residential', 157.9, 1038, 163900, NULL::numeric),
    (13, 1306, 5, 0, 'Residential', 54.1, 1038, 56200, NULL::numeric),
    (13, 1307, 6, 1, 'Residential', 72.2, 1038, 74900, NULL::numeric),
    (13, 1308, 7, 3, 'Residential', 153.4, 1038, 159200, NULL::numeric),
    (14, 1401, 0, 0, 'Residential', 54.1, 1015, 54900, NULL::numeric),
    (14, 1402, 1, 4, 'Residential', 157.9, 1015, 160300, NULL::numeric),
    (14, 1403, 2, 1, 'Residential', 72.2, 1015, 73300, NULL::numeric),
    (14, 1404, 3, 2, 'Residential', 90.2, 1015, 91600, NULL::numeric),
    (14, 1405, 4, 4, 'Residential', 157.9, 1015, 160300, NULL::numeric),
    (14, 1406, 5, 0, 'Residential', 54.1, 1015, 54900, NULL::numeric),
    (14, 1407, 6, 1, 'Residential', 72.2, 1015, 73300, NULL::numeric),
    (14, 1408, 7, 3, 'Residential', 153.4, 1015, 155700, NULL::numeric),
    (15, 1501, 0, 0, 'Residential', 54.1, 992, 53700, NULL::numeric),
    (15, 1502, 1, 4, 'Residential', 157.9, 992, 156600, NULL::numeric),
    (15, 1503, 2, 1, 'Residential', 72.2, 992, 71600, NULL::numeric),
    (15, 1504, 3, 2, 'Residential', 90.2, 992, 89500, NULL::numeric),
    (15, 1505, 4, 4, 'Residential', 157.9, 992, 156600, NULL::numeric),
    (15, 1506, 5, 0, 'Residential', 54.1, 992, 53700, NULL::numeric),
    (15, 1507, 6, 1, 'Residential', 72.2, 992, 71600, NULL::numeric),
    (15, 1508, 7, 3, 'Residential', 153.4, 992, 152200, NULL::numeric),
    (16, 1601, 0, 0, 'Residential', 54.1, 969, 52400, NULL::numeric),
    (16, 1602, 1, 4, 'Residential', 157.9, 969, 153000, NULL::numeric),
    (16, 1603, 2, 1, 'Residential', 72.2, 969, 70000, NULL::numeric),
    (16, 1604, 3, 2, 'Residential', 90.2, 969, 87400, NULL::numeric),
    (16, 1605, 4, 4, 'Residential', 157.9, 969, 153000, NULL::numeric),
    (16, 1606, 5, 0, 'Residential', 54.1, 969, 52400, NULL::numeric),
    (16, 1607, 6, 1, 'Residential', 72.2, 969, 70000, NULL::numeric),
    (16, 1608, 7, 3, 'Residential', 153.4, 969, 148600, NULL::numeric),
    (17, 1701, 0, 0, 'Residential', 54.1, 946, 51200, NULL::numeric),
    (17, 1702, 1, 4, 'Residential', 157.9, 946, 149400, NULL::numeric),
    (17, 1703, 2, 1, 'Residential', 72.2, 946, 68300, NULL::numeric),
    (17, 1704, 3, 2, 'Residential', 90.2, 946, 85300, NULL::numeric),
    (17, 1705, 4, 4, 'Residential', 157.9, 946, 149400, NULL::numeric),
    (17, 1706, 5, 0, 'Residential', 54.1, 946, 51200, NULL::numeric),
    (17, 1707, 6, 1, 'Residential', 72.2, 946, 68300, NULL::numeric),
    (17, 1708, 7, 3, 'Residential', 153.4, 946, 145100, NULL::numeric),
    (18, 1801, 0, 0, 'Residential', 54.1, 923, 49900, NULL::numeric),
    (18, 1802, 1, 4, 'Residential', 157.9, 923, 145700, NULL::numeric),
    (18, 1803, 2, 1, 'Residential', 72.2, 923, 66600, NULL::numeric),
    (18, 1804, 3, 2, 'Residential', 90.2, 923, 83300, NULL::numeric),
    (18, 1805, 4, 4, 'Residential', 157.9, 923, 145700, NULL::numeric),
    (18, 1806, 5, 0, 'Residential', 54.1, 923, 49900, NULL::numeric),
    (18, 1807, 6, 1, 'Residential', 72.2, 923, 66600, NULL::numeric),
    (18, 1808, 7, 3, 'Residential', 153.4, 923, 141600, NULL::numeric),
    (19, 1901, 0, 0, 'Residential', 54.1, 900, 48700, NULL::numeric),
    (19, 1902, 1, 4, 'Residential', 157.9, 900, 142100, NULL::numeric),
    (19, 1903, 2, 1, 'Residential', 72.2, 900, 65000, NULL::numeric),
    (19, 1904, 3, 2, 'Residential', 90.2, 900, 81200, NULL::numeric),
    (19, 1905, 4, 4, 'Residential', 157.9, 900, 142100, NULL::numeric),
    (19, 1906, 5, 0, 'Residential', 54.1, 900, 48700, NULL::numeric),
    (19, 1907, 6, 1, 'Residential', 72.2, 900, 65000, NULL::numeric),
    (19, 1908, 7, 3, 'Residential', 153.4, 900, 138100, NULL::numeric),
    (20, 2001, 0, 0, 'Residential', 54.1, 900, 48700, NULL::numeric),
    (20, 2002, 1, 4, 'Residential', 157.9, 900, 142100, NULL::numeric),
    (20, 2003, 2, 1, 'Residential', 72.2, 900, 65000, NULL::numeric),
    (20, 2004, 3, 2, 'Residential', 90.2, 900, 81200, NULL::numeric),
    (20, 2005, 4, 4, 'Residential', 157.9, 900, 142100, NULL::numeric),
    (20, 2006, 5, 0, 'Residential', 54.1, 900, 48700, NULL::numeric),
    (20, 2007, 6, 1, 'Residential', 72.2, 900, 65000, NULL::numeric),
    (20, 2008, 7, 3, 'Residential', 153.4, 900, 138100, NULL::numeric),
    (21, 2101, 0, 0, 'Residential', 54.1, 900, 48700, NULL::numeric),
    (21, 2102, 1, 4, 'Residential', 157.9, 900, 142100, NULL::numeric),
    (21, 2103, 2, 1, 'Residential', 72.2, 900, 65000, NULL::numeric),
    (21, 2104, 3, 2, 'Residential', 90.2, 900, 81200, NULL::numeric),
    (21, 2105, 4, 4, 'Residential', 157.9, 900, 142100, NULL::numeric),
    (21, 2106, 5, 0, 'Residential', 54.1, 900, 48700, NULL::numeric),
    (21, 2107, 6, 1, 'Residential', 72.2, 900, 65000, NULL::numeric),
    (21, 2108, 7, 3, 'Residential', 153.4, 900, 138100, NULL::numeric),
    (22, 2201, 0, 4, 'Penthouse', 175.5, 1400, 245700, 42.9::numeric),
    (22, 2202, 1, 4, 'Penthouse', 175.5, 1400, 245700, 42.9::numeric),
    (22, 2203, 2, 4, 'Penthouse', 175.5, 1400, 245700, 42.9::numeric),
    (22, 2204, 3, 4, 'Penthouse', 175.5, 1400, 245700, 42.9::numeric)
  ) AS v(floor, num, slot, rooms, kind, area, ppm, price, terrace)
  JOIN "Floors" f ON f."BuildingId" = b AND f."Number" = v.floor;
  INSERT INTO "AuditLog" ("Id", "UserId", "OrganizationId", "ImpersonatedById", "Entity", "EntityId", "Action", "Before", "After",
                          "At", "Ip", "CreatedAt", "UpdatedAt")
  VALUES (gen_random_uuid(), NULL, org, NULL, 'Building', b, 'Seeded', NULL,
          jsonb_build_object('source', 'scripts/seed.sql', 'blockCode', 'A', 'concept', true, 'apartments', 224), ts, NULL, ts, ts);
  RAISE NOTICE 'Создан блок A: % помещений', 224;

  -- Блок B: 20 этажей, −2/−1 паркинг, 1–2 магазины, последний — пентхаусы.
  b := gen_random_uuid();
  INSERT INTO "Buildings" ("Id", "OrganizationId", "NameRu", "NameTg", "NameEn", "Address", "FloorsCount", "DescriptionRu",
    "DescriptionTg", "DescriptionEn", "ModelUrl", "CompletionYear", "PlannedApartments", "Amenities", "BlockCode", "SortOrder",
    "Footprint", "SitePosition", "Setbacks", "PriceSteps", "PenthousePricePerM2", "PriceCurrency", "CommercialPricePerM2",
    "ParkingSpotPrice", "ElevatorsCount", "IsPublished", "CreatedAt", "UpdatedAt")
  VALUES (b, org, 'Блок B', 'Блоки B', 'Block B', 'г. Душанбе, пр. Борбад, 15', 20,
    'Концепт: форма, расположение и планировки блока — визуализация до проекта застройщика, не проектная документация.',
    'Консепсия: шакл, ҷойгиршавӣ ва нақшаҳои блок — визуализатсия то лоиҳаи сохтмончӣ, ҳуҷҷати лоиҳавӣ нест.',
    'Concept: the block''s shape, placement and layouts are a visualisation before the developer''s project, not design documentation.',
    NULL, NULL, 140, ARRAY['underground_parking', 'closed_yard', 'playground', 'football_field', 'elevators', 'landscaping', 'commercial']::text[], 'B', 2,
    '[[24,0],[31,0],[32.5,-1.5],[37.5,-1.5],[39,0],[46,0],[50,4],[50,16],[46,20],[39,20],[37.5,21.5],[32.5,21.5],[31,20],[19,20],[17.5,21.5],[12.5,21.5],[11,20],[4,20],[0,16],[0,4],[4,0],[11,0],[12.5,-1.5],[17.5,-1.5],[19,0]]'::jsonb,
    '{"x":-25,"y":-60,"rotationDeg":0}'::jsonb,
    '[{"fromFloor":20,"footprint":[[24,0],[43,0],[47,4],[47,16],[43,20],[7,20],[3,16],[3,4],[7,0]],"planDepth":8}]'::jsonb,
    '[{"fromFloor":3,"toFloor":5,"pricePerM2":1200,"pricePerM2To":null},{"fromFloor":6,"toFloor":19,"pricePerM2":1200,"pricePerM2To":900},{"fromFloor":20,"toFloor":20,"pricePerM2":900,"pricePerM2To":null}]'::jsonb,
    1400, 'USD', 1500, 20000, 3, true, ts, ts);
  INSERT INTO "Floors" ("Id", "BuildingId", "Number", "PlanImageUrl", "CreatedAt", "UpdatedAt")
  SELECT gen_random_uuid(), b, n, NULL, ts, ts FROM unnest(ARRAY[-2, -1, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20]) AS n;
  INSERT INTO "Apartments" ("Id", "FloorId", "Number", "PlanSlot", "Model3dId", "Rooms", "Area", "PricePerM2", "Price", "Currency",
    "Status", "Kind", "TerraceArea", "CreatedAt", "UpdatedAt")
  SELECT gen_random_uuid(), f."Id", v.num, v.slot, NULL, v.rooms, v.area, v.ppm, v.price, 'USD', 'Free', v.kind, v.terrace, ts, ts
  FROM (VALUES
    (-2, 201, 0, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 202, 1, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 203, 2, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 204, 3, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 205, 4, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 206, 5, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 207, 6, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 208, 7, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 209, 8, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 210, 9, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 211, 10, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 212, 11, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 213, 12, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 214, 13, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 215, 14, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 216, 15, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 217, 16, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 218, 17, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 219, 18, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 220, 19, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 221, 20, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 222, 21, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 223, 22, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 224, 23, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 225, 24, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 226, 25, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 227, 26, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 228, 27, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 101, 0, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 102, 1, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 103, 2, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 104, 3, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 105, 4, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 106, 5, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 107, 6, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 108, 7, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 109, 8, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 110, 9, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 111, 10, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 112, 11, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 113, 12, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 114, 13, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 115, 14, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 116, 15, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 117, 16, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 118, 17, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 119, 18, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 120, 19, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 121, 20, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 122, 21, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 123, 22, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 124, 23, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 125, 24, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 126, 25, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 127, 26, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 128, 27, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (1, 101, 0, 0, 'Commercial', 157.2, 1500, 235800, NULL::numeric),
    (1, 102, 1, 0, 'Commercial', 104.8, 1500, 157200, NULL::numeric),
    (1, 103, 2, 0, 'Commercial', 131.0, 1500, 196500, NULL::numeric),
    (1, 104, 3, 0, 'Commercial', 157.2, 1500, 235800, NULL::numeric),
    (1, 105, 4, 0, 'Commercial', 104.8, 1500, 157200, NULL::numeric),
    (1, 106, 5, 0, 'Commercial', 131.0, 1500, 196500, NULL::numeric),
    (2, 201, 0, 0, 'Commercial', 157.2, 1500, 235800, NULL::numeric),
    (2, 202, 1, 0, 'Commercial', 104.8, 1500, 157200, NULL::numeric),
    (2, 203, 2, 0, 'Commercial', 131.0, 1500, 196500, NULL::numeric),
    (2, 204, 3, 0, 'Commercial', 157.2, 1500, 235800, NULL::numeric),
    (2, 205, 4, 0, 'Commercial', 104.8, 1500, 157200, NULL::numeric),
    (2, 206, 5, 0, 'Commercial', 131.0, 1500, 196500, NULL::numeric),
    (3, 301, 0, 0, 'Residential', 52.1, 1200, 62500, NULL::numeric),
    (3, 302, 1, 1, 'Residential', 80.5, 1200, 96600, NULL::numeric),
    (3, 303, 2, 4, 'Residential', 170.5, 1200, 204600, NULL::numeric),
    (3, 304, 3, 2, 'Residential', 90.0, 1200, 108000, NULL::numeric),
    (3, 305, 4, 0, 'Residential', 52.1, 1200, 62500, NULL::numeric),
    (3, 306, 5, 1, 'Residential', 80.5, 1200, 96600, NULL::numeric),
    (3, 307, 6, 4, 'Residential', 170.5, 1200, 204600, NULL::numeric),
    (3, 308, 7, 2, 'Residential', 90.0, 1200, 108000, NULL::numeric),
    (4, 401, 0, 0, 'Residential', 52.1, 1200, 62500, NULL::numeric),
    (4, 402, 1, 1, 'Residential', 80.5, 1200, 96600, NULL::numeric),
    (4, 403, 2, 4, 'Residential', 170.5, 1200, 204600, NULL::numeric),
    (4, 404, 3, 2, 'Residential', 90.0, 1200, 108000, NULL::numeric),
    (4, 405, 4, 0, 'Residential', 52.1, 1200, 62500, NULL::numeric),
    (4, 406, 5, 1, 'Residential', 80.5, 1200, 96600, NULL::numeric),
    (4, 407, 6, 4, 'Residential', 170.5, 1200, 204600, NULL::numeric),
    (4, 408, 7, 2, 'Residential', 90.0, 1200, 108000, NULL::numeric),
    (5, 501, 0, 0, 'Residential', 52.1, 1200, 62500, NULL::numeric),
    (5, 502, 1, 1, 'Residential', 80.5, 1200, 96600, NULL::numeric),
    (5, 503, 2, 4, 'Residential', 170.5, 1200, 204600, NULL::numeric),
    (5, 504, 3, 2, 'Residential', 90.0, 1200, 108000, NULL::numeric),
    (5, 505, 4, 0, 'Residential', 52.1, 1200, 62500, NULL::numeric),
    (5, 506, 5, 1, 'Residential', 80.5, 1200, 96600, NULL::numeric),
    (5, 507, 6, 4, 'Residential', 170.5, 1200, 204600, NULL::numeric),
    (5, 508, 7, 2, 'Residential', 90.0, 1200, 108000, NULL::numeric),
    (6, 601, 0, 0, 'Residential', 52.1, 1200, 62500, NULL::numeric),
    (6, 602, 1, 1, 'Residential', 80.5, 1200, 96600, NULL::numeric),
    (6, 603, 2, 4, 'Residential', 170.5, 1200, 204600, NULL::numeric),
    (6, 604, 3, 2, 'Residential', 90.0, 1200, 108000, NULL::numeric),
    (6, 605, 4, 0, 'Residential', 52.1, 1200, 62500, NULL::numeric),
    (6, 606, 5, 1, 'Residential', 80.5, 1200, 96600, NULL::numeric),
    (6, 607, 6, 4, 'Residential', 170.5, 1200, 204600, NULL::numeric),
    (6, 608, 7, 2, 'Residential', 90.0, 1200, 108000, NULL::numeric),
    (7, 701, 0, 0, 'Residential', 52.1, 1177, 61300, NULL::numeric),
    (7, 702, 1, 1, 'Residential', 80.5, 1177, 94700, NULL::numeric),
    (7, 703, 2, 4, 'Residential', 170.5, 1177, 200700, NULL::numeric),
    (7, 704, 3, 2, 'Residential', 90.0, 1177, 105900, NULL::numeric),
    (7, 705, 4, 0, 'Residential', 52.1, 1177, 61300, NULL::numeric),
    (7, 706, 5, 1, 'Residential', 80.5, 1177, 94700, NULL::numeric),
    (7, 707, 6, 4, 'Residential', 170.5, 1177, 200700, NULL::numeric),
    (7, 708, 7, 2, 'Residential', 90.0, 1177, 105900, NULL::numeric),
    (8, 801, 0, 0, 'Residential', 52.1, 1154, 60100, NULL::numeric),
    (8, 802, 1, 1, 'Residential', 80.5, 1154, 92900, NULL::numeric),
    (8, 803, 2, 4, 'Residential', 170.5, 1154, 196800, NULL::numeric),
    (8, 804, 3, 2, 'Residential', 90.0, 1154, 103900, NULL::numeric),
    (8, 805, 4, 0, 'Residential', 52.1, 1154, 60100, NULL::numeric),
    (8, 806, 5, 1, 'Residential', 80.5, 1154, 92900, NULL::numeric),
    (8, 807, 6, 4, 'Residential', 170.5, 1154, 196800, NULL::numeric),
    (8, 808, 7, 2, 'Residential', 90.0, 1154, 103900, NULL::numeric),
    (9, 901, 0, 0, 'Residential', 52.1, 1131, 58900, NULL::numeric),
    (9, 902, 1, 1, 'Residential', 80.5, 1131, 91000, NULL::numeric),
    (9, 903, 2, 4, 'Residential', 170.5, 1131, 192800, NULL::numeric),
    (9, 904, 3, 2, 'Residential', 90.0, 1131, 101800, NULL::numeric),
    (9, 905, 4, 0, 'Residential', 52.1, 1131, 58900, NULL::numeric),
    (9, 906, 5, 1, 'Residential', 80.5, 1131, 91000, NULL::numeric),
    (9, 907, 6, 4, 'Residential', 170.5, 1131, 192800, NULL::numeric),
    (9, 908, 7, 2, 'Residential', 90.0, 1131, 101800, NULL::numeric),
    (10, 1001, 0, 0, 'Residential', 52.1, 1108, 57700, NULL::numeric),
    (10, 1002, 1, 1, 'Residential', 80.5, 1108, 89200, NULL::numeric),
    (10, 1003, 2, 4, 'Residential', 170.5, 1108, 188900, NULL::numeric),
    (10, 1004, 3, 2, 'Residential', 90.0, 1108, 99700, NULL::numeric),
    (10, 1005, 4, 0, 'Residential', 52.1, 1108, 57700, NULL::numeric),
    (10, 1006, 5, 1, 'Residential', 80.5, 1108, 89200, NULL::numeric),
    (10, 1007, 6, 4, 'Residential', 170.5, 1108, 188900, NULL::numeric),
    (10, 1008, 7, 2, 'Residential', 90.0, 1108, 99700, NULL::numeric),
    (11, 1101, 0, 0, 'Residential', 52.1, 1085, 56500, NULL::numeric),
    (11, 1102, 1, 1, 'Residential', 80.5, 1085, 87300, NULL::numeric),
    (11, 1103, 2, 4, 'Residential', 170.5, 1085, 185000, NULL::numeric),
    (11, 1104, 3, 2, 'Residential', 90.0, 1085, 97700, NULL::numeric),
    (11, 1105, 4, 0, 'Residential', 52.1, 1085, 56500, NULL::numeric),
    (11, 1106, 5, 1, 'Residential', 80.5, 1085, 87300, NULL::numeric),
    (11, 1107, 6, 4, 'Residential', 170.5, 1085, 185000, NULL::numeric),
    (11, 1108, 7, 2, 'Residential', 90.0, 1085, 97700, NULL::numeric),
    (12, 1201, 0, 0, 'Residential', 52.1, 1062, 55300, NULL::numeric),
    (12, 1202, 1, 1, 'Residential', 80.5, 1062, 85500, NULL::numeric),
    (12, 1203, 2, 4, 'Residential', 170.5, 1062, 181100, NULL::numeric),
    (12, 1204, 3, 2, 'Residential', 90.0, 1062, 95600, NULL::numeric),
    (12, 1205, 4, 0, 'Residential', 52.1, 1062, 55300, NULL::numeric),
    (12, 1206, 5, 1, 'Residential', 80.5, 1062, 85500, NULL::numeric),
    (12, 1207, 6, 4, 'Residential', 170.5, 1062, 181100, NULL::numeric),
    (12, 1208, 7, 2, 'Residential', 90.0, 1062, 95600, NULL::numeric),
    (13, 1301, 0, 0, 'Residential', 52.1, 1038, 54100, NULL::numeric),
    (13, 1302, 1, 1, 'Residential', 80.5, 1038, 83600, NULL::numeric),
    (13, 1303, 2, 4, 'Residential', 170.5, 1038, 177000, NULL::numeric),
    (13, 1304, 3, 2, 'Residential', 90.0, 1038, 93400, NULL::numeric),
    (13, 1305, 4, 0, 'Residential', 52.1, 1038, 54100, NULL::numeric),
    (13, 1306, 5, 1, 'Residential', 80.5, 1038, 83600, NULL::numeric),
    (13, 1307, 6, 4, 'Residential', 170.5, 1038, 177000, NULL::numeric),
    (13, 1308, 7, 2, 'Residential', 90.0, 1038, 93400, NULL::numeric),
    (14, 1401, 0, 0, 'Residential', 52.1, 1015, 52900, NULL::numeric),
    (14, 1402, 1, 1, 'Residential', 80.5, 1015, 81700, NULL::numeric),
    (14, 1403, 2, 4, 'Residential', 170.5, 1015, 173100, NULL::numeric),
    (14, 1404, 3, 2, 'Residential', 90.0, 1015, 91400, NULL::numeric),
    (14, 1405, 4, 0, 'Residential', 52.1, 1015, 52900, NULL::numeric),
    (14, 1406, 5, 1, 'Residential', 80.5, 1015, 81700, NULL::numeric),
    (14, 1407, 6, 4, 'Residential', 170.5, 1015, 173100, NULL::numeric),
    (14, 1408, 7, 2, 'Residential', 90.0, 1015, 91400, NULL::numeric),
    (15, 1501, 0, 0, 'Residential', 52.1, 992, 51700, NULL::numeric),
    (15, 1502, 1, 1, 'Residential', 80.5, 992, 79900, NULL::numeric),
    (15, 1503, 2, 4, 'Residential', 170.5, 992, 169100, NULL::numeric),
    (15, 1504, 3, 2, 'Residential', 90.0, 992, 89300, NULL::numeric),
    (15, 1505, 4, 0, 'Residential', 52.1, 992, 51700, NULL::numeric),
    (15, 1506, 5, 1, 'Residential', 80.5, 992, 79900, NULL::numeric),
    (15, 1507, 6, 4, 'Residential', 170.5, 992, 169100, NULL::numeric),
    (15, 1508, 7, 2, 'Residential', 90.0, 992, 89300, NULL::numeric),
    (16, 1601, 0, 0, 'Residential', 52.1, 969, 50500, NULL::numeric),
    (16, 1602, 1, 1, 'Residential', 80.5, 969, 78000, NULL::numeric),
    (16, 1603, 2, 4, 'Residential', 170.5, 969, 165200, NULL::numeric),
    (16, 1604, 3, 2, 'Residential', 90.0, 969, 87200, NULL::numeric),
    (16, 1605, 4, 0, 'Residential', 52.1, 969, 50500, NULL::numeric),
    (16, 1606, 5, 1, 'Residential', 80.5, 969, 78000, NULL::numeric),
    (16, 1607, 6, 4, 'Residential', 170.5, 969, 165200, NULL::numeric),
    (16, 1608, 7, 2, 'Residential', 90.0, 969, 87200, NULL::numeric),
    (17, 1701, 0, 0, 'Residential', 52.1, 946, 49300, NULL::numeric),
    (17, 1702, 1, 1, 'Residential', 80.5, 946, 76200, NULL::numeric),
    (17, 1703, 2, 4, 'Residential', 170.5, 946, 161300, NULL::numeric),
    (17, 1704, 3, 2, 'Residential', 90.0, 946, 85100, NULL::numeric),
    (17, 1705, 4, 0, 'Residential', 52.1, 946, 49300, NULL::numeric),
    (17, 1706, 5, 1, 'Residential', 80.5, 946, 76200, NULL::numeric),
    (17, 1707, 6, 4, 'Residential', 170.5, 946, 161300, NULL::numeric),
    (17, 1708, 7, 2, 'Residential', 90.0, 946, 85100, NULL::numeric),
    (18, 1801, 0, 0, 'Residential', 52.1, 923, 48100, NULL::numeric),
    (18, 1802, 1, 1, 'Residential', 80.5, 923, 74300, NULL::numeric),
    (18, 1803, 2, 4, 'Residential', 170.5, 923, 157400, NULL::numeric),
    (18, 1804, 3, 2, 'Residential', 90.0, 923, 83100, NULL::numeric),
    (18, 1805, 4, 0, 'Residential', 52.1, 923, 48100, NULL::numeric),
    (18, 1806, 5, 1, 'Residential', 80.5, 923, 74300, NULL::numeric),
    (18, 1807, 6, 4, 'Residential', 170.5, 923, 157400, NULL::numeric),
    (18, 1808, 7, 2, 'Residential', 90.0, 923, 83100, NULL::numeric),
    (19, 1901, 0, 0, 'Residential', 52.1, 900, 46900, NULL::numeric),
    (19, 1902, 1, 1, 'Residential', 80.5, 900, 72500, NULL::numeric),
    (19, 1903, 2, 4, 'Residential', 170.5, 900, 153500, NULL::numeric),
    (19, 1904, 3, 2, 'Residential', 90.0, 900, 81000, NULL::numeric),
    (19, 1905, 4, 0, 'Residential', 52.1, 900, 46900, NULL::numeric),
    (19, 1906, 5, 1, 'Residential', 80.5, 900, 72500, NULL::numeric),
    (19, 1907, 6, 4, 'Residential', 170.5, 900, 153500, NULL::numeric),
    (19, 1908, 7, 2, 'Residential', 90.0, 900, 81000, NULL::numeric),
    (20, 2001, 0, 4, 'Penthouse', 184, 1400, 257600, 39.8::numeric),
    (20, 2002, 1, 4, 'Penthouse', 184, 1400, 257600, 39.8::numeric),
    (20, 2003, 2, 4, 'Penthouse', 184, 1400, 257600, 39.8::numeric),
    (20, 2004, 3, 4, 'Penthouse', 184, 1400, 257600, 39.8::numeric)
  ) AS v(floor, num, slot, rooms, kind, area, ppm, price, terrace)
  JOIN "Floors" f ON f."BuildingId" = b AND f."Number" = v.floor;
  INSERT INTO "AuditLog" ("Id", "UserId", "OrganizationId", "ImpersonatedById", "Entity", "EntityId", "Action", "Before", "After",
                          "At", "Ip", "CreatedAt", "UpdatedAt")
  VALUES (gen_random_uuid(), NULL, org, NULL, 'Building', b, 'Seeded', NULL,
          jsonb_build_object('source', 'scripts/seed.sql', 'blockCode', 'B', 'concept', true, 'apartments', 208), ts, NULL, ts, ts);
  RAISE NOTICE 'Создан блок B: % помещений', 208;

  -- Блок C: 20 этажей, −2/−1 паркинг, 1–2 магазины, последний — пентхаусы.
  b := gen_random_uuid();
  INSERT INTO "Buildings" ("Id", "OrganizationId", "NameRu", "NameTg", "NameEn", "Address", "FloorsCount", "DescriptionRu",
    "DescriptionTg", "DescriptionEn", "ModelUrl", "CompletionYear", "PlannedApartments", "Amenities", "BlockCode", "SortOrder",
    "Footprint", "SitePosition", "Setbacks", "PriceSteps", "PenthousePricePerM2", "PriceCurrency", "CommercialPricePerM2",
    "ParkingSpotPrice", "ElevatorsCount", "IsPublished", "CreatedAt", "UpdatedAt")
  VALUES (b, org, 'Блок C', 'Блоки C', 'Block C', 'г. Душанбе, пр. Борбад, 15', 20,
    'Концепт: форма, расположение и планировки блока — визуализация до проекта застройщика, не проектная документация.',
    'Консепсия: шакл, ҷойгиршавӣ ва нақшаҳои блок — визуализатсия то лоиҳаи сохтмончӣ, ҳуҷҷати лоиҳавӣ нест.',
    'Concept: the block''s shape, placement and layouts are a visualisation before the developer''s project, not design documentation.',
    NULL, NULL, 140, ARRAY['underground_parking', 'closed_yard', 'playground', 'football_field', 'elevators', 'landscaping', 'commercial']::text[], 'C', 3,
    '[[21,0],[42,0,8],[42,28],[27,28],[27,15],[15,15],[15,28],[0,28],[0,0,8]]'::jsonb,
    '{"x":81,"y":-55,"rotationDeg":90}'::jsonb,
    '[{"fromFloor":20,"footprint":[[21,1.5],[40.5,1.5,7],[40.5,28],[27,28],[27,15],[15,15],[15,28],[1.5,28],[1.5,1.5,7]],"planDepth":6.5}]'::jsonb,
    '[{"fromFloor":3,"toFloor":5,"pricePerM2":1200,"pricePerM2To":null},{"fromFloor":6,"toFloor":19,"pricePerM2":1200,"pricePerM2To":900},{"fromFloor":20,"toFloor":20,"pricePerM2":900,"pricePerM2To":null}]'::jsonb,
    1400, 'USD', 1500, 20000, 3, true, ts, ts);
  INSERT INTO "Floors" ("Id", "BuildingId", "Number", "PlanImageUrl", "CreatedAt", "UpdatedAt")
  SELECT gen_random_uuid(), b, n, NULL, ts, ts FROM unnest(ARRAY[-2, -1, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20]) AS n;
  INSERT INTO "Apartments" ("Id", "FloorId", "Number", "PlanSlot", "Model3dId", "Rooms", "Area", "PricePerM2", "Price", "Currency",
    "Status", "Kind", "TerraceArea", "CreatedAt", "UpdatedAt")
  SELECT gen_random_uuid(), f."Id", v.num, v.slot, NULL, v.rooms, v.area, v.ppm, v.price, 'USD', 'Free', v.kind, v.terrace, ts, ts
  FROM (VALUES
    (-2, 201, 0, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 202, 1, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 203, 2, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 204, 3, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 205, 4, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 206, 5, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 207, 6, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 208, 7, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 209, 8, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 210, 9, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 211, 10, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 212, 11, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 213, 12, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 214, 13, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 215, 14, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 216, 15, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 217, 16, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 218, 17, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 219, 18, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 220, 19, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 221, 20, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 222, 21, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 223, 22, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 224, 23, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 225, 24, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 226, 25, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 227, 26, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-2, 228, 27, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 101, 0, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 102, 1, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 103, 2, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 104, 3, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 105, 4, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 106, 5, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 107, 6, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 108, 7, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 109, 8, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 110, 9, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 111, 10, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 112, 11, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 113, 12, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 114, 13, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 115, 14, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 116, 15, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 117, 16, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 118, 17, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 119, 18, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 120, 19, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 121, 20, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 122, 21, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 123, 22, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 124, 23, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 125, 24, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 126, 25, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 127, 26, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (-1, 128, 27, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric),
    (1, 101, 0, 0, 'Commercial', 193.2, 1500, 289800, NULL::numeric),
    (1, 102, 1, 0, 'Commercial', 128.8, 1500, 193200, NULL::numeric),
    (1, 103, 2, 0, 'Commercial', 161, 1500, 241500, NULL::numeric),
    (1, 104, 3, 0, 'Commercial', 193.2, 1500, 289800, NULL::numeric),
    (1, 105, 4, 0, 'Commercial', 128.8, 1500, 193200, NULL::numeric),
    (1, 106, 5, 0, 'Commercial', 161, 1500, 241500, NULL::numeric),
    (2, 201, 0, 0, 'Commercial', 193.2, 1500, 289800, NULL::numeric),
    (2, 202, 1, 0, 'Commercial', 128.8, 1500, 193200, NULL::numeric),
    (2, 203, 2, 0, 'Commercial', 161, 1500, 241500, NULL::numeric),
    (2, 204, 3, 0, 'Commercial', 193.2, 1500, 289800, NULL::numeric),
    (2, 205, 4, 0, 'Commercial', 128.8, 1500, 193200, NULL::numeric),
    (2, 206, 5, 0, 'Commercial', 161, 1500, 241500, NULL::numeric),
    (3, 301, 0, 0, 'Residential', 67.3, 1200, 80800, NULL::numeric),
    (3, 302, 1, 4, 'Residential', 195.6, 1200, 234700, NULL::numeric),
    (3, 303, 2, 1, 'Residential', 97.8, 1200, 117400, NULL::numeric),
    (3, 304, 3, 2, 'Residential', 122.3, 1200, 146800, NULL::numeric),
    (3, 305, 4, 2, 'Residential', 122.3, 1200, 146800, NULL::numeric),
    (3, 306, 5, 1, 'Residential', 97.8, 1200, 117400, NULL::numeric),
    (3, 307, 6, 4, 'Residential', 195.6, 1200, 234700, NULL::numeric),
    (3, 308, 7, 0, 'Residential', 67.3, 1200, 80800, NULL::numeric),
    (4, 401, 0, 0, 'Residential', 67.3, 1200, 80800, NULL::numeric),
    (4, 402, 1, 4, 'Residential', 195.6, 1200, 234700, NULL::numeric),
    (4, 403, 2, 1, 'Residential', 97.8, 1200, 117400, NULL::numeric),
    (4, 404, 3, 2, 'Residential', 122.3, 1200, 146800, NULL::numeric),
    (4, 405, 4, 2, 'Residential', 122.3, 1200, 146800, NULL::numeric),
    (4, 406, 5, 1, 'Residential', 97.8, 1200, 117400, NULL::numeric),
    (4, 407, 6, 4, 'Residential', 195.6, 1200, 234700, NULL::numeric),
    (4, 408, 7, 0, 'Residential', 67.3, 1200, 80800, NULL::numeric),
    (5, 501, 0, 0, 'Residential', 67.3, 1200, 80800, NULL::numeric),
    (5, 502, 1, 4, 'Residential', 195.6, 1200, 234700, NULL::numeric),
    (5, 503, 2, 1, 'Residential', 97.8, 1200, 117400, NULL::numeric),
    (5, 504, 3, 2, 'Residential', 122.3, 1200, 146800, NULL::numeric),
    (5, 505, 4, 2, 'Residential', 122.3, 1200, 146800, NULL::numeric),
    (5, 506, 5, 1, 'Residential', 97.8, 1200, 117400, NULL::numeric),
    (5, 507, 6, 4, 'Residential', 195.6, 1200, 234700, NULL::numeric),
    (5, 508, 7, 0, 'Residential', 67.3, 1200, 80800, NULL::numeric),
    (6, 601, 0, 0, 'Residential', 67.3, 1200, 80800, NULL::numeric),
    (6, 602, 1, 4, 'Residential', 195.6, 1200, 234700, NULL::numeric),
    (6, 603, 2, 1, 'Residential', 97.8, 1200, 117400, NULL::numeric),
    (6, 604, 3, 2, 'Residential', 122.3, 1200, 146800, NULL::numeric),
    (6, 605, 4, 2, 'Residential', 122.3, 1200, 146800, NULL::numeric),
    (6, 606, 5, 1, 'Residential', 97.8, 1200, 117400, NULL::numeric),
    (6, 607, 6, 4, 'Residential', 195.6, 1200, 234700, NULL::numeric),
    (6, 608, 7, 0, 'Residential', 67.3, 1200, 80800, NULL::numeric),
    (7, 701, 0, 0, 'Residential', 67.3, 1177, 79200, NULL::numeric),
    (7, 702, 1, 4, 'Residential', 195.6, 1177, 230200, NULL::numeric),
    (7, 703, 2, 1, 'Residential', 97.8, 1177, 115100, NULL::numeric),
    (7, 704, 3, 2, 'Residential', 122.3, 1177, 143900, NULL::numeric),
    (7, 705, 4, 2, 'Residential', 122.3, 1177, 143900, NULL::numeric),
    (7, 706, 5, 1, 'Residential', 97.8, 1177, 115100, NULL::numeric),
    (7, 707, 6, 4, 'Residential', 195.6, 1177, 230200, NULL::numeric),
    (7, 708, 7, 0, 'Residential', 67.3, 1177, 79200, NULL::numeric),
    (8, 801, 0, 0, 'Residential', 67.3, 1154, 77700, NULL::numeric),
    (8, 802, 1, 4, 'Residential', 195.6, 1154, 225700, NULL::numeric),
    (8, 803, 2, 1, 'Residential', 97.8, 1154, 112900, NULL::numeric),
    (8, 804, 3, 2, 'Residential', 122.3, 1154, 141100, NULL::numeric),
    (8, 805, 4, 2, 'Residential', 122.3, 1154, 141100, NULL::numeric),
    (8, 806, 5, 1, 'Residential', 97.8, 1154, 112900, NULL::numeric),
    (8, 807, 6, 4, 'Residential', 195.6, 1154, 225700, NULL::numeric),
    (8, 808, 7, 0, 'Residential', 67.3, 1154, 77700, NULL::numeric),
    (9, 901, 0, 0, 'Residential', 67.3, 1131, 76100, NULL::numeric),
    (9, 902, 1, 4, 'Residential', 195.6, 1131, 221200, NULL::numeric),
    (9, 903, 2, 1, 'Residential', 97.8, 1131, 110600, NULL::numeric),
    (9, 904, 3, 2, 'Residential', 122.3, 1131, 138300, NULL::numeric),
    (9, 905, 4, 2, 'Residential', 122.3, 1131, 138300, NULL::numeric),
    (9, 906, 5, 1, 'Residential', 97.8, 1131, 110600, NULL::numeric),
    (9, 907, 6, 4, 'Residential', 195.6, 1131, 221200, NULL::numeric),
    (9, 908, 7, 0, 'Residential', 67.3, 1131, 76100, NULL::numeric),
    (10, 1001, 0, 0, 'Residential', 67.3, 1108, 74600, NULL::numeric),
    (10, 1002, 1, 4, 'Residential', 195.6, 1108, 216700, NULL::numeric),
    (10, 1003, 2, 1, 'Residential', 97.8, 1108, 108400, NULL::numeric),
    (10, 1004, 3, 2, 'Residential', 122.3, 1108, 135500, NULL::numeric),
    (10, 1005, 4, 2, 'Residential', 122.3, 1108, 135500, NULL::numeric),
    (10, 1006, 5, 1, 'Residential', 97.8, 1108, 108400, NULL::numeric),
    (10, 1007, 6, 4, 'Residential', 195.6, 1108, 216700, NULL::numeric),
    (10, 1008, 7, 0, 'Residential', 67.3, 1108, 74600, NULL::numeric),
    (11, 1101, 0, 0, 'Residential', 67.3, 1085, 73000, NULL::numeric),
    (11, 1102, 1, 4, 'Residential', 195.6, 1085, 212200, NULL::numeric),
    (11, 1103, 2, 1, 'Residential', 97.8, 1085, 106100, NULL::numeric),
    (11, 1104, 3, 2, 'Residential', 122.3, 1085, 132700, NULL::numeric),
    (11, 1105, 4, 2, 'Residential', 122.3, 1085, 132700, NULL::numeric),
    (11, 1106, 5, 1, 'Residential', 97.8, 1085, 106100, NULL::numeric),
    (11, 1107, 6, 4, 'Residential', 195.6, 1085, 212200, NULL::numeric),
    (11, 1108, 7, 0, 'Residential', 67.3, 1085, 73000, NULL::numeric),
    (12, 1201, 0, 0, 'Residential', 67.3, 1062, 71500, NULL::numeric),
    (12, 1202, 1, 4, 'Residential', 195.6, 1062, 207700, NULL::numeric),
    (12, 1203, 2, 1, 'Residential', 97.8, 1062, 103900, NULL::numeric),
    (12, 1204, 3, 2, 'Residential', 122.3, 1062, 129900, NULL::numeric),
    (12, 1205, 4, 2, 'Residential', 122.3, 1062, 129900, NULL::numeric),
    (12, 1206, 5, 1, 'Residential', 97.8, 1062, 103900, NULL::numeric),
    (12, 1207, 6, 4, 'Residential', 195.6, 1062, 207700, NULL::numeric),
    (12, 1208, 7, 0, 'Residential', 67.3, 1062, 71500, NULL::numeric),
    (13, 1301, 0, 0, 'Residential', 67.3, 1038, 69900, NULL::numeric),
    (13, 1302, 1, 4, 'Residential', 195.6, 1038, 203000, NULL::numeric),
    (13, 1303, 2, 1, 'Residential', 97.8, 1038, 101500, NULL::numeric),
    (13, 1304, 3, 2, 'Residential', 122.3, 1038, 126900, NULL::numeric),
    (13, 1305, 4, 2, 'Residential', 122.3, 1038, 126900, NULL::numeric),
    (13, 1306, 5, 1, 'Residential', 97.8, 1038, 101500, NULL::numeric),
    (13, 1307, 6, 4, 'Residential', 195.6, 1038, 203000, NULL::numeric),
    (13, 1308, 7, 0, 'Residential', 67.3, 1038, 69900, NULL::numeric),
    (14, 1401, 0, 0, 'Residential', 67.3, 1015, 68300, NULL::numeric),
    (14, 1402, 1, 4, 'Residential', 195.6, 1015, 198500, NULL::numeric),
    (14, 1403, 2, 1, 'Residential', 97.8, 1015, 99300, NULL::numeric),
    (14, 1404, 3, 2, 'Residential', 122.3, 1015, 124100, NULL::numeric),
    (14, 1405, 4, 2, 'Residential', 122.3, 1015, 124100, NULL::numeric),
    (14, 1406, 5, 1, 'Residential', 97.8, 1015, 99300, NULL::numeric),
    (14, 1407, 6, 4, 'Residential', 195.6, 1015, 198500, NULL::numeric),
    (14, 1408, 7, 0, 'Residential', 67.3, 1015, 68300, NULL::numeric),
    (15, 1501, 0, 0, 'Residential', 67.3, 992, 66800, NULL::numeric),
    (15, 1502, 1, 4, 'Residential', 195.6, 992, 194000, NULL::numeric),
    (15, 1503, 2, 1, 'Residential', 97.8, 992, 97000, NULL::numeric),
    (15, 1504, 3, 2, 'Residential', 122.3, 992, 121300, NULL::numeric),
    (15, 1505, 4, 2, 'Residential', 122.3, 992, 121300, NULL::numeric),
    (15, 1506, 5, 1, 'Residential', 97.8, 992, 97000, NULL::numeric),
    (15, 1507, 6, 4, 'Residential', 195.6, 992, 194000, NULL::numeric),
    (15, 1508, 7, 0, 'Residential', 67.3, 992, 66800, NULL::numeric),
    (16, 1601, 0, 0, 'Residential', 67.3, 969, 65200, NULL::numeric),
    (16, 1602, 1, 4, 'Residential', 195.6, 969, 189500, NULL::numeric),
    (16, 1603, 2, 1, 'Residential', 97.8, 969, 94800, NULL::numeric),
    (16, 1604, 3, 2, 'Residential', 122.3, 969, 118500, NULL::numeric),
    (16, 1605, 4, 2, 'Residential', 122.3, 969, 118500, NULL::numeric),
    (16, 1606, 5, 1, 'Residential', 97.8, 969, 94800, NULL::numeric),
    (16, 1607, 6, 4, 'Residential', 195.6, 969, 189500, NULL::numeric),
    (16, 1608, 7, 0, 'Residential', 67.3, 969, 65200, NULL::numeric),
    (17, 1701, 0, 0, 'Residential', 67.3, 946, 63700, NULL::numeric),
    (17, 1702, 1, 4, 'Residential', 195.6, 946, 185000, NULL::numeric),
    (17, 1703, 2, 1, 'Residential', 97.8, 946, 92500, NULL::numeric),
    (17, 1704, 3, 2, 'Residential', 122.3, 946, 115700, NULL::numeric),
    (17, 1705, 4, 2, 'Residential', 122.3, 946, 115700, NULL::numeric),
    (17, 1706, 5, 1, 'Residential', 97.8, 946, 92500, NULL::numeric),
    (17, 1707, 6, 4, 'Residential', 195.6, 946, 185000, NULL::numeric),
    (17, 1708, 7, 0, 'Residential', 67.3, 946, 63700, NULL::numeric),
    (18, 1801, 0, 0, 'Residential', 67.3, 923, 62100, NULL::numeric),
    (18, 1802, 1, 4, 'Residential', 195.6, 923, 180500, NULL::numeric),
    (18, 1803, 2, 1, 'Residential', 97.8, 923, 90300, NULL::numeric),
    (18, 1804, 3, 2, 'Residential', 122.3, 923, 112900, NULL::numeric),
    (18, 1805, 4, 2, 'Residential', 122.3, 923, 112900, NULL::numeric),
    (18, 1806, 5, 1, 'Residential', 97.8, 923, 90300, NULL::numeric),
    (18, 1807, 6, 4, 'Residential', 195.6, 923, 180500, NULL::numeric),
    (18, 1808, 7, 0, 'Residential', 67.3, 923, 62100, NULL::numeric),
    (19, 1901, 0, 0, 'Residential', 67.3, 900, 60600, NULL::numeric),
    (19, 1902, 1, 4, 'Residential', 195.6, 900, 176000, NULL::numeric),
    (19, 1903, 2, 1, 'Residential', 97.8, 900, 88000, NULL::numeric),
    (19, 1904, 3, 2, 'Residential', 122.3, 900, 110100, NULL::numeric),
    (19, 1905, 4, 2, 'Residential', 122.3, 900, 110100, NULL::numeric),
    (19, 1906, 5, 1, 'Residential', 97.8, 900, 88000, NULL::numeric),
    (19, 1907, 6, 4, 'Residential', 195.6, 900, 176000, NULL::numeric),
    (19, 1908, 7, 0, 'Residential', 67.3, 900, 60600, NULL::numeric),
    (20, 2001, 0, 4, 'Penthouse', 206.5, 1400, 289100, 32.9::numeric),
    (20, 2002, 1, 4, 'Penthouse', 219.3, 1400, 307000, 35.0::numeric),
    (20, 2003, 2, 4, 'Penthouse', 219.3, 1400, 307000, 35.0::numeric),
    (20, 2004, 3, 4, 'Penthouse', 206.5, 1400, 289100, 32.9::numeric)
  ) AS v(floor, num, slot, rooms, kind, area, ppm, price, terrace)
  JOIN "Floors" f ON f."BuildingId" = b AND f."Number" = v.floor;
  INSERT INTO "AuditLog" ("Id", "UserId", "OrganizationId", "ImpersonatedById", "Entity", "EntityId", "Action", "Before", "After",
                          "At", "Ip", "CreatedAt", "UpdatedAt")
  VALUES (gen_random_uuid(), NULL, org, NULL, 'Building', b, 'Seeded', NULL,
          jsonb_build_object('source', 'scripts/seed.sql', 'blockCode', 'C', 'concept', true, 'apartments', 208), ts, NULL, ts, ts);
  RAISE NOTICE 'Создан блок C: % помещений', 208;

  UPDATE "Organizations" SET "SitePlan" = '{"boundary":[[-100,-75],[105,-75],[105,22],[-100,22]],"zones":[{"kind":"football_field","polygon":[[-6,-30],[36,-30],[36,-8],[-6,-8]]},{"kind":"playground","polygon":[[-42,-32],[-24,-32],[-24,-18],[-42,-18]]},{"kind":"lawn","polygon":[[-46,-36],[48,-36],[48,6],[-46,6]]},{"kind":"path","polygon":[[-46,8],[48,8],[48,12],[-46,12]]},{"kind":"parking_ramp","polygon":[[-12,14],[2,14],[2,22],[-12,22]]}]}'::jsonb, "UpdatedAt" = ts
  WHERE "Id" = org AND "SitePlan" IS NULL;

  -- Прежние дома без буквы блока — с сайта убираются (не удаляются: сделки, графики и платежи остаются как есть).
  FOR old IN SELECT "Id", "NameRu" FROM "Buildings" WHERE "OrganizationId" = org AND "BlockCode" IS NULL AND "IsPublished" LOOP
    UPDATE "Buildings" SET "IsPublished" = false, "UpdatedAt" = ts WHERE "Id" = old."Id";
    INSERT INTO "AuditLog" ("Id", "UserId", "OrganizationId", "ImpersonatedById", "Entity", "EntityId", "Action", "Before", "After",
                            "At", "Ip", "CreatedAt", "UpdatedAt")
    VALUES (gen_random_uuid(), NULL, org, NULL, 'Building', old."Id", 'Unpublished', jsonb_build_object('IsPublished', true),
            jsonb_build_object('IsPublished', false, 'reason', 'ЖК из блоков A/B/C', 'source', 'scripts/seed.sql'), ts, NULL, ts, ts);
    RAISE NOTICE 'Дом «%» скрыт с сайта (сделки и платежи не тронуты)', old."NameRu";
  END LOOP;
END $$;
-- END generated: Rich Tower blocks
