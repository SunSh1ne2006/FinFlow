namespace FinFlow.Application.Abstractions;

/// <summary>Запись в неизменяемый журнал аудита (требование 19).</summary>
public interface IAuditWriter
{
    Task WriteAsync(string action, string entityName, string entityId, object oldValue, object newValue, CancellationToken cancellationToken = default);
}
