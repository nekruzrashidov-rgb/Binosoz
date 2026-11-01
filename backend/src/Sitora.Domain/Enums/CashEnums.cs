namespace Sitora.Domain.Enums;

public enum CashDirection { Income, Expense }

public enum CashCategory
{
    // Приход
    ApartmentPayment,   // оплата покупателя по сделке
    Rent,               // аренда коммерческих помещений
    OtherIncome,        // прочий приход

    // Расход
    Construction,       // строительные работы
    Materials,          // материалы
    Salaries,           // зарплата сотрудников
    Marketing,          // реклама и маркетинг
    Utilities,          // коммунальные услуги
    Tax,                // налоги
    OtherExpense        // прочий расход
}

public static class CashCategories
{
    public static CashDirection DirectionOf(CashCategory category) =>
        category is CashCategory.ApartmentPayment or CashCategory.Rent or CashCategory.OtherIncome
            ? CashDirection.Income
            : CashDirection.Expense;
}
