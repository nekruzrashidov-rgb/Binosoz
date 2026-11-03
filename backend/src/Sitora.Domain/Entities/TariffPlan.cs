using Sitora.Domain.Enums;

namespace Sitora.Domain.Entities;

// Тариф платформы для застройщика. MaxBuildings / MaxUsers null — без ограничения.
// Features — список возможностей тарифа, JSON-массив строк (для карточки тарифа).
public class TariffPlan : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public Currency Currency { get; set; } = Currency.TJS;
    public int? MaxBuildings { get; set; }
    public int? MaxUsers { get; set; }
    public string Features { get; set; } = "[]";
    public bool IsActive { get; set; } = true;
}

// Тариф организации на период. Действующий — начался и не закончился (EndsAt null — бессрочно).
// Новый тариф закрывает прежний; история остаётся.
public class OrganizationTariff : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public Guid TariffPlanId { get; set; }
    public TariffPlan TariffPlan { get; set; } = null!;
    public DateTime StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public bool IsPaid { get; set; }

    public bool IsCurrent(DateTime now) => StartsAt <= now && (EndsAt is null || EndsAt > now);
}
