using Sitora.Domain.Enums;
using Sitora.Domain.Exceptions;

namespace Sitora.Domain.Entities;

public class Deal : BaseEntity
{
    public Guid ApartmentId { get; set; }
    public Apartment Apartment { get; set; } = null!;
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public Guid ManagerId { get; set; }
    public AppUser Manager { get; set; } = null!;
    public Guid? BookingId { get; set; }
    public Booking? Booking { get; set; }

    public DealStage Stage { get; private set; } = DealStage.Lead;
    public string? StageReason { get; private set; }

    // Цена копируется из квартиры при создании и дальше от прайса не зависит.
    public decimal Price { get; private set; }
    public decimal Discount { get; private set; }
    public string? DiscountReason { get; private set; }
    public Currency Currency { get; private set; } = Currency.TJS;

    public decimal FinalPrice => Price - Discount;
    public bool IsClosed => Stage is DealStage.Completed or DealStage.Cancelled;

    // Квартира удерживается сделкой с этапа «Бронь», продана — с этапа «Договор».
    public static bool HoldsApartment(DealStage stage) => stage is >= DealStage.Booking and <= DealStage.Completed;
    public static bool SellsApartment(DealStage stage) => stage is >= DealStage.Contract and <= DealStage.Completed;

    public void SetPrice(decimal price, Currency currency, decimal discount, string? discountReason)
    {
        if (discount < 0 || discount >= price)
            throw new BusinessRuleException("Скидка должна быть не меньше нуля и меньше цены");
        if (discount > 0 && string.IsNullOrWhiteSpace(discountReason))
            throw new BusinessRuleException("Укажите причину скидки");

        Price = price;
        Currency = currency;
        Discount = discount;
        DiscountReason = discount > 0 ? discountReason!.Trim() : null;
    }

    public void ChangeStage(DealStage target, string? reason, Apartment apartment)
    {
        if (IsClosed)
            throw new BusinessRuleException("Сделка закрыта, этап менять нельзя");
        if (target == Stage)
            throw new BusinessRuleException("Сделка уже на этом этапе");

        var backward = target == DealStage.Cancelled || target < Stage;
        if (backward && string.IsNullOrWhiteSpace(reason))
            throw new BusinessRuleException("Для возврата сделки на прошлый этап или отмены укажите причину");

        ApplyApartmentStatus(target, apartment);
        Stage = target;
        StageReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }

    private void ApplyApartmentStatus(DealStage target, Apartment apartment)
    {
        var wasHeld = HoldsApartment(Stage);

        if (SellsApartment(target))
        {
            if (apartment.Status != ApartmentStatus.Sold)
                apartment.Sell();
        }
        else if (HoldsApartment(target))
        {
            if (apartment.Status == ApartmentStatus.Sold)
                apartment.RevertSale();
            else if (apartment.Status != ApartmentStatus.Booked)
                apartment.Book();
        }
        else if (wasHeld)
        {
            if (apartment.Status == ApartmentStatus.Sold)
                apartment.RevertSale();
            apartment.ReleaseBooking();
        }
    }
}
