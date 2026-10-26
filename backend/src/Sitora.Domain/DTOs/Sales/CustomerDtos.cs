namespace Sitora.Domain.DTOs.Sales;

// LeadId — если клиент заводится из заявки: заявка закрывается, её брони переходят на клиента.
// BuildingId — дом, где заводят клиента (для сотрудника с доступом к части домов подставляется его дом).
public record CreateCustomerRequest(string FullName, string Phone, string? Email, string? PassportData, Guid? LeadId, Guid? BuildingId = null);

public record CustomerListItem(Guid Id, string FullName, string Phone, string? Email, DateTime CreatedAt);

public record CustomerResponse(
    Guid Id,
    string FullName,
    string Phone,
    string? Email,
    string? PassportData,
    Guid? UserId,
    DateTime CreatedAt);
