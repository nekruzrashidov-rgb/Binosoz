using System.ComponentModel.DataAnnotations;

namespace Sitora.Infrastructure.Security;

// Способы входа и лимиты кодов (раздел Auth; переменные Auth__*).
public class AuthOptions
{
    public const string SectionName = "Auth";

    // Старый вход покупателей по SMS: выключен, пока не подключён SMS-провайдер.
    public bool SmsLoginEnabled { get; set; }

    // Вход сотрудников по паролю (для создателя платформы закрыт всегда: только почта и второй фактор).
    public bool PasswordLoginEnabled { get; set; } = true;

    // Не чаще одной отправки кода в минуту и пяти в час — на почту и на IP.
    [Range(1, 100)]
    public int EmailCodesPerMinute { get; set; } = 1;

    [Range(1, 1000)]
    public int EmailCodesPerHour { get; set; } = 5;
}

// Чей это токен и что ему видно. OrganizationId null — без ограничения организацией.
public record TokenScope(Guid? OrganizationId, IReadOnlyList<Guid> BuildingIds, Guid? ImpersonatedBy = null);
