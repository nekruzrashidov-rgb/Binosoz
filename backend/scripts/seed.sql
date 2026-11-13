-- Demo catalog for an empty production database (Render): «Некруз Резиденс», Dushanbe, 20 floors, 160 apartments.
--
-- Run once against the Render PostgreSQL (External Database URL from the Render dashboard):
--   psql "postgresql://USER:PASSWORD@HOST/DB?sslmode=require" -f backend/scripts/seed.sql
--
-- * Idempotent: if the organization already has a building named «Некруз Резиденс», nothing is changed.
-- * Atomic: everything is one DO block — on any error nothing is left half-written.
-- * Floors: 0 (commercial, no apartments) and 1–20 (residential), 8 apartments each, numbers floor×100 + slot+1 (101 … 2008).
-- * Types by plan slot: 0–1 — 1 room 42.5 m² at 7 500 TJS/m²; 2–3 — 2 rooms 68.0 m² at 7 000; 4–5 — 3 rooms 92.0 m² at 6 800;
--   6–7 — 4 rooms 118.5 m² at 6 500. Price = ROUND(area × price per m² / 100) × 100, as Apartment.CalculatePrice
--   (PostgreSQL rounds numeric halves away from zero, like MidpointRounding.AwayFromZero). Money is numeric(18,2), currency TJS.
-- * Statuses: floors 1–3, slots 0–1 — Sold (6); floors 1–5, slot 2 — Booked (5); all others — Free (149).
--   A booked apartment gets a real active booking (with a demo lead: a booking needs a customer or a lead) expiring in 30 days,
--   so managers can cancel, extend or turn it into a deal in the CRM. Sold apartments have no deal: they are catalog data only.
-- * The seed itself is recorded in AuditLog (Building / Seeded).
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

  IF EXISTS (SELECT 1 FROM "Buildings" WHERE "OrganizationId" = org AND "NameRu" = 'Некруз Резиденс') THEN
    RAISE NOTICE 'Дом «Некруз Резиденс» уже есть — заполнение пропущено';
    RETURN;
  END IF;

  building := gen_random_uuid();
  INSERT INTO "Buildings" ("Id", "OrganizationId", "NameRu", "NameTg", "Address", "FloorsCount", "DescriptionRu", "DescriptionTg",
                           "ModelUrl", "CompletionYear", "PlannedApartments", "CreatedAt", "UpdatedAt")
  VALUES (building, org, 'Некруз Резиденс', 'Некруз Резиденс', 'г. Душанбе, пр. Борбад, 15', 20,
          'Жилой комплекс бизнес-класса с коммерческим первым этажом', NULL, NULL, 2026, 160, ts, ts);

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
        VALUES (gen_random_uuid(), apt_id, NULL, lead_id, ts + interval '30 days', 'Active', NULL, ts, ts);
      END IF;
    END LOOP;
  END LOOP;

  INSERT INTO "AuditLog" ("Id", "UserId", "OrganizationId", "ImpersonatedById", "Entity", "EntityId", "Action", "Before", "After",
                          "At", "Ip", "CreatedAt", "UpdatedAt")
  VALUES (gen_random_uuid(), NULL, org, NULL, 'Building', building, 'Seeded', NULL,
          jsonb_build_object('source', 'scripts/seed.sql', 'floors', 20, 'apartments', 160, 'sold', sold_n, 'booked', booked_n),
          ts, NULL, ts, ts);

  RAISE NOTICE 'Создан дом «Некруз Резиденс»: 21 этаж (0 — коммерческий), 160 квартир, продано %, в брони %', sold_n, booked_n;
END $$;
