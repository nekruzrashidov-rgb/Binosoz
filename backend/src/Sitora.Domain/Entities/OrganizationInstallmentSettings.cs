namespace Sitora.Domain.Entities;

// Условия рассрочки застройщика для сайта (столбцы Installment* в Organizations). Пустое поле — «не задано»: сайт не пишет
// это условие. Задаёт владелец в настройках CRM (PUT /owner/installment-settings).
public class OrganizationInstallmentSettings
{
    // Минимальный первоначальный взнос, % от цены квартиры.
    public decimal? DownPaymentMinPct { get; set; }

    // Максимальный срок рассрочки, месяцев.
    public int? MaxMonths { get; set; }

    // Рассрочка без переплаты: график делит остаток поровну, проценты не начисляются (так считает PaymentScheduleCalculator).
    // Сайт пишет «без переплаты / 0%» только когда застройщик это подтвердил.
    public bool NoOverpayment { get; set; }

    public bool IsEmpty => DownPaymentMinPct is null && MaxMonths is null && !NoOverpayment;
}
