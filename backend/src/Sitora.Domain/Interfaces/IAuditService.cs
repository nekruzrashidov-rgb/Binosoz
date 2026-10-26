using Sitora.Domain.DTOs.Common;

namespace Sitora.Domain.Interfaces;

public interface IAuditService
{
    // Добавляет запись в текущую единицу работы: она сохранится вместе с самим изменением.
    // Организация берётся из токена сотрудника; для действий гостя (заявка, бронь с сайта) её передают явно.
    void Record(string entity, Guid entityId, string action, object? before, object? after, Guid? organizationId = null);

    Task<PagedResult<AuditLogResponse>> GetAsync(AuditQuery query, CancellationToken ct = default);
}
