using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.Entities;
using Sitora.Domain.Interfaces;

namespace Sitora.Infrastructure.Data.Seed;

// Пароль учётной записи сидера приводится к значению из секрета (env, GitHub Secrets, user-secrets): владелец забыл
// пароль — меняет секрет и снова запускает --migrate. В одной транзакции: новый пароль, запись в AuditLog и отзыв всех
// refresh-токенов (на других устройствах сессии заканчиваются). Значение пароля никуда не пишется — ни в лог, ни в ошибки.
public static class SeedPasswords
{
    public const string PasswordSetAction = "PasswordSetFromSeedSecret";
    public const string CreatedAction = "CreatedFromSeedSecret";

    // true — пароль изменён; false — уже совпадал с секретом.
    public static async Task<bool> SyncAsync(AppDbContext db, UserManager<AppUser> users, IAuditService audit,
        TimeProvider timeProvider, AppUser user, string password, string secretName)
    {
        if (await users.CheckPasswordAsync(user, password))
            return false;

        // Сначала правила Identity: при слабом пароле старый пароль не снимается.
        var errors = new List<IdentityError>();
        foreach (var validator in users.PasswordValidators)
        {
            var check = await validator.ValidateAsync(users, user, password);
            if (!check.Succeeded)
                errors.AddRange(check.Errors);
        }
        if (errors.Count > 0)
            throw new InvalidOperationException($"Пароль из секрета {secretName} для {user.Email} не подходит: " + Describe(errors));

        await using var transaction = await db.Database.BeginTransactionAsync();
        var removed = await users.RemovePasswordAsync(user);
        var added = removed.Succeeded ? await users.AddPasswordAsync(user, password) : removed;
        if (!added.Succeeded)
            throw new InvalidOperationException($"Не удалось задать пароль из секрета {secretName} для {user.Email}: " + Describe(added.Errors));

        var now = timeProvider.GetUtcNow().UtcDateTime;
        await db.RefreshTokens.Where(t => t.UserId == user.Id && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now));
        audit.Record(nameof(AppUser), user.Id, PasswordSetAction, null,
            new { user.Email, Secret = secretName, SessionsRevoked = true }, user.OrganizationId);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return true;
    }

    public static string Describe(IEnumerable<IdentityError> errors) => string.Join("; ", errors.Select(e => e.Description));
}
