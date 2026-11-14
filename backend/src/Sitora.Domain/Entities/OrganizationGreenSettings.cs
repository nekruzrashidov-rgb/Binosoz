namespace Sitora.Domain.Entities;

// «Зелёные» настройки застройщика: тарифы и коэффициенты, которые нельзя выдумывать — их задаёт руководитель
// с указанием источника. Не задано — соответствующий расчёт на сайте не показывается.
public class OrganizationGreenSettings
{
    // Тариф на электроэнергию, TJS за кВт·ч (ставка, а не сумма: 4 знака после запятой).
    public decimal? TariffKwhTjs { get; set; }
    // Потребление «типового дома» для сравнения, кВт·ч на м² в год, и откуда эта цифра.
    public decimal? BaselineKwhPerM2Year { get; set; }
    public string? BaselineSource { get; set; }
    // Сколько кг CO₂ даёт производство 1 кг бумаги, и источник. Без него CO₂ не считается.
    public decimal? Co2KgPerKgPaper { get; set; }
    public string? Co2Source { get; set; }
    // Данные организации — демонстрационные (сайт показывает бейдж «Демо-данные»).
    public bool IsDemo { get; set; }
}
