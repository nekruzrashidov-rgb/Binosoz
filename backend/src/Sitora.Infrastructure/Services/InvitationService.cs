using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.Common;
using Sitora.Domain.DTOs.Auth;
using Sitora.Domain.Entities;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;
using Sitora.Infrastructure.Security;

namespace Sitora.Infrastructure.Services;

// Приглашение сотрудника. Свободной регистрации сотрудников нет: учётную запись сразу заводит руководитель,
// а логин и сгенерированный пароль уходят сотруднику письмом. Запись приглашения остаётся для истории (уже принятой).
public class InvitationService(
    AppDbContext db,
    UserManager<AppUser> userManager,
    ICurrentUser currentUser,
    StaffPolicy policy,
    StaffAccounts accounts,
    IAuditService audit,
    TimeProvider timeProvider) : IInvitationService
{
    public async Task<InvitationResponse> CreateAsync(CreateInvitationRequest request, CancellationToken ct = default)
    {
        var requested = request.BuildingIds?.ToList() ?? [];
        if (request.BuildingId is { } single && !requested.Contains(single))
            requested.Add(single);
        var buildings = await policy.ValidateAsync(request.Role, requested, ct);
        var organizationId = policy.RequireOrganization();
        var address = EmailAddress.Normalize(request.Email) ?? throw new BusinessRuleException("Неверный адрес почты");
        var fullName = $"{request.FirstName?.Trim()} {request.LastName?.Trim()}".Trim();
        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName) || fullName.Length > 200)
            throw new BusinessRuleException("Укажите имя и фамилию (вместе до 200 символов)");
        if (await userManager.FindByEmailAsync(address) is not null)
            throw new ConflictException("Пользователь с такой почтой уже есть");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        // Ссылки из прежних приглашений на ту же почту больше не действуют.
        var previous = await db.Invitations
            .Where(i => i.Email == address && i.AcceptedAt == null && i.RevokedAt == null)
            .ToListAsync(ct);
        previous.ForEach(i => i.RevokedAt = now);

        var (user, password, _) = await accounts.CreateAsync(address, fullName, request.Role, organizationId, null, null);
        foreach (var buildingId in buildings)
            db.UserBuildings.Add(new UserBuilding { UserId = user.Id, BuildingId = buildingId });

        var invitation = new Invitation
        {
            OrganizationId = organizationId,
            Email = address,
            FullName = fullName,
            Role = request.Role,
            BuildingIds = buildings,
            // Ссылки нет: учётная запись уже создана. Хеш случайный, чтобы по нему ничего нельзя было принять.
            TokenHash = JwtTokenService.HashToken(Convert.ToHexString(RandomNumberGenerator.GetBytes(32))),
            ExpiresAt = now,
            AcceptedAt = now,
            InvitedById = currentUser.UserId ?? throw new UnauthorizedException("Требуется вход")
        };
        db.Invitations.Add(invitation);
        audit.Record(nameof(Invitation), invitation.Id, "Created", null,
            new { invitation.Email, invitation.Role, invitation.BuildingIds, UserId = user.Id });
        await db.SaveChangesAsync(ct);

        var organizationName = await db.Organizations.Where(o => o.Id == organizationId).Select(o => o.Name).SingleAsync(ct);
        await accounts.SendCredentialsAsync(user, password, organizationName, ct);
        return ToResponse(invitation);
    }

    public async Task<IReadOnlyList<InvitationResponse>> GetPendingAsync(CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var query = db.Invitations.AsNoTracking().Where(i => i.AcceptedAt == null && i.RevokedAt == null && i.ExpiresAt > now);
        var list = await query.OrderByDescending(i => i.CreatedAt).ToListAsync(ct);
        // Админ объекта видит приглашения в свой дом.
        if (currentUser.BuildingIds is { } own)
            list = list.Where(i => i.BuildingIds.Any(own.Contains)).ToList();
        return list.Select(ToResponse).ToList();
    }

    public async Task RevokeAsync(Guid id, CancellationToken ct = default)
    {
        var invitation = await db.Invitations.FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new NotFoundException("Приглашение не найдено");
        if (currentUser.BuildingIds is { } own && !invitation.BuildingIds.Any(own.Contains))
            throw new NotFoundException("Приглашение не найдено");
        if (invitation.AcceptedAt is not null)
            throw new BusinessRuleException("Приглашение уже принято");

        invitation.RevokedAt = timeProvider.GetUtcNow().UtcDateTime;
        audit.Record(nameof(Invitation), invitation.Id, "Revoked", null, new { invitation.Email });
        await db.SaveChangesAsync(ct);
    }

    private static InvitationResponse ToResponse(Invitation i) =>
        new(i.Id, i.Email, i.FullName, i.Role, i.BuildingIds, i.ExpiresAt, i.AcceptedAt, i.CreatedAt);
}
