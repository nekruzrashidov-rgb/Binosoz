using System.Globalization;
using FluentValidation;

namespace Sitora.WebApi.Validators;

// Общие настройки FluentValidation. Сообщения всегда на русском (таджикского перевода у библиотеки нет).
// У большинства правил свои тексты, а стандартные (длина строки, значение из перечисления) иначе
// пришли бы с английским именем поля вроде «'Rate To Tjs' должно быть больше '0'».
public static class ValidationSetup
{
    private static readonly Dictionary<string, string> FieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Address"] = "Адрес",
        ["Area"] = "Площадь",
        ["Code"] = "Код",
        ["Comment"] = "Комментарий",
        ["Currency"] = "Валюта",
        ["Date"] = "Дата",
        ["DescriptionRu"] = "Описание на русском",
        ["DescriptionTg"] = "Описание на таджикском",
        ["Discount"] = "Скидка",
        ["DiscountReason"] = "Причина скидки",
        ["DownPaymentPct"] = "Первоначальный взнос",
        ["Email"] = "Почта",
        ["FullName"] = "ФИО",
        ["Method"] = "Способ оплаты",
        ["Model3dId"] = "ID в 3D-модели",
        ["ModelUrl"] = "Ссылка на 3D-модель",
        ["Name"] = "Название",
        ["NameRu"] = "Название на русском",
        ["NameTg"] = "Название на таджикском",
        ["PassportData"] = "Паспортные данные",
        ["Password"] = "Пароль",
        ["Phone"] = "Телефон",
        ["PlanImageUrl"] = "Ссылка на план этажа",
        ["PricePerM2"] = "Цена за м²",
        ["RateToTjs"] = "Курс",
        ["Reason"] = "Причина",
        ["RefreshToken"] = "Refresh-токен",
        ["Role"] = "Роль",
        ["SortOrder"] = "Порядок",
        ["Source"] = "Источник",
        ["Stage"] = "Этап",
        ["Status"] = "Статус",
        ["Text"] = "Сообщение",
    };

    public static void Configure()
    {
        ValidatorOptions.Global.LanguageManager.Culture = CultureInfo.GetCultureInfo("ru");
        ValidatorOptions.Global.DisplayNameResolver = (_, member, _) =>
            member is not null && FieldNames.TryGetValue(member.Name, out var name) ? name : null;
    }
}
