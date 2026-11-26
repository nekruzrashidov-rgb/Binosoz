using System.Globalization;
using System.Text;
using System.Text.Json;
using Sitora.Domain.Entities;

namespace Sitora.Infrastructure.Data.Seed;

// Блок backend/scripts/seed.sql «Rich Tower — блоки A/B/C» для прода, из тех же данных, что RichTowerBlocksSeeder
// (Development): контуры, места, уступы, правило цены и все помещения. На проде помещения создаются свободными —
// продаж и броней скрипт не выдумывает. Текст блока сверяет тест (RichTowerBlocksSqlTests): после изменения сидера
// его перегенерирует UPDATE_SEED_SQL=1 dotnet test --filter RichTowerBlocksSql.
public static class RichTowerBlocksSql
{
    public const string Begin = "-- BEGIN generated: Rich Tower blocks (RichTowerBlocksSql.cs; UPDATE_SEED_SQL=1 dotnet test --filter RichTowerBlocksSql)";
    public const string End = "-- END generated: Rich Tower blocks";
    public const string UnpublishedAction = "Unpublished";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private static string Q(string? s) => s is null ? "NULL" : "'" + s.Replace("'", "''", StringComparison.Ordinal) + "'";
    private static string J(object? o) => o is null ? "NULL" : Q(JsonSerializer.Serialize(o, Json)) + "::jsonb";
    private static string D(decimal? d) => d is { } v ? v.ToString(Inv) : "NULL";

