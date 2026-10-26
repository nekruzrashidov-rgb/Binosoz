using Sitora.Domain.Enums;

namespace Sitora.Domain.DTOs.Sales;

// BuildingId — заявка на дом без конкретной квартиры (публичный сайт застройщика).
public record CreateLeadRequest(string Name, string Phone, Guid? ApartmentId, LeadSource? Source, string? Comment, Guid? BuildingId = null);

// null в поле — значение не меняется.
public record UpdateLeadRequest(LeadStatus? Status, Guid? ManagerId, string? Comment);

public record LeadResponse(
    Guid Id,
    string Name,
    string Phone,
    string? Comment,
    Guid? ApartmentId,
    LeadSource Source,
    LeadStatus Status,
    Guid? ManagerId,
    DateTime CreatedAt);
