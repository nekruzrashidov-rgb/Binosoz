using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.Entities;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;

namespace Sitora.Infrastructure.Services;

public class AuditService(AppDbContext db, ICurrentUser currentUser, TimeProvider timeProvider) : IAuditService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
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

    public async Task<PagedResult<AuditLogResponse>> GetAsync(AuditQuery query, CancellationToken ct = default)
    {
        var p = PageRequest.Normalize(query.Page, query.PageSize);
        var logs = db.AuditLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Entity))
            logs = logs.Where(a => a.Entity == query.Entity);
        if (query.EntityId is not null)
            logs = logs.Where(a => a.EntityId == query.EntityId);
        if (query.From is not null)
            logs = logs.Where(a => a.At >= query.From);
        if (query.To is not null)
            logs = logs.Where(a => a.At < query.To);

        var total = await logs.CountAsync(ct);
        var items = await (
                from a in logs
                join u in db.Users on a.UserId equals u.Id into users
                from u in users.DefaultIfEmpty()
                orderby a.At descending, a.Id descending
                select new AuditLogResponse(a.Id, a.UserId, u != null ? u.Email : null,
                    a.Entity, a.EntityId, a.Action, a.Before, a.After, a.At))
            .Skip(p.Skip).Take(p.PageSize)
            .ToListAsync(ct);
        return new PagedResult<AuditLogResponse>(items, p.Page, p.PageSize, total);
    }

    private static string? Serialize(object? value) =>
        value is null ? null : JsonSerializer.Serialize(value, JsonOptions);
}
