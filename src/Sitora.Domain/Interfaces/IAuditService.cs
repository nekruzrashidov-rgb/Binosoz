namespace Sitora.Domain.Interfaces;

public interface IAuditService
{
    // Добавляет запись в текущую единицу работы: она сохранится вместе с самим изменением.
    void Record(string entity, Guid entityId, string action, object? before, object? after);
}
