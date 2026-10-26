using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sitora.Domain.Common;
using Sitora.Domain.DTOs.Auth;
using Sitora.Domain.Entities;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;
using Sitora.Infrastructure.Integrations;
using Sitora.Infrastructure.Security;

namespace Sitora.Infrastructure.Services;

// Приглашение сотрудника по почте. Свободной регистрации сотрудников нет: учётная запись появляется,
// только когда человек откроет ссылку из письма. Токен хранится в базе только в виде хеша.
public class InvitationService(
    AppDbContext db,
    UserManager<AppUser> userManager,
    ICurrentUser currentUser,
    StaffPolicy policy,
    IEmailSender email,
    IAuditService audit,
    IOptions<EmailOptions> emailOptions,
    TimeProvider timeProvider) : IInvitationService
{
    public async Task<InvitationResponse> CreateAsync(CreateInvitationRequest request, CancellationToken ct = default)
    {
        var buildings = await policy.ValidateAsync(request.Role, request.BuildingIds, ct);
        var organizationId = policy.RequireOrganization();
        var address = EmailAddress.Normalize(request.Email) ?? throw new BusinessRuleException("Неверный адрес почты");
        if (await userManager.FindByEmailAsync(address) is not null)
            throw new ConflictException("Пользователь с такой почтой уже есть");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        // Новое приглашение на ту же почту заменяет прежнее.
        var previous = await db.Invitations
            .Where(i => i.Email == address && i.AcceptedAt == null && i.RevokedAt == null)
            .ToListAsync(ct);
        previous.ForEach(i => i.RevokedAt = now);

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var organizationName = await db.Organizations.Where(o => o.Id == organizationId).Select(o => o.Name).SingleAsync(ct);
        var invitation = new Invitation
        {
            OrganizationId = organizationId,
            Email = address,
            FullName = request.FullName.Trim(),
            Role = request.Role,
            BuildingIds = buildings,
            TokenHash = JwtTokenService.HashToken(token),
            ExpiresAt = now.Add(Invitation.Lifetime),
            InvitedById = currentUser.UserId ?? throw new UnauthorizedException("Требуется вход")
        };
        db.Invitations.Add(invitation);
        audit.Record(nameof(Invitation), invitation.Id, "Created", null,
            new { invitation.Email, invitation.Role, invitation.BuildingIds });
        await db.SaveChangesAsync(ct);

        await email.SendAsync(new EmailMessage(address, $"Приглашение в Sitora: {organizationName}",
            $"Здравствуйте, {invitation.FullName}!\n\nВас пригласили в «{organizationName}» на платформе Sitora. " +
            $"Чтобы принять приглашение, откройте ссылку:\n{emailOptions.Value.InviteUrl}{token}\n\n" +
            $"Ссылка действует {Invitation.Lifetime.TotalDays:0} дней. Дальше входите по коду, который придёт на эту почту."), ct);

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
