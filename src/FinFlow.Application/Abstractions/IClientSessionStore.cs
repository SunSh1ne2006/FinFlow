namespace FinFlow.Application.Abstractions;

/// <summary>Сеанс клиента интернет-банка (отдельно от сеансов сотрудников).</summary>
public sealed class ClientSessionPrincipal
{
    public Guid ClientId { get; init; }
    public string DocumentNumber { get; init; }
    public string FullName { get; init; }
    public DateTime ExpiresAt { get; init; }
}

/// <summary>
/// Выдача и проверка токенов личного кабинета клиента. Хранилище отделено от
/// токенов сотрудников: токен клиента не даёт доступа к API операционного контура.
/// </summary>
public interface IClientSessionStore
{
    (string Token, DateTime ExpiresAt) Issue(Guid clientId, string documentNumber, string fullName);
    bool TryValidate(string token, out ClientSessionPrincipal principal);
    void Revoke(string token);
}
