namespace FinFlow.Application.Abstractions;

/// <summary>Шлюз АИС ИДО: приём постановлений об аресте и приостановлениях (требование 20).</summary>
public interface IAisIdoGateway
{
    Task<string> SendReceiptAsync(string documentExternalId, bool accepted, string message, CancellationToken cancellationToken = default);
}

/// <summary>Шлюз главной книги АБС: передача проводок и сверка оборотов (требование 21).</summary>
public interface ILedgerGateway
{
    Task<int> SendPostingsAsync(IReadOnlyList<Guid> postingIds, CancellationToken cancellationToken = default);
    Task<(decimal Debit, decimal Credit)> GetTurnoverAsync(DateOnly date, CancellationToken cancellationToken = default);
}

/// <summary>Межбанковская система идентификации: данные клиента с его согласия (требование 1).</summary>
public interface IMsiGateway
{
    Task<MsiClientData> GetClientDataAsync(string idNumber, CancellationToken cancellationToken = default);
}

public sealed class MsiClientData
{
    public bool Found { get; init; }
    public string FullName { get; init; }
    public string Phone { get; init; }
    public string Email { get; init; }
    public string Comment { get; init; }
}

/// <summary>Публикация доменных событий (шаблон Observer, п. 2.3.1).</summary>
public interface IEventPublisher
{
    Task PublishAsync(string eventName, object payload, CancellationToken cancellationToken = default);
}
