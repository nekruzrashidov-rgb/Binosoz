using Sitora.Domain.Enums;
using Sitora.Domain.Exceptions;

namespace Sitora.Domain.Entities;

public class Apartment : BaseEntity
{
    public Guid FloorId { get; set; }
    public Floor Floor { get; set; } = null!;

    public int Number { get; set; }

    // 0 — студия.
    public int Rooms { get; set; }
    public decimal Area { get; private set; }
    public decimal PricePerM2 { get; private set; }
    public decimal Price { get; private set; }
    public Currency Currency { get; private set; } = Currency.TJS;

    // Статус меняется только методами ниже: их вызывают операции брони, сделки и каталога.
    public ApartmentStatus Status { get; private set; } = ApartmentStatus.Free;

    public static decimal CalculatePrice(decimal area, decimal pricePerM2) =>
        Math.Round(area * pricePerM2 / 100m, 0, MidpointRounding.AwayFromZero) * 100m;

    public void SetPricing(decimal area, decimal pricePerM2)
    {
        if (area <= 0)
            throw new BusinessRuleException("Площадь должна быть больше нуля");
        if (pricePerM2 <= 0)
            throw new BusinessRuleException("Цена за м² должна быть больше нуля");

        Area = area;
        PricePerM2 = pricePerM2;
        Price = CalculatePrice(area, pricePerM2);
    }

    public void SetOnRequest(bool onRequest)
    {
        if (onRequest)
        {
            EnsureStatus(ApartmentStatus.Free, "Под запрос можно перевести только свободную квартиру");
            Status = ApartmentStatus.OnRequest;
        }
        else
        {
            EnsureStatus(ApartmentStatus.OnRequest, "Квартира не находится в статусе «под запрос»");
            Status = ApartmentStatus.Free;
        }
    }

    public void Book()
    {
        EnsureStatus(ApartmentStatus.Free, "Квартира недоступна для брони");
        Status = ApartmentStatus.Booked;
    }

    public void ReleaseBooking()
    {
        EnsureStatus(ApartmentStatus.Booked, "Квартира не забронирована");
        Status = ApartmentStatus.Free;
    }

    public void Sell()
    {
        if (Status is not (ApartmentStatus.Free or ApartmentStatus.Booked))
            throw new BusinessRuleException("Квартиру в текущем статусе нельзя продать");
        Status = ApartmentStatus.Sold;
    }

    private void EnsureStatus(ApartmentStatus expected, string message)
    {
        if (Status != expected)
            throw new BusinessRuleException(message);
    }
}
