using System.Collections.Concurrent;
using System.Security.Cryptography;
using FinFlow.Application.Abstractions;

namespace FinFlow.Infrastructure.Security;

/// <summary>
/// Хранилище токенов доступа в памяти сервера приложений. Время жизни — 15 минут (НФТ-8);
/// при горизонтальном масштабировании заменяется распределённым кэшем.
/// </summary>
public class InMemoryTokenStore : ITokenStore
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);
    private readonly ConcurrentDictionary<string, TokenPrincipal> _tokens = new(StringComparer.Ordinal);

    public (string Token, DateTime ExpiresAt) Issue(Guid userId, string login, IReadOnlyList<string> roles)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var expiresAt = DateTime.UtcNow.Add(Lifetime);
        _tokens[token] = new TokenPrincipal
        {
            UserId = userId,
            Login = login,
            Roles = roles,
            ExpiresAt = expiresAt
        };

        return (token, expiresAt);
    }

    public bool TryValidate(string token, out TokenPrincipal principal)
    {
        principal = null;
        if (string.IsNullOrWhiteSpace(token) || !_tokens.TryGetValue(token, out var stored))
        {
            return false;
        }

        if (stored.ExpiresAt <= DateTime.UtcNow)
        {
            _tokens.TryRemove(token, out _);
            return false;
        }

        principal = stored;
        return true;
    }

    public void Revoke(string token)
    {
        if (!string.IsNullOrWhiteSpace(token))
        {
            _tokens.TryRemove(token, out _);
        }
    }
}
