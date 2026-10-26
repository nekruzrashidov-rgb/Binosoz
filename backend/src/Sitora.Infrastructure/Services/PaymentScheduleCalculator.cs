using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Domain.Exceptions;

namespace Sitora.Infrastructure.Services;

public static class PaymentScheduleCalculator
{
    public const int MaxMonths = 360;
    // До 28-го, чтобы день платежа был в каждом месяце.
    public const int MaxPaymentDay = 28;

    // Взнос = цена × процент, месячный платёж = (цена − взнос) ÷ месяцы; оба округляются
    // до целой единицы валюты, последний месячный платёж забирает остаток округлений.
    public static List<PaymentScheduleItem> Build(decimal total, decimal downPaymentPct, int months, int paymentDay, DateOnly startDate)
    {
        if (total <= 0)
            throw new BusinessRuleException("Сумма договора должна быть больше нуля");
        if (downPaymentPct is < 0 or >= 100)
            throw new BusinessRuleException("Первоначальный взнос должен быть от 0 до 100% (не включая 100)");
        if (months is < 1 or > MaxMonths)
            throw new BusinessRuleException($"Срок должен быть от 1 до {MaxMonths} месяцев");
        if (paymentDay is < 1 or > MaxPaymentDay)
            throw new BusinessRuleException($"День платежа должен быть от 1 до {MaxPaymentDay}");

        var downPayment = Math.Round(total * downPaymentPct / 100m, 0, MidpointRounding.AwayFromZero);
        var rest = total - downPayment;
        var monthly = Math.Round(rest / months, 0, MidpointRounding.AwayFromZero);
        var last = rest - monthly * (months - 1);
        if (monthly <= 0 || last <= 0)
            throw new BusinessRuleException("Срок слишком большой для такой суммы");

        var items = new List<PaymentScheduleItem>();
        if (downPayment > 0)
            items.Add(new PaymentScheduleItem { Kind = ScheduleItemKind.DownPayment, DueDate = startDate, AmountDue = downPayment });

        for (var i = 1; i <= months; i++)
        {
            var month = startDate.AddMonths(i);
            items.Add(new PaymentScheduleItem
            {
                Kind = ScheduleItemKind.Monthly,
                DueDate = new DateOnly(month.Year, month.Month, paymentDay),
                AmountDue = i == months ? last : monthly
            });
        }

        return items;
    }
}
