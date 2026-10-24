namespace Sitora.Domain.DTOs.Sales;

// LeadId — если клиент заводится из заявки: заявка закрывается, её брони переходят на клиента.
public record CreateCustomerRequest(string FullName, string Phone, string? Email, string? PassportData, Guid? LeadId);

public record CustomerListItem(Guid Id, string FullName, string Phone, string? Email, DateTime CreatedAt);

public record CustomerResponse(
    Guid Id,
    string FullName,
    string Phone,
    string? Email,
    string? PassportData,
    Guid? UserId,
    DateTime CreatedAt);
