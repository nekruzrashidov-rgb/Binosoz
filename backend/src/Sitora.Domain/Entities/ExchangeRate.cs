using Sitora.Domain.Enums;

namespace Sitora.Domain.Entities;

public class ExchangeRate : BaseEntity
{
    // null — общий курс платформы (НБТ); иначе ручной курс бухгалтера этой организации.
    // Ручной курс действует только в своей организации и на свою дату важнее общего.
    public Guid? OrganizationId { get; set; }

    public DateOnly Date { get; set; }
    public Currency Currency { get; set; }

    // Сколько сомони за 1 единицу валюты.
    public decimal RateToTjs { get; set; }
    public RateSource Source { get; set; }
}
