using Sitora.Domain.Enums;

namespace Sitora.Domain.DTOs.Auth;

// Заявка застройщика с публичной страницы /register.
public record SubmitApplicationRequest(string CompanyName, string ContactEmail, string ContactPhone, string OwnerFullName, string? Comment);

public record SubmitApplicationResponse(Guid ApplicationId, string Message);

public record OrgApplicationResponse(
    Guid Id,
    string CompanyName,
    string ContactEmail,
    string ContactPhone,
    string OwnerFullName,
    string? Comment,
    OrgApplicationStatus Status,
    string? ReviewNote,
    DateTime CreatedAt,
    DateTime? ReviewedAt,
    Guid? OrganizationId);

// Короткое имя (поддомен) будущей организации выбирает создатель платформы.
public record ApproveApplicationRequest(string OrganizationSlug);

public record ApproveApplicationResponse(Guid OrganizationId, string Message);

public record RejectApplicationRequest(string Reason);
