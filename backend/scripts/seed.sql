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
