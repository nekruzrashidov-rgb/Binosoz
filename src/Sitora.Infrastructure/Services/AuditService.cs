using System.Text.Json;
using Sitora.Domain.Entities;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;

namespace Sitora.Infrastructure.Services;

public class AuditService(AppDbContext db, ICurrentUser currentUser, TimeProvider timeProvider) : IAuditService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public void Record(string entity, Guid entityId, string action, object? before, object? after)
    {
        db.AuditLogs.Add(new AuditLog
        {
            UserId = currentUser.UserId,
            Entity = entity,
            EntityId = entityId,
            Action = action,
            Before = Serialize(before),
            After = Serialize(after),
            At = timeProvider.GetUtcNow().UtcDateTime
        });
    }

    private static string? Serialize(object? value) =>
        value is null ? null : JsonSerializer.Serialize(value, JsonOptions);
}
