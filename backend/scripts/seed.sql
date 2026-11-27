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
      INSERT INTO "Apartments" ("Id", "FloorId", "BuildingId", "Number", "PlanSlot", "Model3dId", "Rooms", "Area", "PricePerM2", "Price",
                                "Currency", "Status", "CreatedAt", "UpdatedAt")
      VALUES (apt_id, floor_id, building, apt_number, slot, NULL, rooms, area, ppm, ROUND(area * ppm / 100) * 100,
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
  INSERT INTO "Apartments" ("Id", "FloorId", "BuildingId", "Number", "PlanSlot", "Model3dId", "Rooms", "Area", "PricePerM2", "Price", "Currency",
    "Status", "Kind", "TerraceArea", "LayoutCode", "CreatedAt", "UpdatedAt")
  SELECT gen_random_uuid(), f."Id", b, v.num, v.slot, NULL, v.rooms, v.area, v.ppm, v.price, 'USD', 'Free', v.kind, v.terrace, v.layout, ts, ts
  FROM (VALUES
    (-2, 29, 0, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 30, 1, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 31, 2, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 32, 3, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 33, 4, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 34, 5, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 35, 6, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 36, 7, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 37, 8, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 38, 9, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 39, 10, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 40, 11, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 41, 12, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 42, 13, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 43, 14, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 44, 15, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 45, 16, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 46, 17, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 47, 18, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 48, 19, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 49, 20, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 50, 21, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 51, 22, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 52, 23, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 53, 24, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 54, 25, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 55, 26, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 56, 27, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 1, 0, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 2, 1, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 3, 2, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 4, 3, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 5, 4, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 6, 5, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 7, 6, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 8, 7, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 9, 8, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 10, 9, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 11, 10, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 12, 11, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 13, 12, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 14, 13, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 15, 14, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 16, 15, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 17, 16, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 18, 17, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 19, 18, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 20, 19, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 21, 20, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 22, 21, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 23, 22, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 24, 23, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 25, 24, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 26, 25, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 27, 26, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 28, 27, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (1, 1, 0, 0, 'Commercial', 162.4, 1500, 243600, NULL::numeric, NULL::text),
    (1, 2, 1, 0, 'Commercial', 108.3, 1500, 162500, NULL::numeric, NULL::text),
    (1, 3, 2, 0, 'Commercial', 135.3, 1500, 203000, NULL::numeric, NULL::text),
    (1, 4, 3, 0, 'Commercial', 162.4, 1500, 243600, NULL::numeric, NULL::text),
    (1, 5, 4, 0, 'Commercial', 108.3, 1500, 162500, NULL::numeric, NULL::text),
    (1, 6, 5, 0, 'Commercial', 135.3, 1500, 203000, NULL::numeric, NULL::text),
    (2, 7, 0, 0, 'Commercial', 162.4, 1500, 243600, NULL::numeric, NULL::text),
    (2, 8, 1, 0, 'Commercial', 108.3, 1500, 162500, NULL::numeric, NULL::text),
    (2, 9, 2, 0, 'Commercial', 135.3, 1500, 203000, NULL::numeric, NULL::text),
    (2, 10, 3, 0, 'Commercial', 162.4, 1500, 243600, NULL::numeric, NULL::text),
    (2, 11, 4, 0, 'Commercial', 108.3, 1500, 162500, NULL::numeric, NULL::text),
    (2, 12, 5, 0, 'Commercial', 135.3, 1500, 203000, NULL::numeric, NULL::text),
    (3, 5, 0, 0, 'Residential', 54.1, 1200, 64900, NULL::numeric, NULL::text),
    (3, 6, 1, 4, 'Residential', 157.9, 1200, 189500, NULL::numeric, NULL::text),
    (3, 7, 2, 1, 'Residential', 72.2, 1200, 86600, NULL::numeric, NULL::text),
    (3, 8, 3, 2, 'Residential', 90.2, 1200, 108200, NULL::numeric, NULL::text),
    (3, 1, 4, 4, 'Residential', 157.9, 1200, 189500, NULL::numeric, NULL::text),
    (3, 2, 5, 0, 'Residential', 54.1, 1200, 64900, NULL::numeric, NULL::text),
    (3, 3, 6, 1, 'Residential', 72.2, 1200, 86600, NULL::numeric, NULL::text),
    (3, 4, 7, 4, 'Residential', 153.4, 1200, 184100, NULL::numeric, 'corner-l'::text),
    (4, 13, 0, 0, 'Residential', 54.1, 1200, 64900, NULL::numeric, NULL::text),
    (4, 14, 1, 4, 'Residential', 157.9, 1200, 189500, NULL::numeric, NULL::text),
    (4, 15, 2, 1, 'Residential', 72.2, 1200, 86600, NULL::numeric, NULL::text),
    (4, 16, 3, 2, 'Residential', 90.2, 1200, 108200, NULL::numeric, NULL::text),
    (4, 9, 4, 4, 'Residential', 157.9, 1200, 189500, NULL::numeric, NULL::text),
    (4, 10, 5, 0, 'Residential', 54.1, 1200, 64900, NULL::numeric, NULL::text),
    (4, 11, 6, 1, 'Residential', 72.2, 1200, 86600, NULL::numeric, NULL::text),
    (4, 12, 7, 4, 'Residential', 153.4, 1200, 184100, NULL::numeric, 'corner-l'::text),
    (5, 21, 0, 0, 'Residential', 54.1, 1200, 64900, NULL::numeric, NULL::text),
    (5, 22, 1, 4, 'Residential', 157.9, 1200, 189500, NULL::numeric, NULL::text),
    (5, 23, 2, 1, 'Residential', 72.2, 1200, 86600, NULL::numeric, NULL::text),
    (5, 24, 3, 2, 'Residential', 90.2, 1200, 108200, NULL::numeric, NULL::text),
    (5, 17, 4, 4, 'Residential', 157.9, 1200, 189500, NULL::numeric, NULL::text),
    (5, 18, 5, 0, 'Residential', 54.1, 1200, 64900, NULL::numeric, NULL::text),
    (5, 19, 6, 1, 'Residential', 72.2, 1200, 86600, NULL::numeric, NULL::text),
    (5, 20, 7, 4, 'Residential', 153.4, 1200, 184100, NULL::numeric, 'corner-l'::text),
    (6, 29, 0, 0, 'Residential', 54.1, 1200, 64900, NULL::numeric, NULL::text),
    (6, 30, 1, 4, 'Residential', 157.9, 1200, 189500, NULL::numeric, NULL::text),
    (6, 31, 2, 1, 'Residential', 72.2, 1200, 86600, NULL::numeric, NULL::text),
    (6, 32, 3, 2, 'Residential', 90.2, 1200, 108200, NULL::numeric, NULL::text),
    (6, 25, 4, 4, 'Residential', 157.9, 1200, 189500, NULL::numeric, NULL::text),
    (6, 26, 5, 0, 'Residential', 54.1, 1200, 64900, NULL::numeric, NULL::text),
    (6, 27, 6, 1, 'Residential', 72.2, 1200, 86600, NULL::numeric, NULL::text),
    (6, 28, 7, 4, 'Residential', 153.4, 1200, 184100, NULL::numeric, 'corner-l'::text),
    (7, 37, 0, 0, 'Residential', 54.1, 1177, 63700, NULL::numeric, NULL::text),
    (7, 38, 1, 4, 'Residential', 157.9, 1177, 185800, NULL::numeric, NULL::text),
    (7, 39, 2, 1, 'Residential', 72.2, 1177, 85000, NULL::numeric, NULL::text),
    (7, 40, 3, 2, 'Residential', 90.2, 1177, 106200, NULL::numeric, NULL::text),
    (7, 33, 4, 4, 'Residential', 157.9, 1177, 185800, NULL::numeric, NULL::text),
    (7, 34, 5, 0, 'Residential', 54.1, 1177, 63700, NULL::numeric, NULL::text),
    (7, 35, 6, 1, 'Residential', 72.2, 1177, 85000, NULL::numeric, NULL::text),
    (7, 36, 7, 4, 'Residential', 153.4, 1177, 180600, NULL::numeric, 'corner-l'::text),
    (8, 45, 0, 0, 'Residential', 54.1, 1154, 62400, NULL::numeric, NULL::text),
    (8, 46, 1, 4, 'Residential', 157.9, 1154, 182200, NULL::numeric, NULL::text),
    (8, 47, 2, 1, 'Residential', 72.2, 1154, 83300, NULL::numeric, NULL::text),
    (8, 48, 3, 2, 'Residential', 90.2, 1154, 104100, NULL::numeric, NULL::text),
    (8, 41, 4, 4, 'Residential', 157.9, 1154, 182200, NULL::numeric, NULL::text),
    (8, 42, 5, 0, 'Residential', 54.1, 1154, 62400, NULL::numeric, NULL::text),
    (8, 43, 6, 1, 'Residential', 72.2, 1154, 83300, NULL::numeric, NULL::text),
    (8, 44, 7, 4, 'Residential', 153.4, 1154, 177000, NULL::numeric, 'corner-l'::text),
    (9, 53, 0, 0, 'Residential', 54.1, 1131, 61200, NULL::numeric, NULL::text),
    (9, 54, 1, 4, 'Residential', 157.9, 1131, 178600, NULL::numeric, NULL::text),
    (9, 55, 2, 1, 'Residential', 72.2, 1131, 81700, NULL::numeric, NULL::text),
    (9, 56, 3, 2, 'Residential', 90.2, 1131, 102000, NULL::numeric, NULL::text),
    (9, 49, 4, 4, 'Residential', 157.9, 1131, 178600, NULL::numeric, NULL::text),
    (9, 50, 5, 0, 'Residential', 54.1, 1131, 61200, NULL::numeric, NULL::text),
    (9, 51, 6, 1, 'Residential', 72.2, 1131, 81700, NULL::numeric, NULL::text),
    (9, 52, 7, 4, 'Residential', 153.4, 1131, 173500, NULL::numeric, 'corner-l'::text),
    (10, 61, 0, 0, 'Residential', 54.1, 1108, 59900, NULL::numeric, NULL::text),
    (10, 62, 1, 4, 'Residential', 157.9, 1108, 175000, NULL::numeric, NULL::text),
    (10, 63, 2, 1, 'Residential', 72.2, 1108, 80000, NULL::numeric, NULL::text),
    (10, 64, 3, 2, 'Residential', 90.2, 1108, 99900, NULL::numeric, NULL::text),
    (10, 57, 4, 4, 'Residential', 157.9, 1108, 175000, NULL::numeric, NULL::text),
    (10, 58, 5, 0, 'Residential', 54.1, 1108, 59900, NULL::numeric, NULL::text),
    (10, 59, 6, 1, 'Residential', 72.2, 1108, 80000, NULL::numeric, NULL::text),
    (10, 60, 7, 4, 'Residential', 153.4, 1108, 170000, NULL::numeric, 'corner-l'::text),
    (11, 69, 0, 0, 'Residential', 54.1, 1085, 58700, NULL::numeric, NULL::text),
    (11, 70, 1, 4, 'Residential', 157.9, 1085, 171300, NULL::numeric, NULL::text),
    (11, 71, 2, 1, 'Residential', 72.2, 1085, 78300, NULL::numeric, NULL::text),
    (11, 72, 3, 2, 'Residential', 90.2, 1085, 97900, NULL::numeric, NULL::text),
    (11, 65, 4, 4, 'Residential', 157.9, 1085, 171300, NULL::numeric, NULL::text),
    (11, 66, 5, 0, 'Residential', 54.1, 1085, 58700, NULL::numeric, NULL::text),
    (11, 67, 6, 1, 'Residential', 72.2, 1085, 78300, NULL::numeric, NULL::text),
    (11, 68, 7, 4, 'Residential', 153.4, 1085, 166400, NULL::numeric, 'corner-l'::text),
    (12, 77, 0, 0, 'Residential', 54.1, 1062, 57500, NULL::numeric, NULL::text),
    (12, 78, 1, 4, 'Residential', 157.9, 1062, 167700, NULL::numeric, NULL::text),
    (12, 79, 2, 1, 'Residential', 72.2, 1062, 76700, NULL::numeric, NULL::text),
    (12, 80, 3, 2, 'Residential', 90.2, 1062, 95800, NULL::numeric, NULL::text),
    (12, 73, 4, 4, 'Residential', 157.9, 1062, 167700, NULL::numeric, NULL::text),
    (12, 74, 5, 0, 'Residential', 54.1, 1062, 57500, NULL::numeric, NULL::text),
    (12, 75, 6, 1, 'Residential', 72.2, 1062, 76700, NULL::numeric, NULL::text),
    (12, 76, 7, 4, 'Residential', 153.4, 1062, 162900, NULL::numeric, 'corner-l'::text),
    (13, 85, 0, 0, 'Residential', 54.1, 1038, 56200, NULL::numeric, NULL::text),
    (13, 86, 1, 4, 'Residential', 157.9, 1038, 163900, NULL::numeric, NULL::text),
    (13, 87, 2, 1, 'Residential', 72.2, 1038, 74900, NULL::numeric, NULL::text),
    (13, 88, 3, 2, 'Residential', 90.2, 1038, 93600, NULL::numeric, NULL::text),
    (13, 81, 4, 4, 'Residential', 157.9, 1038, 163900, NULL::numeric, NULL::text),
    (13, 82, 5, 0, 'Residential', 54.1, 1038, 56200, NULL::numeric, NULL::text),
    (13, 83, 6, 1, 'Residential', 72.2, 1038, 74900, NULL::numeric, NULL::text),
    (13, 84, 7, 4, 'Residential', 153.4, 1038, 159200, NULL::numeric, 'corner-l'::text),
    (14, 93, 0, 0, 'Residential', 54.1, 1015, 54900, NULL::numeric, NULL::text),
    (14, 94, 1, 4, 'Residential', 157.9, 1015, 160300, NULL::numeric, NULL::text),
    (14, 95, 2, 1, 'Residential', 72.2, 1015, 73300, NULL::numeric, NULL::text),
    (14, 96, 3, 2, 'Residential', 90.2, 1015, 91600, NULL::numeric, NULL::text),
    (14, 89, 4, 4, 'Residential', 157.9, 1015, 160300, NULL::numeric, NULL::text),
    (14, 90, 5, 0, 'Residential', 54.1, 1015, 54900, NULL::numeric, NULL::text),
    (14, 91, 6, 1, 'Residential', 72.2, 1015, 73300, NULL::numeric, NULL::text),
    (14, 92, 7, 4, 'Residential', 153.4, 1015, 155700, NULL::numeric, 'corner-l'::text),
    (15, 101, 0, 0, 'Residential', 54.1, 992, 53700, NULL::numeric, NULL::text),
    (15, 102, 1, 4, 'Residential', 157.9, 992, 156600, NULL::numeric, NULL::text),
    (15, 103, 2, 1, 'Residential', 72.2, 992, 71600, NULL::numeric, NULL::text),
    (15, 104, 3, 2, 'Residential', 90.2, 992, 89500, NULL::numeric, NULL::text),
    (15, 97, 4, 4, 'Residential', 157.9, 992, 156600, NULL::numeric, NULL::text),
    (15, 98, 5, 0, 'Residential', 54.1, 992, 53700, NULL::numeric, NULL::text),
    (15, 99, 6, 1, 'Residential', 72.2, 992, 71600, NULL::numeric, NULL::text),
    (15, 100, 7, 4, 'Residential', 153.4, 992, 152200, NULL::numeric, 'corner-l'::text),
    (16, 109, 0, 0, 'Residential', 54.1, 969, 52400, NULL::numeric, NULL::text),
    (16, 110, 1, 4, 'Residential', 157.9, 969, 153000, NULL::numeric, NULL::text),
    (16, 111, 2, 1, 'Residential', 72.2, 969, 70000, NULL::numeric, NULL::text),
    (16, 112, 3, 2, 'Residential', 90.2, 969, 87400, NULL::numeric, NULL::text),
    (16, 105, 4, 4, 'Residential', 157.9, 969, 153000, NULL::numeric, NULL::text),
    (16, 106, 5, 0, 'Residential', 54.1, 969, 52400, NULL::numeric, NULL::text),
    (16, 107, 6, 1, 'Residential', 72.2, 969, 70000, NULL::numeric, NULL::text),
    (16, 108, 7, 4, 'Residential', 153.4, 969, 148600, NULL::numeric, 'corner-l'::text),
    (17, 117, 0, 0, 'Residential', 54.1, 946, 51200, NULL::numeric, NULL::text),
    (17, 118, 1, 4, 'Residential', 157.9, 946, 149400, NULL::numeric, NULL::text),
    (17, 119, 2, 1, 'Residential', 72.2, 946, 68300, NULL::numeric, NULL::text),
    (17, 120, 3, 2, 'Residential', 90.2, 946, 85300, NULL::numeric, NULL::text),
    (17, 113, 4, 4, 'Residential', 157.9, 946, 149400, NULL::numeric, NULL::text),
    (17, 114, 5, 0, 'Residential', 54.1, 946, 51200, NULL::numeric, NULL::text),
    (17, 115, 6, 1, 'Residential', 72.2, 946, 68300, NULL::numeric, NULL::text),
    (17, 116, 7, 4, 'Residential', 153.4, 946, 145100, NULL::numeric, 'corner-l'::text),
    (18, 125, 0, 0, 'Residential', 54.1, 923, 49900, NULL::numeric, NULL::text),
    (18, 126, 1, 4, 'Residential', 157.9, 923, 145700, NULL::numeric, NULL::text),
    (18, 127, 2, 1, 'Residential', 72.2, 923, 66600, NULL::numeric, NULL::text),
    (18, 128, 3, 2, 'Residential', 90.2, 923, 83300, NULL::numeric, NULL::text),
    (18, 121, 4, 4, 'Residential', 157.9, 923, 145700, NULL::numeric, NULL::text),
    (18, 122, 5, 0, 'Residential', 54.1, 923, 49900, NULL::numeric, NULL::text),
    (18, 123, 6, 1, 'Residential', 72.2, 923, 66600, NULL::numeric, NULL::text),
    (18, 124, 7, 4, 'Residential', 153.4, 923, 141600, NULL::numeric, 'corner-l'::text),
    (19, 133, 0, 0, 'Residential', 54.1, 900, 48700, NULL::numeric, NULL::text),
    (19, 134, 1, 4, 'Residential', 157.9, 900, 142100, NULL::numeric, NULL::text),
    (19, 135, 2, 1, 'Residential', 72.2, 900, 65000, NULL::numeric, NULL::text),
    (19, 136, 3, 2, 'Residential', 90.2, 900, 81200, NULL::numeric, NULL::text),
    (19, 129, 4, 4, 'Residential', 157.9, 900, 142100, NULL::numeric, NULL::text),
    (19, 130, 5, 0, 'Residential', 54.1, 900, 48700, NULL::numeric, NULL::text),
    (19, 131, 6, 1, 'Residential', 72.2, 900, 65000, NULL::numeric, NULL::text),
    (19, 132, 7, 4, 'Residential', 153.4, 900, 138100, NULL::numeric, 'corner-l'::text),
    (20, 141, 0, 0, 'Residential', 54.1, 900, 48700, NULL::numeric, NULL::text),
    (20, 142, 1, 4, 'Residential', 157.9, 900, 142100, NULL::numeric, NULL::text),
    (20, 143, 2, 1, 'Residential', 72.2, 900, 65000, NULL::numeric, NULL::text),
    (20, 144, 3, 2, 'Residential', 90.2, 900, 81200, NULL::numeric, NULL::text),
    (20, 137, 4, 4, 'Residential', 157.9, 900, 142100, NULL::numeric, NULL::text),
    (20, 138, 5, 0, 'Residential', 54.1, 900, 48700, NULL::numeric, NULL::text),
    (20, 139, 6, 1, 'Residential', 72.2, 900, 65000, NULL::numeric, NULL::text),
    (20, 140, 7, 4, 'Residential', 153.4, 900, 138100, NULL::numeric, 'corner-l'::text),
    (21, 149, 0, 0, 'Residential', 54.1, 900, 48700, NULL::numeric, NULL::text),
    (21, 150, 1, 4, 'Residential', 157.9, 900, 142100, NULL::numeric, NULL::text),
    (21, 151, 2, 1, 'Residential', 72.2, 900, 65000, NULL::numeric, NULL::text),
    (21, 152, 3, 2, 'Residential', 90.2, 900, 81200, NULL::numeric, NULL::text),
    (21, 145, 4, 4, 'Residential', 157.9, 900, 142100, NULL::numeric, NULL::text),
    (21, 146, 5, 0, 'Residential', 54.1, 900, 48700, NULL::numeric, NULL::text),
    (21, 147, 6, 1, 'Residential', 72.2, 900, 65000, NULL::numeric, NULL::text),
    (21, 148, 7, 4, 'Residential', 153.4, 900, 138100, NULL::numeric, 'corner-l'::text),
    (22, 156, 0, 4, 'Penthouse', 175.5, 1400, 245700, 42.9::numeric, 'ph-4'::text),
    (22, 153, 1, 4, 'Penthouse', 175.5, 1400, 245700, 42.9::numeric, 'ph-4'::text),
    (22, 154, 2, 4, 'Penthouse', 175.5, 1400, 245700, 42.9::numeric, 'ph-4'::text),
    (22, 155, 3, 4, 'Penthouse', 175.5, 1400, 245700, 42.9::numeric, 'ph-4'::text)
  ) AS v(floor, num, slot, rooms, kind, area, ppm, price, terrace, layout)
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
  INSERT INTO "Apartments" ("Id", "FloorId", "BuildingId", "Number", "PlanSlot", "Model3dId", "Rooms", "Area", "PricePerM2", "Price", "Currency",
    "Status", "Kind", "TerraceArea", "LayoutCode", "CreatedAt", "UpdatedAt")
  SELECT gen_random_uuid(), f."Id", b, v.num, v.slot, NULL, v.rooms, v.area, v.ppm, v.price, 'USD', 'Free', v.kind, v.terrace, v.layout, ts, ts
  FROM (VALUES
    (-2, 29, 0, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 30, 1, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 31, 2, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 32, 3, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 33, 4, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 34, 5, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 35, 6, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 36, 7, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 37, 8, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 38, 9, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 39, 10, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 40, 11, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 41, 12, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 42, 13, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 43, 14, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 44, 15, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 45, 16, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 46, 17, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 47, 18, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 48, 19, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 49, 20, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 50, 21, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 51, 22, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 52, 23, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 53, 24, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 54, 25, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 55, 26, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 56, 27, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 1, 0, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 2, 1, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 3, 2, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 4, 3, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 5, 4, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 6, 5, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 7, 6, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 8, 7, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 9, 8, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 10, 9, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 11, 10, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 12, 11, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 13, 12, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 14, 13, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 15, 14, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 16, 15, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 17, 16, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 18, 17, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 19, 18, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 20, 19, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 21, 20, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 22, 21, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 23, 22, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 24, 23, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 25, 24, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 26, 25, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 27, 26, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 28, 27, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (1, 1, 0, 0, 'Commercial', 157.2, 1500, 235800, NULL::numeric, NULL::text),
    (1, 2, 1, 0, 'Commercial', 104.8, 1500, 157200, NULL::numeric, NULL::text),
    (1, 3, 2, 0, 'Commercial', 131.0, 1500, 196500, NULL::numeric, NULL::text),
    (1, 4, 3, 0, 'Commercial', 157.2, 1500, 235800, NULL::numeric, NULL::text),
    (1, 5, 4, 0, 'Commercial', 104.8, 1500, 157200, NULL::numeric, NULL::text),
    (1, 6, 5, 0, 'Commercial', 131.0, 1500, 196500, NULL::numeric, NULL::text),
    (2, 7, 0, 0, 'Commercial', 157.2, 1500, 235800, NULL::numeric, NULL::text),
    (2, 8, 1, 0, 'Commercial', 104.8, 1500, 157200, NULL::numeric, NULL::text),
    (2, 9, 2, 0, 'Commercial', 131.0, 1500, 196500, NULL::numeric, NULL::text),
    (2, 10, 3, 0, 'Commercial', 157.2, 1500, 235800, NULL::numeric, NULL::text),
    (2, 11, 4, 0, 'Commercial', 104.8, 1500, 157200, NULL::numeric, NULL::text),
    (2, 12, 5, 0, 'Commercial', 131.0, 1500, 196500, NULL::numeric, NULL::text),
    (3, 4, 0, 0, 'Residential', 52.1, 1200, 62500, NULL::numeric, NULL::text),
    (3, 5, 1, 1, 'Residential', 80.5, 1200, 96600, NULL::numeric, NULL::text),
    (3, 6, 2, 4, 'Residential', 170.5, 1200, 204600, NULL::numeric, NULL::text),
    (3, 7, 3, 2, 'Residential', 90.0, 1200, 108000, NULL::numeric, 'bay-2'::text),
    (3, 8, 4, 0, 'Residential', 52.1, 1200, 62500, NULL::numeric, NULL::text),
    (3, 1, 5, 1, 'Residential', 80.5, 1200, 96600, NULL::numeric, NULL::text),
    (3, 2, 6, 4, 'Residential', 170.5, 1200, 204600, NULL::numeric, NULL::text),
    (3, 3, 7, 2, 'Residential', 90.0, 1200, 108000, NULL::numeric, 'bay-2'::text),
    (4, 12, 0, 0, 'Residential', 52.1, 1200, 62500, NULL::numeric, NULL::text),
    (4, 13, 1, 1, 'Residential', 80.5, 1200, 96600, NULL::numeric, NULL::text),
    (4, 14, 2, 4, 'Residential', 170.5, 1200, 204600, NULL::numeric, NULL::text),
    (4, 15, 3, 2, 'Residential', 90.0, 1200, 108000, NULL::numeric, 'bay-2'::text),
    (4, 16, 4, 0, 'Residential', 52.1, 1200, 62500, NULL::numeric, NULL::text),
    (4, 9, 5, 1, 'Residential', 80.5, 1200, 96600, NULL::numeric, NULL::text),
    (4, 10, 6, 4, 'Residential', 170.5, 1200, 204600, NULL::numeric, NULL::text),
    (4, 11, 7, 2, 'Residential', 90.0, 1200, 108000, NULL::numeric, 'bay-2'::text),
    (5, 20, 0, 0, 'Residential', 52.1, 1200, 62500, NULL::numeric, NULL::text),
    (5, 21, 1, 1, 'Residential', 80.5, 1200, 96600, NULL::numeric, NULL::text),
    (5, 22, 2, 4, 'Residential', 170.5, 1200, 204600, NULL::numeric, NULL::text),
    (5, 23, 3, 2, 'Residential', 90.0, 1200, 108000, NULL::numeric, 'bay-2'::text),
    (5, 24, 4, 0, 'Residential', 52.1, 1200, 62500, NULL::numeric, NULL::text),
    (5, 17, 5, 1, 'Residential', 80.5, 1200, 96600, NULL::numeric, NULL::text),
    (5, 18, 6, 4, 'Residential', 170.5, 1200, 204600, NULL::numeric, NULL::text),
    (5, 19, 7, 2, 'Residential', 90.0, 1200, 108000, NULL::numeric, 'bay-2'::text),
    (6, 28, 0, 0, 'Residential', 52.1, 1200, 62500, NULL::numeric, NULL::text),
    (6, 29, 1, 1, 'Residential', 80.5, 1200, 96600, NULL::numeric, NULL::text),
    (6, 30, 2, 4, 'Residential', 170.5, 1200, 204600, NULL::numeric, NULL::text),
    (6, 31, 3, 2, 'Residential', 90.0, 1200, 108000, NULL::numeric, 'bay-2'::text),
    (6, 32, 4, 0, 'Residential', 52.1, 1200, 62500, NULL::numeric, NULL::text),
    (6, 25, 5, 1, 'Residential', 80.5, 1200, 96600, NULL::numeric, NULL::text),
    (6, 26, 6, 4, 'Residential', 170.5, 1200, 204600, NULL::numeric, NULL::text),
    (6, 27, 7, 2, 'Residential', 90.0, 1200, 108000, NULL::numeric, 'bay-2'::text),
    (7, 36, 0, 0, 'Residential', 52.1, 1177, 61300, NULL::numeric, NULL::text),
    (7, 37, 1, 1, 'Residential', 80.5, 1177, 94700, NULL::numeric, NULL::text),
    (7, 38, 2, 4, 'Residential', 170.5, 1177, 200700, NULL::numeric, NULL::text),
    (7, 39, 3, 2, 'Residential', 90.0, 1177, 105900, NULL::numeric, 'bay-2'::text),
    (7, 40, 4, 0, 'Residential', 52.1, 1177, 61300, NULL::numeric, NULL::text),
    (7, 33, 5, 1, 'Residential', 80.5, 1177, 94700, NULL::numeric, NULL::text),
    (7, 34, 6, 4, 'Residential', 170.5, 1177, 200700, NULL::numeric, NULL::text),
    (7, 35, 7, 2, 'Residential', 90.0, 1177, 105900, NULL::numeric, 'bay-2'::text),
    (8, 44, 0, 0, 'Residential', 52.1, 1154, 60100, NULL::numeric, NULL::text),
    (8, 45, 1, 1, 'Residential', 80.5, 1154, 92900, NULL::numeric, NULL::text),
    (8, 46, 2, 4, 'Residential', 170.5, 1154, 196800, NULL::numeric, NULL::text),
    (8, 47, 3, 2, 'Residential', 90.0, 1154, 103900, NULL::numeric, 'bay-2'::text),
    (8, 48, 4, 0, 'Residential', 52.1, 1154, 60100, NULL::numeric, NULL::text),
    (8, 41, 5, 1, 'Residential', 80.5, 1154, 92900, NULL::numeric, NULL::text),
    (8, 42, 6, 4, 'Residential', 170.5, 1154, 196800, NULL::numeric, NULL::text),
    (8, 43, 7, 2, 'Residential', 90.0, 1154, 103900, NULL::numeric, 'bay-2'::text),
    (9, 52, 0, 0, 'Residential', 52.1, 1131, 58900, NULL::numeric, NULL::text),
    (9, 53, 1, 1, 'Residential', 80.5, 1131, 91000, NULL::numeric, NULL::text),
    (9, 54, 2, 4, 'Residential', 170.5, 1131, 192800, NULL::numeric, NULL::text),
    (9, 55, 3, 2, 'Residential', 90.0, 1131, 101800, NULL::numeric, 'bay-2'::text),
    (9, 56, 4, 0, 'Residential', 52.1, 1131, 58900, NULL::numeric, NULL::text),
    (9, 49, 5, 1, 'Residential', 80.5, 1131, 91000, NULL::numeric, NULL::text),
    (9, 50, 6, 4, 'Residential', 170.5, 1131, 192800, NULL::numeric, NULL::text),
    (9, 51, 7, 2, 'Residential', 90.0, 1131, 101800, NULL::numeric, 'bay-2'::text),
    (10, 60, 0, 0, 'Residential', 52.1, 1108, 57700, NULL::numeric, NULL::text),
    (10, 61, 1, 1, 'Residential', 80.5, 1108, 89200, NULL::numeric, NULL::text),
    (10, 62, 2, 4, 'Residential', 170.5, 1108, 188900, NULL::numeric, NULL::text),
    (10, 63, 3, 2, 'Residential', 90.0, 1108, 99700, NULL::numeric, 'bay-2'::text),
    (10, 64, 4, 0, 'Residential', 52.1, 1108, 57700, NULL::numeric, NULL::text),
    (10, 57, 5, 1, 'Residential', 80.5, 1108, 89200, NULL::numeric, NULL::text),
    (10, 58, 6, 4, 'Residential', 170.5, 1108, 188900, NULL::numeric, NULL::text),
    (10, 59, 7, 2, 'Residential', 90.0, 1108, 99700, NULL::numeric, 'bay-2'::text),
    (11, 68, 0, 0, 'Residential', 52.1, 1085, 56500, NULL::numeric, NULL::text),
    (11, 69, 1, 1, 'Residential', 80.5, 1085, 87300, NULL::numeric, NULL::text),
    (11, 70, 2, 4, 'Residential', 170.5, 1085, 185000, NULL::numeric, NULL::text),
    (11, 71, 3, 2, 'Residential', 90.0, 1085, 97700, NULL::numeric, 'bay-2'::text),
    (11, 72, 4, 0, 'Residential', 52.1, 1085, 56500, NULL::numeric, NULL::text),
    (11, 65, 5, 1, 'Residential', 80.5, 1085, 87300, NULL::numeric, NULL::text),
    (11, 66, 6, 4, 'Residential', 170.5, 1085, 185000, NULL::numeric, NULL::text),
    (11, 67, 7, 2, 'Residential', 90.0, 1085, 97700, NULL::numeric, 'bay-2'::text),
    (12, 76, 0, 0, 'Residential', 52.1, 1062, 55300, NULL::numeric, NULL::text),
    (12, 77, 1, 1, 'Residential', 80.5, 1062, 85500, NULL::numeric, NULL::text),
    (12, 78, 2, 4, 'Residential', 170.5, 1062, 181100, NULL::numeric, NULL::text),
    (12, 79, 3, 2, 'Residential', 90.0, 1062, 95600, NULL::numeric, 'bay-2'::text),
    (12, 80, 4, 0, 'Residential', 52.1, 1062, 55300, NULL::numeric, NULL::text),
    (12, 73, 5, 1, 'Residential', 80.5, 1062, 85500, NULL::numeric, NULL::text),
    (12, 74, 6, 4, 'Residential', 170.5, 1062, 181100, NULL::numeric, NULL::text),
    (12, 75, 7, 2, 'Residential', 90.0, 1062, 95600, NULL::numeric, 'bay-2'::text),
    (13, 84, 0, 0, 'Residential', 52.1, 1038, 54100, NULL::numeric, NULL::text),
    (13, 85, 1, 1, 'Residential', 80.5, 1038, 83600, NULL::numeric, NULL::text),
    (13, 86, 2, 4, 'Residential', 170.5, 1038, 177000, NULL::numeric, NULL::text),
    (13, 87, 3, 2, 'Residential', 90.0, 1038, 93400, NULL::numeric, 'bay-2'::text),
    (13, 88, 4, 0, 'Residential', 52.1, 1038, 54100, NULL::numeric, NULL::text),
    (13, 81, 5, 1, 'Residential', 80.5, 1038, 83600, NULL::numeric, NULL::text),
    (13, 82, 6, 4, 'Residential', 170.5, 1038, 177000, NULL::numeric, NULL::text),
    (13, 83, 7, 2, 'Residential', 90.0, 1038, 93400, NULL::numeric, 'bay-2'::text),
    (14, 92, 0, 0, 'Residential', 52.1, 1015, 52900, NULL::numeric, NULL::text),
    (14, 93, 1, 1, 'Residential', 80.5, 1015, 81700, NULL::numeric, NULL::text),
    (14, 94, 2, 4, 'Residential', 170.5, 1015, 173100, NULL::numeric, NULL::text),
    (14, 95, 3, 2, 'Residential', 90.0, 1015, 91400, NULL::numeric, 'bay-2'::text),
    (14, 96, 4, 0, 'Residential', 52.1, 1015, 52900, NULL::numeric, NULL::text),
    (14, 89, 5, 1, 'Residential', 80.5, 1015, 81700, NULL::numeric, NULL::text),
    (14, 90, 6, 4, 'Residential', 170.5, 1015, 173100, NULL::numeric, NULL::text),
    (14, 91, 7, 2, 'Residential', 90.0, 1015, 91400, NULL::numeric, 'bay-2'::text),
    (15, 100, 0, 0, 'Residential', 52.1, 992, 51700, NULL::numeric, NULL::text),
    (15, 101, 1, 1, 'Residential', 80.5, 992, 79900, NULL::numeric, NULL::text),
    (15, 102, 2, 4, 'Residential', 170.5, 992, 169100, NULL::numeric, NULL::text),
    (15, 103, 3, 2, 'Residential', 90.0, 992, 89300, NULL::numeric, 'bay-2'::text),
    (15, 104, 4, 0, 'Residential', 52.1, 992, 51700, NULL::numeric, NULL::text),
    (15, 97, 5, 1, 'Residential', 80.5, 992, 79900, NULL::numeric, NULL::text),
    (15, 98, 6, 4, 'Residential', 170.5, 992, 169100, NULL::numeric, NULL::text),
    (15, 99, 7, 2, 'Residential', 90.0, 992, 89300, NULL::numeric, 'bay-2'::text),
    (16, 108, 0, 0, 'Residential', 52.1, 969, 50500, NULL::numeric, NULL::text),
    (16, 109, 1, 1, 'Residential', 80.5, 969, 78000, NULL::numeric, NULL::text),
    (16, 110, 2, 4, 'Residential', 170.5, 969, 165200, NULL::numeric, NULL::text),
    (16, 111, 3, 2, 'Residential', 90.0, 969, 87200, NULL::numeric, 'bay-2'::text),
    (16, 112, 4, 0, 'Residential', 52.1, 969, 50500, NULL::numeric, NULL::text),
    (16, 105, 5, 1, 'Residential', 80.5, 969, 78000, NULL::numeric, NULL::text),
    (16, 106, 6, 4, 'Residential', 170.5, 969, 165200, NULL::numeric, NULL::text),
    (16, 107, 7, 2, 'Residential', 90.0, 969, 87200, NULL::numeric, 'bay-2'::text),
    (17, 116, 0, 0, 'Residential', 52.1, 946, 49300, NULL::numeric, NULL::text),
    (17, 117, 1, 1, 'Residential', 80.5, 946, 76200, NULL::numeric, NULL::text),
    (17, 118, 2, 4, 'Residential', 170.5, 946, 161300, NULL::numeric, NULL::text),
    (17, 119, 3, 2, 'Residential', 90.0, 946, 85100, NULL::numeric, 'bay-2'::text),
    (17, 120, 4, 0, 'Residential', 52.1, 946, 49300, NULL::numeric, NULL::text),
    (17, 113, 5, 1, 'Residential', 80.5, 946, 76200, NULL::numeric, NULL::text),
    (17, 114, 6, 4, 'Residential', 170.5, 946, 161300, NULL::numeric, NULL::text),
    (17, 115, 7, 2, 'Residential', 90.0, 946, 85100, NULL::numeric, 'bay-2'::text),
    (18, 124, 0, 0, 'Residential', 52.1, 923, 48100, NULL::numeric, NULL::text),
    (18, 125, 1, 1, 'Residential', 80.5, 923, 74300, NULL::numeric, NULL::text),
    (18, 126, 2, 4, 'Residential', 170.5, 923, 157400, NULL::numeric, NULL::text),
    (18, 127, 3, 2, 'Residential', 90.0, 923, 83100, NULL::numeric, 'bay-2'::text),
    (18, 128, 4, 0, 'Residential', 52.1, 923, 48100, NULL::numeric, NULL::text),
    (18, 121, 5, 1, 'Residential', 80.5, 923, 74300, NULL::numeric, NULL::text),
    (18, 122, 6, 4, 'Residential', 170.5, 923, 157400, NULL::numeric, NULL::text),
    (18, 123, 7, 2, 'Residential', 90.0, 923, 83100, NULL::numeric, 'bay-2'::text),
    (19, 132, 0, 0, 'Residential', 52.1, 900, 46900, NULL::numeric, NULL::text),
    (19, 133, 1, 1, 'Residential', 80.5, 900, 72500, NULL::numeric, NULL::text),
    (19, 134, 2, 4, 'Residential', 170.5, 900, 153500, NULL::numeric, NULL::text),
    (19, 135, 3, 2, 'Residential', 90.0, 900, 81000, NULL::numeric, 'bay-2'::text),
    (19, 136, 4, 0, 'Residential', 52.1, 900, 46900, NULL::numeric, NULL::text),
    (19, 129, 5, 1, 'Residential', 80.5, 900, 72500, NULL::numeric, NULL::text),
    (19, 130, 6, 4, 'Residential', 170.5, 900, 153500, NULL::numeric, NULL::text),
    (19, 131, 7, 2, 'Residential', 90.0, 900, 81000, NULL::numeric, 'bay-2'::text),
    (20, 139, 0, 4, 'Penthouse', 184, 1400, 257600, 39.8::numeric, 'ph-4'::text),
    (20, 140, 1, 4, 'Penthouse', 184, 1400, 257600, 39.8::numeric, 'ph-4'::text),
    (20, 137, 2, 4, 'Penthouse', 184, 1400, 257600, 39.8::numeric, 'ph-4'::text),
    (20, 138, 3, 4, 'Penthouse', 184, 1400, 257600, 39.8::numeric, 'ph-4'::text)
  ) AS v(floor, num, slot, rooms, kind, area, ppm, price, terrace, layout)
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
  INSERT INTO "Apartments" ("Id", "FloorId", "BuildingId", "Number", "PlanSlot", "Model3dId", "Rooms", "Area", "PricePerM2", "Price", "Currency",
    "Status", "Kind", "TerraceArea", "LayoutCode", "CreatedAt", "UpdatedAt")
  SELECT gen_random_uuid(), f."Id", b, v.num, v.slot, NULL, v.rooms, v.area, v.ppm, v.price, 'USD', 'Free', v.kind, v.terrace, v.layout, ts, ts
  FROM (VALUES
    (-2, 29, 0, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 30, 1, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 31, 2, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 32, 3, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 33, 4, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 34, 5, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 35, 6, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 36, 7, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 37, 8, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 38, 9, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 39, 10, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 40, 11, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 41, 12, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 42, 13, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 43, 14, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 44, 15, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 45, 16, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 46, 17, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 47, 18, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 48, 19, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 49, 20, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 50, 21, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 51, 22, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 52, 23, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 53, 24, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 54, 25, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 55, 26, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-2, 56, 27, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 1, 0, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 2, 1, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 3, 2, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 4, 3, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 5, 4, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 6, 5, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 7, 6, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 8, 7, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 9, 8, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 10, 9, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 11, 10, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 12, 11, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 13, 12, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 14, 13, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 15, 14, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 16, 15, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 17, 16, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 18, 17, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 19, 18, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 20, 19, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 21, 20, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 22, 21, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 23, 22, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 24, 23, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 25, 24, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 26, 25, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 27, 26, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (-1, 28, 27, 0, 'Parking', 13.25, 1509.43, 20000, NULL::numeric, NULL::text),
    (1, 1, 0, 0, 'Commercial', 193.2, 1500, 289800, NULL::numeric, NULL::text),
    (1, 2, 1, 0, 'Commercial', 128.8, 1500, 193200, NULL::numeric, NULL::text),
    (1, 3, 2, 0, 'Commercial', 161, 1500, 241500, NULL::numeric, NULL::text),
    (1, 4, 3, 0, 'Commercial', 193.2, 1500, 289800, NULL::numeric, NULL::text),
    (1, 5, 4, 0, 'Commercial', 128.8, 1500, 193200, NULL::numeric, NULL::text),
    (1, 6, 5, 0, 'Commercial', 161, 1500, 241500, NULL::numeric, NULL::text),
    (2, 7, 0, 0, 'Commercial', 193.2, 1500, 289800, NULL::numeric, NULL::text),
    (2, 8, 1, 0, 'Commercial', 128.8, 1500, 193200, NULL::numeric, NULL::text),
    (2, 9, 2, 0, 'Commercial', 161, 1500, 241500, NULL::numeric, NULL::text),
    (2, 10, 3, 0, 'Commercial', 193.2, 1500, 289800, NULL::numeric, NULL::text),
    (2, 11, 4, 0, 'Commercial', 128.8, 1500, 193200, NULL::numeric, NULL::text),
    (2, 12, 5, 0, 'Commercial', 161, 1500, 241500, NULL::numeric, NULL::text),
    (3, 3, 0, 0, 'Residential', 67.3, 1200, 80800, NULL::numeric, NULL::text),
    (3, 4, 1, 4, 'Residential', 195.6, 1200, 234700, NULL::numeric, 'corner-round'::text),
    (3, 5, 2, 1, 'Residential', 97.8, 1200, 117400, NULL::numeric, NULL::text),
    (3, 6, 3, 2, 'Residential', 122.3, 1200, 146800, NULL::numeric, NULL::text),
    (3, 7, 4, 2, 'Residential', 122.3, 1200, 146800, NULL::numeric, NULL::text),
    (3, 8, 5, 1, 'Residential', 97.8, 1200, 117400, NULL::numeric, NULL::text),
    (3, 1, 6, 4, 'Residential', 195.6, 1200, 234700, NULL::numeric, 'corner-round'::text),
    (3, 2, 7, 0, 'Residential', 67.3, 1200, 80800, NULL::numeric, NULL::text),
    (4, 11, 0, 0, 'Residential', 67.3, 1200, 80800, NULL::numeric, NULL::text),
    (4, 12, 1, 4, 'Residential', 195.6, 1200, 234700, NULL::numeric, 'corner-round'::text),
    (4, 13, 2, 1, 'Residential', 97.8, 1200, 117400, NULL::numeric, NULL::text),
    (4, 14, 3, 2, 'Residential', 122.3, 1200, 146800, NULL::numeric, NULL::text),
    (4, 15, 4, 2, 'Residential', 122.3, 1200, 146800, NULL::numeric, NULL::text),
    (4, 16, 5, 1, 'Residential', 97.8, 1200, 117400, NULL::numeric, NULL::text),
    (4, 9, 6, 4, 'Residential', 195.6, 1200, 234700, NULL::numeric, 'corner-round'::text),
    (4, 10, 7, 0, 'Residential', 67.3, 1200, 80800, NULL::numeric, NULL::text),
    (5, 19, 0, 0, 'Residential', 67.3, 1200, 80800, NULL::numeric, NULL::text),
    (5, 20, 1, 4, 'Residential', 195.6, 1200, 234700, NULL::numeric, 'corner-round'::text),
    (5, 21, 2, 1, 'Residential', 97.8, 1200, 117400, NULL::numeric, NULL::text),
    (5, 22, 3, 2, 'Residential', 122.3, 1200, 146800, NULL::numeric, NULL::text),
    (5, 23, 4, 2, 'Residential', 122.3, 1200, 146800, NULL::numeric, NULL::text),
    (5, 24, 5, 1, 'Residential', 97.8, 1200, 117400, NULL::numeric, NULL::text),
    (5, 17, 6, 4, 'Residential', 195.6, 1200, 234700, NULL::numeric, 'corner-round'::text),
    (5, 18, 7, 0, 'Residential', 67.3, 1200, 80800, NULL::numeric, NULL::text),
    (6, 27, 0, 0, 'Residential', 67.3, 1200, 80800, NULL::numeric, NULL::text),
    (6, 28, 1, 4, 'Residential', 195.6, 1200, 234700, NULL::numeric, 'corner-round'::text),
    (6, 29, 2, 1, 'Residential', 97.8, 1200, 117400, NULL::numeric, NULL::text),
    (6, 30, 3, 2, 'Residential', 122.3, 1200, 146800, NULL::numeric, NULL::text),
    (6, 31, 4, 2, 'Residential', 122.3, 1200, 146800, NULL::numeric, NULL::text),
    (6, 32, 5, 1, 'Residential', 97.8, 1200, 117400, NULL::numeric, NULL::text),
    (6, 25, 6, 4, 'Residential', 195.6, 1200, 234700, NULL::numeric, 'corner-round'::text),
    (6, 26, 7, 0, 'Residential', 67.3, 1200, 80800, NULL::numeric, NULL::text),
    (7, 35, 0, 0, 'Residential', 67.3, 1177, 79200, NULL::numeric, NULL::text),
    (7, 36, 1, 4, 'Residential', 195.6, 1177, 230200, NULL::numeric, 'corner-round'::text),
    (7, 37, 2, 1, 'Residential', 97.8, 1177, 115100, NULL::numeric, NULL::text),
    (7, 38, 3, 2, 'Residential', 122.3, 1177, 143900, NULL::numeric, NULL::text),
    (7, 39, 4, 2, 'Residential', 122.3, 1177, 143900, NULL::numeric, NULL::text),
    (7, 40, 5, 1, 'Residential', 97.8, 1177, 115100, NULL::numeric, NULL::text),
    (7, 33, 6, 4, 'Residential', 195.6, 1177, 230200, NULL::numeric, 'corner-round'::text),
    (7, 34, 7, 0, 'Residential', 67.3, 1177, 79200, NULL::numeric, NULL::text),
    (8, 43, 0, 0, 'Residential', 67.3, 1154, 77700, NULL::numeric, NULL::text),
    (8, 44, 1, 4, 'Residential', 195.6, 1154, 225700, NULL::numeric, 'corner-round'::text),
    (8, 45, 2, 1, 'Residential', 97.8, 1154, 112900, NULL::numeric, NULL::text),
    (8, 46, 3, 2, 'Residential', 122.3, 1154, 141100, NULL::numeric, NULL::text),
    (8, 47, 4, 2, 'Residential', 122.3, 1154, 141100, NULL::numeric, NULL::text),
    (8, 48, 5, 1, 'Residential', 97.8, 1154, 112900, NULL::numeric, NULL::text),
    (8, 41, 6, 4, 'Residential', 195.6, 1154, 225700, NULL::numeric, 'corner-round'::text),
    (8, 42, 7, 0, 'Residential', 67.3, 1154, 77700, NULL::numeric, NULL::text),
    (9, 51, 0, 0, 'Residential', 67.3, 1131, 76100, NULL::numeric, NULL::text),
    (9, 52, 1, 4, 'Residential', 195.6, 1131, 221200, NULL::numeric, 'corner-round'::text),
    (9, 53, 2, 1, 'Residential', 97.8, 1131, 110600, NULL::numeric, NULL::text),
    (9, 54, 3, 2, 'Residential', 122.3, 1131, 138300, NULL::numeric, NULL::text),
    (9, 55, 4, 2, 'Residential', 122.3, 1131, 138300, NULL::numeric, NULL::text),
    (9, 56, 5, 1, 'Residential', 97.8, 1131, 110600, NULL::numeric, NULL::text),
    (9, 49, 6, 4, 'Residential', 195.6, 1131, 221200, NULL::numeric, 'corner-round'::text),
    (9, 50, 7, 0, 'Residential', 67.3, 1131, 76100, NULL::numeric, NULL::text),
    (10, 59, 0, 0, 'Residential', 67.3, 1108, 74600, NULL::numeric, NULL::text),
    (10, 60, 1, 4, 'Residential', 195.6, 1108, 216700, NULL::numeric, 'corner-round'::text),
    (10, 61, 2, 1, 'Residential', 97.8, 1108, 108400, NULL::numeric, NULL::text),
    (10, 62, 3, 2, 'Residential', 122.3, 1108, 135500, NULL::numeric, NULL::text),
    (10, 63, 4, 2, 'Residential', 122.3, 1108, 135500, NULL::numeric, NULL::text),
    (10, 64, 5, 1, 'Residential', 97.8, 1108, 108400, NULL::numeric, NULL::text),
    (10, 57, 6, 4, 'Residential', 195.6, 1108, 216700, NULL::numeric, 'corner-round'::text),
    (10, 58, 7, 0, 'Residential', 67.3, 1108, 74600, NULL::numeric, NULL::text),
    (11, 67, 0, 0, 'Residential', 67.3, 1085, 73000, NULL::numeric, NULL::text),
    (11, 68, 1, 4, 'Residential', 195.6, 1085, 212200, NULL::numeric, 'corner-round'::text),
    (11, 69, 2, 1, 'Residential', 97.8, 1085, 106100, NULL::numeric, NULL::text),
    (11, 70, 3, 2, 'Residential', 122.3, 1085, 132700, NULL::numeric, NULL::text),
    (11, 71, 4, 2, 'Residential', 122.3, 1085, 132700, NULL::numeric, NULL::text),
    (11, 72, 5, 1, 'Residential', 97.8, 1085, 106100, NULL::numeric, NULL::text),
    (11, 65, 6, 4, 'Residential', 195.6, 1085, 212200, NULL::numeric, 'corner-round'::text),
    (11, 66, 7, 0, 'Residential', 67.3, 1085, 73000, NULL::numeric, NULL::text),
    (12, 75, 0, 0, 'Residential', 67.3, 1062, 71500, NULL::numeric, NULL::text),
    (12, 76, 1, 4, 'Residential', 195.6, 1062, 207700, NULL::numeric, 'corner-round'::text),
    (12, 77, 2, 1, 'Residential', 97.8, 1062, 103900, NULL::numeric, NULL::text),
    (12, 78, 3, 2, 'Residential', 122.3, 1062, 129900, NULL::numeric, NULL::text),
    (12, 79, 4, 2, 'Residential', 122.3, 1062, 129900, NULL::numeric, NULL::text),
    (12, 80, 5, 1, 'Residential', 97.8, 1062, 103900, NULL::numeric, NULL::text),
    (12, 73, 6, 4, 'Residential', 195.6, 1062, 207700, NULL::numeric, 'corner-round'::text),
    (12, 74, 7, 0, 'Residential', 67.3, 1062, 71500, NULL::numeric, NULL::text),
    (13, 83, 0, 0, 'Residential', 67.3, 1038, 69900, NULL::numeric, NULL::text),
    (13, 84, 1, 4, 'Residential', 195.6, 1038, 203000, NULL::numeric, 'corner-round'::text),
    (13, 85, 2, 1, 'Residential', 97.8, 1038, 101500, NULL::numeric, NULL::text),
    (13, 86, 3, 2, 'Residential', 122.3, 1038, 126900, NULL::numeric, NULL::text),
    (13, 87, 4, 2, 'Residential', 122.3, 1038, 126900, NULL::numeric, NULL::text),
    (13, 88, 5, 1, 'Residential', 97.8, 1038, 101500, NULL::numeric, NULL::text),
    (13, 81, 6, 4, 'Residential', 195.6, 1038, 203000, NULL::numeric, 'corner-round'::text),
    (13, 82, 7, 0, 'Residential', 67.3, 1038, 69900, NULL::numeric, NULL::text),
    (14, 91, 0, 0, 'Residential', 67.3, 1015, 68300, NULL::numeric, NULL::text),
    (14, 92, 1, 4, 'Residential', 195.6, 1015, 198500, NULL::numeric, 'corner-round'::text),
    (14, 93, 2, 1, 'Residential', 97.8, 1015, 99300, NULL::numeric, NULL::text),
    (14, 94, 3, 2, 'Residential', 122.3, 1015, 124100, NULL::numeric, NULL::text),
    (14, 95, 4, 2, 'Residential', 122.3, 1015, 124100, NULL::numeric, NULL::text),
    (14, 96, 5, 1, 'Residential', 97.8, 1015, 99300, NULL::numeric, NULL::text),
    (14, 89, 6, 4, 'Residential', 195.6, 1015, 198500, NULL::numeric, 'corner-round'::text),
    (14, 90, 7, 0, 'Residential', 67.3, 1015, 68300, NULL::numeric, NULL::text),
    (15, 99, 0, 0, 'Residential', 67.3, 992, 66800, NULL::numeric, NULL::text),
    (15, 100, 1, 4, 'Residential', 195.6, 992, 194000, NULL::numeric, 'corner-round'::text),
    (15, 101, 2, 1, 'Residential', 97.8, 992, 97000, NULL::numeric, NULL::text),
    (15, 102, 3, 2, 'Residential', 122.3, 992, 121300, NULL::numeric, NULL::text),
    (15, 103, 4, 2, 'Residential', 122.3, 992, 121300, NULL::numeric, NULL::text),
    (15, 104, 5, 1, 'Residential', 97.8, 992, 97000, NULL::numeric, NULL::text),
    (15, 97, 6, 4, 'Residential', 195.6, 992, 194000, NULL::numeric, 'corner-round'::text),
    (15, 98, 7, 0, 'Residential', 67.3, 992, 66800, NULL::numeric, NULL::text),
    (16, 107, 0, 0, 'Residential', 67.3, 969, 65200, NULL::numeric, NULL::text),
    (16, 108, 1, 4, 'Residential', 195.6, 969, 189500, NULL::numeric, 'corner-round'::text),
    (16, 109, 2, 1, 'Residential', 97.8, 969, 94800, NULL::numeric, NULL::text),
    (16, 110, 3, 2, 'Residential', 122.3, 969, 118500, NULL::numeric, NULL::text),
    (16, 111, 4, 2, 'Residential', 122.3, 969, 118500, NULL::numeric, NULL::text),
    (16, 112, 5, 1, 'Residential', 97.8, 969, 94800, NULL::numeric, NULL::text),
    (16, 105, 6, 4, 'Residential', 195.6, 969, 189500, NULL::numeric, 'corner-round'::text),
    (16, 106, 7, 0, 'Residential', 67.3, 969, 65200, NULL::numeric, NULL::text),
    (17, 115, 0, 0, 'Residential', 67.3, 946, 63700, NULL::numeric, NULL::text),
    (17, 116, 1, 4, 'Residential', 195.6, 946, 185000, NULL::numeric, 'corner-round'::text),
    (17, 117, 2, 1, 'Residential', 97.8, 946, 92500, NULL::numeric, NULL::text),
    (17, 118, 3, 2, 'Residential', 122.3, 946, 115700, NULL::numeric, NULL::text),
    (17, 119, 4, 2, 'Residential', 122.3, 946, 115700, NULL::numeric, NULL::text),
    (17, 120, 5, 1, 'Residential', 97.8, 946, 92500, NULL::numeric, NULL::text),
    (17, 113, 6, 4, 'Residential', 195.6, 946, 185000, NULL::numeric, 'corner-round'::text),
    (17, 114, 7, 0, 'Residential', 67.3, 946, 63700, NULL::numeric, NULL::text),
    (18, 123, 0, 0, 'Residential', 67.3, 923, 62100, NULL::numeric, NULL::text),
    (18, 124, 1, 4, 'Residential', 195.6, 923, 180500, NULL::numeric, 'corner-round'::text),
    (18, 125, 2, 1, 'Residential', 97.8, 923, 90300, NULL::numeric, NULL::text),
    (18, 126, 3, 2, 'Residential', 122.3, 923, 112900, NULL::numeric, NULL::text),
    (18, 127, 4, 2, 'Residential', 122.3, 923, 112900, NULL::numeric, NULL::text),
    (18, 128, 5, 1, 'Residential', 97.8, 923, 90300, NULL::numeric, NULL::text),
    (18, 121, 6, 4, 'Residential', 195.6, 923, 180500, NULL::numeric, 'corner-round'::text),
    (18, 122, 7, 0, 'Residential', 67.3, 923, 62100, NULL::numeric, NULL::text),
    (19, 131, 0, 0, 'Residential', 67.3, 900, 60600, NULL::numeric, NULL::text),
    (19, 132, 1, 4, 'Residential', 195.6, 900, 176000, NULL::numeric, 'corner-round'::text),
    (19, 133, 2, 1, 'Residential', 97.8, 900, 88000, NULL::numeric, NULL::text),
    (19, 134, 3, 2, 'Residential', 122.3, 900, 110100, NULL::numeric, NULL::text),
    (19, 135, 4, 2, 'Residential', 122.3, 900, 110100, NULL::numeric, NULL::text),
    (19, 136, 5, 1, 'Residential', 97.8, 900, 88000, NULL::numeric, NULL::text),
    (19, 129, 6, 4, 'Residential', 195.6, 900, 176000, NULL::numeric, 'corner-round'::text),
    (19, 130, 7, 0, 'Residential', 67.3, 900, 60600, NULL::numeric, NULL::text),
    (20, 139, 0, 4, 'Penthouse', 206.5, 1400, 289100, 32.9::numeric, 'ph-4'::text),
    (20, 140, 1, 4, 'Penthouse', 219.3, 1400, 307000, 35.0::numeric, 'ph-4'::text),
    (20, 137, 2, 4, 'Penthouse', 219.3, 1400, 307000, 35.0::numeric, 'ph-4'::text),
    (20, 138, 3, 4, 'Penthouse', 206.5, 1400, 289100, 32.9::numeric, 'ph-4'::text)
  ) AS v(floor, num, slot, rooms, kind, area, ppm, price, terrace, layout)
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