    public static string Generate()
    {
        var sb = new StringBuilder();
        void L(string line) => sb.Append(line).Append('\n');
        L(Begin);
        L("-- Rich Tower — 3 блока на одном участке (концепт, docs/DECISIONS.md). Один раз: если у организации по умолчанию ещё");
        L("-- нет блоков (BlockCode), создаются блоки A/B/C со всеми этажами и помещениями (свободными, цены в USD по правилу),");
        L("-- план участка (если не задан), и прежние дома организации без буквы скрываются с публичного сайта");
        L("-- (IsPublished = false, AuditLog Unpublished). Их квартиры, брони, сделки, графики и платежи не меняются —");
        L("-- покупатель видит свою сделку в кабинете, сотрудник — дом в CRM. Повторный запуск ничего не меняет.");
        L("DO $$");
        L("DECLARE");
        L("  org constant uuid := '" + Organization.DefaultId + "';");
        L("  ts  constant timestamptz := now();");
        L("  b   uuid;");
        L("  old record;");
        L("BEGIN");
        L("  IF NOT EXISTS (SELECT 1 FROM \"Organizations\" WHERE \"Id\" = org) THEN");
        L("    RAISE EXCEPTION 'Нет организации по умолчанию %: сначала запустите API, чтобы он создал её', org;");
        L("  END IF;");
        L("  IF EXISTS (SELECT 1 FROM \"Buildings\" WHERE \"OrganizationId\" = org AND \"BlockCode\" IS NOT NULL) THEN");
        L("    RAISE NOTICE 'Блоки ЖК уже есть — пропущено';");
        L("    RETURN;");
        L("  END IF;");
        foreach (var spec in RichTowerBlocksSeeder.Blocks)
        {
            var x = RichTowerBlocksSeeder.NewBlock(spec, Organization.DefaultId, null);
            L("");
            L("  -- Блок " + spec.Code + ": " + spec.Floors + " этажей, −2/−1 паркинг, 1–2 магазины, последний — пентхаусы.");
            L("  b := gen_random_uuid();");
            L("  INSERT INTO \"Buildings\" (\"Id\", \"OrganizationId\", \"NameRu\", \"NameTg\", \"NameEn\", \"Address\", \"FloorsCount\", \"DescriptionRu\",");
            L("    \"DescriptionTg\", \"DescriptionEn\", \"ModelUrl\", \"CompletionYear\", \"PlannedApartments\", \"Amenities\", \"BlockCode\", \"SortOrder\",");
            L("    \"Footprint\", \"SitePosition\", \"Setbacks\", \"PriceSteps\", \"PenthousePricePerM2\", \"PriceCurrency\", \"CommercialPricePerM2\",");
            L("    \"ParkingSpotPrice\", \"ElevatorsCount\", \"IsPublished\", \"CreatedAt\", \"UpdatedAt\")");
            L("  VALUES (b, org, " + Q(x.NameRu) + ", " + Q(x.NameTg) + ", " + Q(x.NameEn) + ", " + Q(x.Address) + ", " + x.FloorsCount + ",");
            L("    " + Q(x.DescriptionRu) + ",");
            L("    " + Q(x.DescriptionTg) + ",");
            L("    " + Q(x.DescriptionEn) + ",");
            L("    NULL, NULL, " + x.PlannedApartments + ", ARRAY[" + string.Join(", ", x.Amenities.Select(Q)) + "]::text[], " + Q(x.BlockCode) + ", " + x.SortOrder + ",");
            L("    " + J(x.Footprint) + ",");
            L("    " + J(x.SitePosition) + ",");
            L("    " + J(x.Setbacks) + ",");
            L("    " + J(x.PriceSteps) + ",");
            L("    " + D(x.PenthousePricePerM2) + ", " + Q(x.PriceCurrency.ToString()) + ", " + D(x.CommercialPricePerM2) + ", " + D(x.ParkingSpotPrice) + ", "
              + x.ElevatorsCount + ", true, ts, ts);");
            L("  INSERT INTO \"Floors\" (\"Id\", \"BuildingId\", \"Number\", \"PlanImageUrl\", \"CreatedAt\", \"UpdatedAt\")");
            L("  SELECT gen_random_uuid(), b, n, NULL, ts, ts FROM unnest(ARRAY[" + string.Join(", ", x.Floors.Select(f => f.Number).OrderBy(n => n)) + "]) AS n;");
            L("  INSERT INTO \"Apartments\" (\"Id\", \"FloorId\", \"BuildingId\", \"Number\", \"PlanSlot\", \"Model3dId\", \"Rooms\", \"Area\", \"PricePerM2\", \"Price\", \"Currency\",");
            L("    \"Status\", \"Kind\", \"TerraceArea\", \"CreatedAt\", \"UpdatedAt\")");
            L("  SELECT gen_random_uuid(), f.\"Id\", b, v.num, v.slot, NULL, v.rooms, v.area, v.ppm, v.price, " + Q(x.PriceCurrency.ToString()) + ", 'Free', v.kind, v.terrace, ts, ts");
            L("  FROM (VALUES");
            var rows = x.Floors.OrderBy(f => f.Number).SelectMany(f => f.Apartments.OrderBy(a => a.PlanSlot).Select(a =>
                string.Create(Inv, $"    ({f.Number}, {a.Number}, {a.PlanSlot}, {a.Rooms}, '{a.Kind}', {a.Area}, {a.PricePerM2}, {a.Price}, {D(a.TerraceArea)}::numeric)"))).ToList();
            for (var i = 0; i < rows.Count; i++)
                L(rows[i] + (i < rows.Count - 1 ? "," : ""));
            L("  ) AS v(floor, num, slot, rooms, kind, area, ppm, price, terrace)");
            L("  JOIN \"Floors\" f ON f.\"BuildingId\" = b AND f.\"Number\" = v.floor;");
            L("  INSERT INTO \"AuditLog\" (\"Id\", \"UserId\", \"OrganizationId\", \"ImpersonatedById\", \"Entity\", \"EntityId\", \"Action\", \"Before\", \"After\",");
            L("                          \"At\", \"Ip\", \"CreatedAt\", \"UpdatedAt\")");
            L("  VALUES (gen_random_uuid(), NULL, org, NULL, 'Building', b, 'Seeded', NULL,");
            L("          jsonb_build_object('source', 'scripts/seed.sql', 'blockCode', " + Q(x.BlockCode) + ", 'concept', true, 'apartments', " + rows.Count + "), ts, NULL, ts, ts);");
            L("  RAISE NOTICE 'Создан блок " + spec.Code + ": % помещений', " + rows.Count + ";");
        }
        L("");
        L("  UPDATE \"Organizations\" SET \"SitePlan\" = " + J(RichTowerBlocksSeeder.SitePlan()) + ", \"UpdatedAt\" = ts");
        L("  WHERE \"Id\" = org AND \"SitePlan\" IS NULL;");
        L("");
        L("  -- Прежние дома без буквы блока — с сайта убираются (не удаляются: сделки, графики и платежи остаются как есть).");
        L("  FOR old IN SELECT \"Id\", \"NameRu\" FROM \"Buildings\" WHERE \"OrganizationId\" = org AND \"BlockCode\" IS NULL AND \"IsPublished\" LOOP");
        L("    UPDATE \"Buildings\" SET \"IsPublished\" = false, \"UpdatedAt\" = ts WHERE \"Id\" = old.\"Id\";");
        L("    INSERT INTO \"AuditLog\" (\"Id\", \"UserId\", \"OrganizationId\", \"ImpersonatedById\", \"Entity\", \"EntityId\", \"Action\", \"Before\", \"After\",");
        L("                            \"At\", \"Ip\", \"CreatedAt\", \"UpdatedAt\")");
        L("    VALUES (gen_random_uuid(), NULL, org, NULL, 'Building', old.\"Id\", '" + UnpublishedAction + "', jsonb_build_object('IsPublished', true),");
        L("            jsonb_build_object('IsPublished', false, 'reason', 'ЖК из блоков A/B/C', 'source', 'scripts/seed.sql'), ts, NULL, ts, ts);");
        L("    RAISE NOTICE 'Дом «%» скрыт с сайта (сделки и платежи не тронуты)', old.\"NameRu\";");
        L("  END LOOP;");
        L("END $$;");
        L(End);
        return sb.ToString();
    }
}
