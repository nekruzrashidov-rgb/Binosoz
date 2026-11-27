namespace Sitora.Domain.Enums;

public enum ApartmentStatus
{
    Free,
    Booked,
    Sold,
    OnRequest
}

// Вид помещения: квартира, пентхаус (квартира последнего этажа), коммерческое помещение (магазин 1–2 этажа), машино-место
// подземного паркинга (−1/−2). Бронь, продажа и оплата — по одним правилам; «квартиры» в счётчиках — Residential и Penthouse.
public enum ApartmentKind
{
    Residential,
    Penthouse,
    Commercial,
    Parking
}

public enum Currency
{
    TJS,
    USD
}
