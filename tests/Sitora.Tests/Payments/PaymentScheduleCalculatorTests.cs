using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Domain.Exceptions;
using Sitora.Infrastructure.Services;

namespace Sitora.Tests.Payments;

public class PaymentScheduleCalculatorTests
{
    [Fact]
    public void Build_SplitsRestEvenly_AndLastPaymentTakesRoundingRemainder()
    {
        var items = PaymentScheduleCalculator.Build(445100m, 30m, 24, 10, new DateOnly(2026, 1, 15));

        var down = Assert.Single(items, i => i.Kind == ScheduleItemKind.DownPayment);
        Assert.Equal(133530m, down.AmountDue);
        Assert.Equal(new DateOnly(2026, 1, 15), down.DueDate);

        var monthly = items.Where(i => i.Kind == ScheduleItemKind.Monthly).ToList();
        Assert.Equal(24, monthly.Count);
        Assert.All(monthly.SkipLast(1), i => Assert.Equal(12982m, i.AmountDue));
        Assert.Equal(12984m, monthly[^1].AmountDue);
        Assert.Equal(445100m, items.Sum(i => i.AmountDue));
    }

    [Fact]
    public void Build_PutsMonthlyPaymentsOnPaymentDay()
    {
        var items = PaymentScheduleCalculator.Build(120000m, 0m, 3, 28, new DateOnly(2026, 11, 30));

        Assert.DoesNotContain(items, i => i.Kind == ScheduleItemKind.DownPayment);
        Assert.Equal([new DateOnly(2026, 12, 28), new DateOnly(2027, 1, 28), new DateOnly(2027, 2, 28)], items.Select(i => i.DueDate));
        Assert.All(items, i => Assert.Equal(40000m, i.AmountDue));
    }

    [Fact]
    public void Build_RoundsToWholeUnits_EvenWithKopecksInPrice()
    {
        var items = PaymentScheduleCalculator.Build(100000.50m, 15m, 7, 1, new DateOnly(2026, 3, 1));

        Assert.Equal(15000m, items[0].AmountDue);
        Assert.All(items.Skip(1).SkipLast(1), i => Assert.Equal(decimal.Truncate(i.AmountDue), i.AmountDue));
        Assert.Equal(100000.50m, items.Sum(i => i.AmountDue));
    }

    [Theory]
    [InlineData(100, 12, 5)]
    [InlineData(-1, 12, 5)]
    [InlineData(20, 0, 5)]
    [InlineData(20, 361, 5)]
    [InlineData(20, 12, 29)]
    [InlineData(20, 12, 0)]
    public void Build_RejectsInvalidParameters(decimal pct, int months, int day)
    {
        Assert.Throws<BusinessRuleException>(() => PaymentScheduleCalculator.Build(500000m, pct, months, day, new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public void ApplyPayments_ClosesOldestItemsFirst()
    {
        var plan = new PaymentPlan { Items = PaymentScheduleCalculator.Build(100000m, 20m, 4, 5, new DateOnly(2026, 1, 1)) };

        plan.ApplyPayments(30000m);

        var ordered = plan.Items.OrderBy(i => i.DueDate).ToList();
        Assert.Equal(
            [ScheduleItemStatus.Paid, ScheduleItemStatus.PartiallyPaid, ScheduleItemStatus.Pending, ScheduleItemStatus.Pending, ScheduleItemStatus.Pending],
            ordered.Select(i => i.Status));
        Assert.Equal(10000m, ordered[1].AmountPaid);

        plan.ApplyPayments(20000m);
        Assert.Equal(ScheduleItemStatus.Pending, ordered[1].Status);
        Assert.Equal(ScheduleItemStatus.Paid, ordered[0].Status);
    }
}
