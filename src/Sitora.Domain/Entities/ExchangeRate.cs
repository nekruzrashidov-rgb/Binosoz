using Sitora.Domain.Enums;

namespace Sitora.Domain.Entities;

public class ExchangeRate : BaseEntity
{
    public DateOnly Date { get; set; }
    public Currency Currency { get; set; }

    // Сколько сомони за 1 единицу валюты.
    public decimal RateToTjs { get; set; }
    public RateSource Source { get; set; }
}
