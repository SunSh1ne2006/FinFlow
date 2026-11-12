namespace FinFlow.Application.Abstractions;

public sealed class TokenPrincipal
{
    public Guid UserId { get; init; }
    public string Login { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
    public DateTime ExpiresAt { get; init; }
}

/// <summary>Выдача и проверка токенов доступа (сессии на сервере приложений).</summary>
public interface ITokenStore
{
    (string Token, DateTime ExpiresAt) Issue(Guid userId, string login, IReadOnlyList<string> roles);
    bool TryValidate(string token, out TokenPrincipal principal);
    void Revoke(string token);
}
