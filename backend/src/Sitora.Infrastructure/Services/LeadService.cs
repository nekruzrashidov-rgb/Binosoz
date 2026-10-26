using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sitora.Domain.Common;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.DTOs.Sales;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;

namespace Sitora.Infrastructure.Services;

public class LeadService(
    AppDbContext db,
    UserManager<AppUser> userManager,
    ICurrentUser currentUser,
    INotificationSender notifications,
    IAuditService audit,
    ILogger<LeadService> logger) : ILeadService
{
    public async Task<LeadResponse> CreateAsync(CreateLeadRequest request, CancellationToken ct = default)
    {
        var phone = PhoneNumber.Normalize(request.Phone)
            ?? throw new BusinessRuleException("Неверный номер телефона");

        Apartment? apartment = null;
        if (request.ApartmentId is { } apartmentId)
        {
            apartment = await db.Apartments.AsNoTracking()
                .Include(a => a.Floor).ThenInclude(f => f.Building)
                .FirstOrDefaultAsync(a => a.Id == apartmentId, ct)
                ?? throw new NotFoundException("Квартира не найдена");
        }

        var (organizationId, buildingId) = await ResolvePlaceAsync(apartment, request.BuildingId, ct);
        var lead = new Lead
        {
            OrganizationId = organizationId,
            BuildingId = buildingId,
            Name = request.Name.Trim(),
            Phone = phone,
            Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
            ApartmentId = apartment?.Id,
            Source = request.Source ?? LeadSource.Website,
            Status = LeadStatus.New
        };
        db.Leads.Add(lead);
        audit.Record(nameof(Lead), lead.Id, "Created", null, new { lead.Name, lead.Phone, lead.Source, lead.BuildingId }, organizationId);
        await db.SaveChangesAsync(ct);

        // Заявка уже сохранена; сбой канала уведомлений не должен терять её.
        try
        {
            await notifications.SendAsync(null, BuildNotificationText(lead, apartment), ct);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Не удалось отправить уведомление о заявке {LeadId}", lead.Id);
        }

        return ToResponse(lead);
    }

    public async Task<PagedResult<LeadResponse>> GetAsync(LeadStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        var p = PageRequest.Normalize(page, pageSize);
        var query = db.Leads.AsNoTracking();
        if (status is not null)
            query = query.Where(l => l.Status == status);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(l => l.CreatedAt)
            .Skip(p.Skip).Take(p.PageSize)
            .Select(l => new LeadResponse(l.Id, l.Name, l.Phone, l.Comment, l.ApartmentId, l.Source, l.Status, l.ManagerId, l.CreatedAt))
            .ToListAsync(ct);
        return new PagedResult<LeadResponse>(items, p.Page, p.PageSize, total);
    }

    public async Task<LeadResponse> UpdateAsync(Guid id, UpdateLeadRequest request, CancellationToken ct = default)
    {
        var lead = await db.Leads.FirstOrDefaultAsync(l => l.Id == id, ct)
            ?? throw new NotFoundException("Заявка не найдена");

        if (request.ManagerId is { } managerId)
        {
            var manager = await userManager.FindByIdAsync(managerId.ToString());
            if (manager is null || !manager.IsActive || manager.OrganizationId != lead.OrganizationId
                || !(await userManager.IsInRoleAsync(manager, RoleNames.Manager)
                     || await userManager.IsInRoleAsync(manager, RoleNames.Owner)
                     || await userManager.IsInRoleAsync(manager, RoleNames.ObjectAdmin)))
                throw new BusinessRuleException("Ответственным можно назначить только активного менеджера этого застройщика");
        }

        var before = new { lead.Status, lead.ManagerId, lead.Comment };
        lead.Status = request.Status ?? lead.Status;
        lead.ManagerId = request.ManagerId ?? lead.ManagerId;
        if (request.Comment is not null)
            lead.Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();

        audit.Record(nameof(Lead), lead.Id, "Updated", before, new { lead.Status, lead.ManagerId, lead.Comment });
        await db.SaveChangesAsync(ct);
        return ToResponse(lead);
    }

    // Застройщик и дом заявки: из квартиры, из указанного дома, из организации сотрудника,
    // а если на платформе один застройщик — он. Иначе заявку не к кому отнести.
    private async Task<(Guid OrganizationId, Guid? BuildingId)> ResolvePlaceAsync(Apartment? apartment, Guid? buildingId, CancellationToken ct)
    {
        if (apartment is not null)
            return (apartment.Floor.Building.OrganizationId, apartment.Floor.BuildingId);

        if (buildingId is { } id)
        {
            var organizationId = await db.Buildings.Where(b => b.Id == id).Select(b => (Guid?)b.OrganizationId).FirstOrDefaultAsync(ct)
                ?? throw new NotFoundException("Дом не найден");
            return (organizationId, id);
        }

        if (currentUser.OrganizationId is { } own)
            return (own, currentUser.BuildingIds is [var single] ? single : null);

        var organizations = await db.Organizations.Where(o => o.IsActive).Select(o => o.Id).Take(2).ToListAsync(ct);
        return organizations.Count == 1
            ? (organizations[0], null)
            : throw new BusinessRuleException("Укажите дом или квартиру, к которой относится заявка");
    }

    private static LeadResponse ToResponse(Lead l) =>
        new(l.Id, l.Name, l.Phone, l.Comment, l.ApartmentId, l.Source, l.Status, l.ManagerId, l.CreatedAt);

    private static string BuildNotificationText(Lead lead, Apartment? apartment)
    {
        var text = $"Новая заявка ({lead.Source}): {lead.Name}, {lead.Phone}";
        if (apartment is not null)
            text += $". {apartment.Floor.Building.NameRu}, этаж {apartment.Floor.Number}, кв. №{apartment.Number}";
        if (lead.Comment is not null)
            text += $". Комментарий: {lead.Comment}";
        return text;
    }
}
