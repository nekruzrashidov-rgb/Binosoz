using Microsoft.EntityFrameworkCore;
using Sitora.Domain.Enums;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;

namespace Sitora.Infrastructure.Services;

// Проверка «чья это сделка» на каждый запрос. Сотрудники продаж и бухгалтерии видят все сделки,
// покупатель — только свои. Для чужой сделки ответ такой же, как для несуществующей (404).
public class DealAccess(AppDbContext db, ICurrentUser currentUser)
{
    public Task<bool> CanViewAsync(Guid dealId, CancellationToken ct)
    {
        if (currentUser.IsDealStaff)
            return db.Deals.AnyAsync(d => d.Id == dealId, ct);

        if (currentUser.IsInRole(RoleNames.Customer) && currentUser.UserId is { } userId)
            return db.Deals.AnyAsync(d => d.Id == dealId && d.Customer.UserId == userId, ct);

        return Task.FromResult(false);
    }
}
