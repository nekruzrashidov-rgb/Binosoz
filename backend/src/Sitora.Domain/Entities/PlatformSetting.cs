namespace Sitora.Domain.Entities;

// Настройка платформы «ключ — значение» (раздел суперадмина «Настройки платформы»).
// Ключи — в PlatformSettingKeys; значение хранится строкой, тип знает сервис.
public class PlatformSetting
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}

public static class PlatformSettingKeys
{
    // Сколько организаций может быть на платформе; пусто — без ограничения.
    public const string MaxOrganizations = "MaxOrganizations";

    // Демо-режим платформы: только флаг для интерфейса, данные не меняет.
    public const string DemoMode = "DemoMode";

    // Почта для связи с платформой (в письмах и на странице заявки застройщика).
    public const string ContactEmail = "ContactEmail";
}
