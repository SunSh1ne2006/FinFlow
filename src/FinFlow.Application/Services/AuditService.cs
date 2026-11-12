using System.Text.Json;
using FinFlow.Application.Abstractions;
using FinFlow.Application.Mapping;
using FinFlow.Contracts;
using FinFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Application.Services;

/// <summary>Журнал аудита: только вставка, фильтрация и постраничный просмотр (требование 19).</summary>
public class AuditService : IAuditWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly IBankDbContext _db;
    private readonly IClock _clock;
    private readonly ICurrentUser _current;

    public AuditService(IBankDbContext db, IClock clock, ICurrentUser current)
    {
        _db = db;
        _clock = clock;
        _current = current;
    }

    public async Task WriteAsync(string action, string entityName, string entityId, object oldValue, object newValue, CancellationToken cancellationToken = default)
    {
        _db.AuditRecords.Add(new AuditRecord
        {
            At = _clock.UtcNow,
            UserId = _current.UserId,
            UserLogin = _current.Login ?? "system",
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            OldValue = Serialize(oldValue),
            NewValue = Serialize(newValue),
            IpAddress = _current.IpAddress
        });

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResult<AuditRecordDto>> QueryAsync(string entity, string action, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 200 ? 50 : pageSize;

        var query = _db.AuditRecords.AsQueryable();
        if (!string.IsNullOrWhiteSpace(entity))
        {
            query = query.Where(r => r.EntityName == entity);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(r => r.Action == action);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(r => r.At)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<AuditRecordDto>
        {
            Items = items.Select(r => r.ToDto()).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    private static string Serialize(object value)
    {
        if (value == null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Serialize(value, JsonOptions);
        }
        catch (Exception)
        {
            return value.ToString();
        }
    }
}
