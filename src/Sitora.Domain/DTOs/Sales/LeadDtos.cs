using Sitora.Domain.Enums;

namespace Sitora.Domain.DTOs.Sales;

public record CreateLeadRequest(string Name, string Phone, Guid? ApartmentId, LeadSource? Source, string? Comment);

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
