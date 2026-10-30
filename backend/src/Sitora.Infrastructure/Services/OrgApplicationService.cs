using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sitora.Domain.Common;
using Sitora.Domain.DTOs.Auth;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;
using Sitora.Infrastructure.Integrations;

namespace Sitora.Infrastructure.Services;

// Заявки застройщиков: подача с публичной страницы, рассмотрение создателем платформы.
// Одобрение создаёт организацию с владельцем через PlatformService — он же отправляет владельцу письмо со входом.
public class OrgApplicationService(
    AppDbContext db,
    IPlatformService platform,
    ICurrentUser currentUser,
    IAuditService audit,
    IEmailSender email,
    TimeProvider timeProvider,
    ILogger<OrgApplicationService> logger) : IOrgApplicationService
{
    private static readonly TimeSpan RepeatWindow = TimeSpan.FromHours(24);
    public const string AcceptedMessage = "Заявка принята. Мы свяжемся с вами в ближайшее время.";

    public async Task<SubmitApplicationResponse> SubmitAsync(SubmitApplicationRequest request, CancellationToken ct = default)
    {
        var contactEmail = EmailAddress.Normalize(request.ContactEmail) ?? throw new BusinessRuleException("Неверный адрес почты");
        var phone = PhoneNumber.Normalize(request.ContactPhone) ?? throw new BusinessRuleException("Неверный номер телефона");

        var since = timeProvider.GetUtcNow().UtcDateTime - RepeatWindow;
        if (await db.OrgApplications.AnyAsync(a => a.ContactEmail == contactEmail && a.Status == OrgApplicationStatus.Pending && a.CreatedAt > since, ct))
            throw new ConflictException("Заявка с этой почты уже на рассмотрении. Мы свяжемся с вами");

        var application = new OrgApplication
        {
            CompanyName = request.CompanyName.Trim(),
            ContactEmail = contactEmail,
            ContactPhone = phone,
            OwnerFullName = request.OwnerFullName.Trim(),
            Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim()
        };
        db.OrgApplications.Add(application);
        audit.Record(nameof(OrgApplication), application.Id, "Created", null,
            new { application.CompanyName, application.ContactEmail, application.OwnerFullName });
        await db.SaveChangesAsync(ct);

        logger.LogInformation("New org application from {CompanyName} <{ContactEmail}>, owner: {OwnerFullName}",
            application.CompanyName, application.ContactEmail, application.OwnerFullName);

        // Заявка уже сохранена: сбой почты её не отменяет. В разработке письмо только пишется в лог.
        await TrySendAsync(EmailTemplates.ApplicationReceived(contactEmail, application.OwnerFullName, application.CompanyName), ct);

        return new SubmitApplicationResponse(application.Id, AcceptedMessage);
    }

    public async Task<PagedResult<OrgApplicationResponse>> GetAsync(OrgApplicationStatus? status, PageRequest page, CancellationToken ct = default)
    {
        var query = db.OrgApplications.AsNoTracking();
        if (status is { } s)
            query = query.Where(a => a.Status == s);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(a => a.CreatedAt)
            .Skip(page.Skip).Take(page.PageSize)
            .Select(a => new OrgApplicationResponse(a.Id, a.CompanyName, a.ContactEmail, a.ContactPhone, a.OwnerFullName, a.Comment,
                a.Status, a.ReviewNote, a.CreatedAt, a.ReviewedAt, a.OrganizationId))
            .ToListAsync(ct);
        return new PagedResult<OrgApplicationResponse>(items, page.Page, page.PageSize, total);
    }

    public async Task<ApproveApplicationResponse> ApproveAsync(Guid id, ApproveApplicationRequest request, CancellationToken ct = default)
    {
        var application = await LoadPendingAsync(id, ct);

        // Организация, её владелец и решение по заявке — вместе или никак.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var organization = await platform.CreateOrganizationAsync(
            new CreateOrganizationRequest(application.CompanyName, request.OrganizationSlug, application.ContactEmail, application.OwnerFullName), ct);

        application.Status = OrgApplicationStatus.Approved;
        application.OrganizationId = organization.Id;
        MarkReviewed(application);
        audit.Record(nameof(OrgApplication), application.Id, "Approved", new { Status = OrgApplicationStatus.Pending },
            new { application.Status, OrganizationId = organization.Id, organization.Slug }, organization.Id);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        logger.LogInformation("Заявка {ApplicationId} одобрена: организация {OrganizationId} ({Slug})", application.Id, organization.Id, organization.Slug);
        return new ApproveApplicationResponse(organization.Id, "Организация создана, приглашение отправлено на почту");
    }

    public async Task RejectAsync(Guid id, RejectApplicationRequest request, CancellationToken ct = default)
    {
        var application = await LoadPendingAsync(id, ct);
        application.Status = OrgApplicationStatus.Rejected;
        application.ReviewNote = request.Reason.Trim();
        MarkReviewed(application);
        audit.Record(nameof(OrgApplication), application.Id, "Rejected", new { Status = OrgApplicationStatus.Pending },
            new { application.Status, application.ReviewNote });
        await db.SaveChangesAsync(ct);

        await TrySendAsync(new EmailMessage(application.ContactEmail, "Sitora: решение по заявке",
            $"Здравствуйте, {application.OwnerFullName}!\n\nК сожалению, заявку компании «{application.CompanyName}» мы пока не можем одобрить.\n" +
            $"Причина: {application.ReviewNote}"), ct);
    }

    private async Task<OrgApplication> LoadPendingAsync(Guid id, CancellationToken ct)
    {
        var application = await db.OrgApplications.FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException("Заявка не найдена");
        if (application.Status != OrgApplicationStatus.Pending)
            throw new ConflictException("Заявка уже рассмотрена");
        return application;
    }

    private void MarkReviewed(OrgApplication application)
    {
        application.ReviewedAt = timeProvider.GetUtcNow().UtcDateTime;
        application.ReviewedByUserId = currentUser.UserId;
    }

    private async Task TrySendAsync(EmailMessage message, CancellationToken ct)
    {
        try
        {
            await email.SendAsync(message, ct);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Не удалось отправить письмо по заявке на {Email}", message.To);
        }
    }
}
