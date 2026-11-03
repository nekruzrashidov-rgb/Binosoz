using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sitora.Domain.Entities;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Integrations;

namespace Sitora.Infrastructure.Services;

// Учётная запись сотрудника с паролем: сотрудники входят только по паролю. Если пароль не задали,
// он генерируется (8 символов, буквы и цифры) и уходит сотруднику письмом вместе с логином.
public class StaffAccounts(
    UserManager<AppUser> userManager,
    IEmailSender email,
    IOptions<EmailOptions> emailOptions,
    ILogger<StaffAccounts> logger)
{
    public const int GeneratedPasswordLength = 8;

    // Без похожих символов (0/O, 1/l/I): пароль переписывают из письма руками.
    private const string Lower = "abcdefghijkmnpqrstuvwxyz";
    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Digits = "23456789";

    // Есть строчная, заглавная и цифра — как требуют правила паролей Identity.
    public static string GeneratePassword()
    {
        var chars = new char[GeneratedPasswordLength];
        chars[0] = Pick(Lower);
        chars[1] = Pick(Upper);
        chars[2] = Pick(Digits);
        for (var i = 3; i < chars.Length; i++)
            chars[i] = Pick(Lower + Upper + Digits);
        RandomNumberGenerator.Shuffle(chars.AsSpan());
        return new string(chars);

        static char Pick(string alphabet) => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
    }

    // Создаёт сотрудника с ролью. Generated = true — пароль придуман здесь, и его нужно отправить письмом.
    public async Task<(AppUser User, string Password, bool Generated)> CreateAsync(
        string address, string fullName, string role, Guid? organizationId, string? phone, string? password)
    {
        var generated = string.IsNullOrEmpty(password);
        var actual = generated ? GeneratePassword() : password!;
        var user = new AppUser
        {
            UserName = address,
            Email = address,
            EmailConfirmed = true,
            FullName = fullName,
            PhoneNumber = phone,
            OrganizationId = organizationId
        };
        var result = await userManager.CreateAsync(user, actual);
        if (!result.Succeeded)
            throw new BusinessRuleException(string.Join("; ", result.Errors.Select(e => e.Description)));
        await userManager.AddToRoleAsync(user, role);
        return (user, actual, generated);
    }

    // Новый пароль, выданный суперадмином: письмо с логином и паролем. Сбой почты не откатывает смену —
    // сотрудник всегда может задать пароль сам через «Забыли пароль?».
    public async Task SendNewPasswordAsync(AppUser user, string password, CancellationToken ct)
    {
        try
        {
            await email.SendAsync(new EmailMessage(user.Email!, "Binosoz: новый пароль",
                $"Здравствуйте, {user.FullName}!\n\nАдминистратор платформы задал вам новый пароль.\n\n" +
                $"Страница входа: {emailOptions.Value.LoginUrl}\nЛогин: {user.Email}\nПароль: {password}\n\n" +
                "Прежний пароль больше не действует, все сессии завершены. Сменить пароль можно через «Забыли пароль?»."), ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogError(e, "Письмо с новым паролем пользователю {UserId} не отправлено", user.Id);
        }
    }

    // Письмо «Вы приглашены в Binosoz» с логином и паролем. Сбой почты не отменяет создание:
    // сотрудник задаст новый пароль через «Забыли пароль?».
    public async Task SendCredentialsAsync(AppUser user, string password, string? organizationName, CancellationToken ct)
    {
        var where = string.IsNullOrEmpty(organizationName) ? "Binosoz" : $"«{organizationName}» на платформе Binosoz";
        try
        {
            await email.SendAsync(new EmailMessage(user.Email!, "Вы приглашены в Binosoz",
                $"Здравствуйте, {user.FullName}!\n\nВас пригласили в {where}.\n\n" +
                $"Страница входа: {emailOptions.Value.LoginUrl}\nЛогин: {user.Email}\nПароль: {password}\n\n" +
                "Входите на вкладке «Сотрудник». Пароль никому не сообщайте; сменить его можно через «Забыли пароль?»."), ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogError(e, "Письмо с паролем сотруднику {UserId} не отправлено", user.Id);
        }
    }
}
