using FinFlow.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinFlow.Infrastructure.Integrations;

/// <summary>Шлюз АИС ИДО: формирование квитанции о приёме документа (требование 20).</summary>
public class AisIdoGateway : IAisIdoGateway
{
    private readonly ILogger<AisIdoGateway> _logger;

    public AisIdoGateway(ILogger<AisIdoGateway> logger)
    {
        _logger = logger;
    }

    public Task<string> SendReceiptAsync(string documentExternalId, bool accepted, string message, CancellationToken cancellationToken = default)
    {
        var receipt = $"КВ-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{(accepted ? "ПРИНЯТ" : "ОТКАЗ")}";
        _logger.LogInformation("АИС ИДО: квитанция {Receipt} по документу {ExternalId}: {Message}", receipt, documentExternalId, message);
        return Task.FromResult(receipt);
    }
}

/// <summary>
/// Шлюз главной книги АБС. Обороты формируются по проводкам, переданным в главную книгу,
/// поэтому расхождение с учётными проводками выявляется сверкой (требование 21).
/// </summary>
public class LedgerGateway : ILedgerGateway
{
    private readonly IBankDbContext _db;

    public LedgerGateway(IBankDbContext db)
    {
        _db = db;
    }

    public async Task<int> SendPostingsAsync(IReadOnlyList<Guid> postingIds, CancellationToken cancellationToken = default)
    {
        if (postingIds.Count == 0)
        {
            return 0;
        }

        var postings = await _db.Postings
            .Where(p => postingIds.Contains(p.Id))
            .ToListAsync(cancellationToken);

        foreach (var posting in postings)
        {
            posting.IsLedgerPosted = true;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return postings.Count;
    }

    public async Task<(decimal Debit, decimal Credit)> GetTurnoverAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var from = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var to = from.AddDays(1);

        var total = await _db.Postings
            .Where(p => p.IsLedgerPosted && p.CreatedAt >= from && p.CreatedAt < to)
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

        return (total, total);
    }
}

/// <summary>Шлюз МСИ: демонстрационные данные идентификации по согласию клиента (требование 1).</summary>
public class MsiGateway : IMsiGateway
{
    public Task<MsiClientData> GetClientDataAsync(string idNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idNumber) || idNumber.Trim().Length < 5)
        {
            return Task.FromResult(new MsiClientData { Found = false, Comment = "Клиент не найден в МСИ." });
        }

        return Task.FromResult(new MsiClientData
        {
            Found = true,
            FullName = "Данные подтверждены МСИ",
            Phone = "+375 (29) 000-00-00",
            Email = "client@example.by",
            Comment = $"Идентификация подтверждена через МСИ {DateTime.UtcNow:dd.MM.yyyy}."
        });
    }
}

/// <summary>Публикация доменных событий: журналирование в конвейере приложения (шаблон Observer).</summary>
public class LoggingEventPublisher : IEventPublisher
{
    private readonly ILogger<LoggingEventPublisher> _logger;

    public LoggingEventPublisher(ILogger<LoggingEventPublisher> logger)
    {
        _logger = logger;
    }

    public Task PublishAsync(string eventName, object payload, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Событие {Event}: {@Payload}", eventName, payload);
        return Task.CompletedTask;
    }
}
