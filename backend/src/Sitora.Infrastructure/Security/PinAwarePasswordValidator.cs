using Microsoft.AspNetCore.Identity;
using Sitora.Domain.Common;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Infrastructure.Data.Seed;

namespace Sitora.Infrastructure.Security;

// Единственный проверяющий пароли Identity. Покупатель (роль Customer) входит по своему 6-значному коду —
// для него правила кода (PinRules); у остальных — стандартные правила пароля сотрудника (длина, цифра, строчная и
// заглавная буквы из IdentityOptions), которые здесь не ослабляются. Демо-покупатель для жюри входит паролем
// (Auth:DemoPasswordLogin) — к нему тоже правила пароля.
// Роль смотрим у сохранённой учётной записи: покупателя сначала создают без пароля, добавляют роль, затем задают код.
public class PinAwarePasswordValidator(IdentityErrorDescriber describer) : IPasswordValidator<AppUser>
{
    public const string WeakPinCode = "WeakPin";

    private readonly PasswordValidator<AppUser> _staff = new(describer);

    public async Task<IdentityResult> ValidateAsync(UserManager<AppUser> manager, AppUser user, string? password)
    {
        if (!await UsesPinAsync(manager, user))
            return await _staff.ValidateAsync(manager, user, password);

        return PinRules.Check(password) is { } error
            ? IdentityResult.Failed(new IdentityError { Code = WeakPinCode, Description = error })
            : IdentityResult.Success;
    }

    private static async Task<bool> UsesPinAsync(UserManager<AppUser> manager, AppUser user)
    {
        if (string.Equals(user.Email, DemoAccountsSeeder.BuyerEmail, StringComparison.OrdinalIgnoreCase))
            return false;
        if (user.Id == Guid.Empty || await manager.FindByIdAsync(user.Id.ToString()) is null)
            return false;
        var roles = await manager.GetRolesAsync(user);
        return roles.Count > 0 && roles.All(r => r == RoleNames.Customer);
    }
}
