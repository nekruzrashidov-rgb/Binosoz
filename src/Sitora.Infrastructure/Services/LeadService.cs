using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sitora.Domain.Common;
using Sitora.Domain.DTOs.Sales;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;

namespace Sitora.Infrastructure.Services;

public class LeadService(AppDbContext db, INotificationSender notifications, ILogger<LeadService> logger) : ILeadService
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

        var lead = new Lead
        {
            Name = request.Name.Trim(),
            Phone = phone,
            Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
            ApartmentId = apartment?.Id,
            Source = request.Source ?? LeadSource.Website,
            Status = LeadStatus.New
        };
        db.Leads.Add(lead);
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

        return new LeadResponse(lead.Id, lead.Name, lead.Phone, lead.Comment, lead.ApartmentId,
            lead.Source, lead.Status, lead.ManagerId, lead.CreatedAt);
    }

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
