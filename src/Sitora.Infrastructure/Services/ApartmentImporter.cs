using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.DTOs.Catalog;
using Sitora.Domain.Entities;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;

namespace Sitora.Infrastructure.Services;

// Формат файла: первый лист, первая строка — заголовки, далее столбцы
// A — этаж, B — номер квартиры, C — комнат (0 — студия), D — площадь, E — цена за м².
// Если в файле есть хоть одна ошибка, ничего не сохраняется.
public class ApartmentImporter(AppDbContext db, IAuditService audit)
{
    private const int MaxRooms = 10;
    private const decimal MaxArea = 1000m;

    private record Row(int RowNumber, int Floor, int Number, int Rooms, decimal Area, decimal PricePerM2);

    public async Task<ImportResult> ImportAsync(Guid buildingId, Stream file, CancellationToken ct)
    {
        var building = await db.Buildings
            .Include(b => b.Floors).ThenInclude(f => f.Apartments)
            .FirstOrDefaultAsync(b => b.Id == buildingId, ct)
            ?? throw new NotFoundException("Дом не найден");

        var errors = new List<ImportError>();
        var rows = ReadRows(file, building.FloorsCount, errors);

        foreach (var duplicate in rows.GroupBy(r => (r.Floor, r.Number)).Where(g => g.Count() > 1))
        {
            foreach (var row in duplicate.Skip(1))
                errors.Add(new ImportError(row.RowNumber, $"Квартира №{row.Number} на этаже {row.Floor} уже есть выше в файле"));
        }

        if (errors.Count > 0)
            return new ImportResult(0, 0, errors.OrderBy(e => e.Row).ToList());
        if (rows.Count == 0)
            throw new BusinessRuleException("В файле нет строк с квартирами");

        var created = 0;
        var updated = 0;
        foreach (var row in rows)
        {
            var floor = building.Floors.FirstOrDefault(f => f.Number == row.Floor);
            if (floor is null)
            {
                floor = new Floor { BuildingId = building.Id, Number = row.Floor };
                building.Floors.Add(floor);
                db.Floors.Add(floor);
            }

            var apartment = floor.Apartments.FirstOrDefault(a => a.Number == row.Number);
            if (apartment is null)
            {
                apartment = new Apartment { FloorId = floor.Id, Number = row.Number, Rooms = row.Rooms };
                apartment.SetPricing(row.Area, row.PricePerM2);
                floor.Apartments.Add(apartment);
                db.Apartments.Add(apartment);
                audit.Record(nameof(Apartment), apartment.Id, "Imported", null, Snapshot(apartment));
                created++;
            }
            else
            {
                var before = Snapshot(apartment);
                apartment.Rooms = row.Rooms;
                apartment.SetPricing(row.Area, row.PricePerM2);
                if (db.Entry(apartment).State == EntityState.Modified)
                {
                    audit.Record(nameof(Apartment), apartment.Id, "Imported", before, Snapshot(apartment));
                    updated++;
                }
            }
        }

        await db.SaveChangesAsync(ct);
        return new ImportResult(created, updated, []);
    }

    private static List<Row> ReadRows(Stream file, int floorsCount, List<ImportError> errors)
    {
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(file);
        }
        catch (Exception)
        {
            throw new BusinessRuleException("Не удалось прочитать файл. Нужен файл Excel в формате .xlsx");
        }

        using (workbook)
        {
            var rows = new List<Row>();
            foreach (var xlRow in workbook.Worksheets.First().RowsUsed().Skip(1))
            {
                var rowNumber = xlRow.RowNumber();
                var rowErrors = new List<string>();

                var floor = ReadInt(xlRow.Cell(1), "этаж", rowErrors);
                var number = ReadInt(xlRow.Cell(2), "номер квартиры", rowErrors);
                var rooms = ReadInt(xlRow.Cell(3), "комнат", rowErrors);
                var area = ReadDecimal(xlRow.Cell(4), "площадь", rowErrors);
                var price = ReadDecimal(xlRow.Cell(5), "цена за м²", rowErrors);

                if (floor is < 0 || floor > floorsCount)
                    rowErrors.Add($"этаж должен быть от 0 до {floorsCount}");
                if (number is <= 0)
                    rowErrors.Add("номер квартиры должен быть больше нуля");
                if (rooms is < 0 or > MaxRooms)
                    rowErrors.Add($"комнат должно быть от 0 до {MaxRooms}");
                if (area is <= 0 or > MaxArea)
                    rowErrors.Add($"площадь должна быть больше 0 и не больше {MaxArea}");
                if (price is <= 0)
                    rowErrors.Add("цена за м² должна быть больше нуля");

                if (rowErrors.Count > 0)
                    errors.Add(new ImportError(rowNumber, string.Join("; ", rowErrors)));
                else
                    rows.Add(new Row(rowNumber, floor!.Value, number!.Value, rooms!.Value, area!.Value, price!.Value));
            }

            return rows;
        }
    }

    private static int? ReadInt(IXLCell cell, string column, List<string> errors)
    {
        if (cell.TryGetValue(out double value) && value == Math.Floor(value))
            return (int)value;
        errors.Add($"столбец «{column}» должен быть целым числом");
        return null;
    }

    private static decimal? ReadDecimal(IXLCell cell, string column, List<string> errors)
    {
        if (cell.TryGetValue(out double value))
            return Math.Round((decimal)value, 2);
        errors.Add($"столбец «{column}» должен быть числом");
        return null;
    }

    private static object Snapshot(Apartment a) => new { a.Number, a.Rooms, a.Area, a.PricePerM2, a.Price };
}
