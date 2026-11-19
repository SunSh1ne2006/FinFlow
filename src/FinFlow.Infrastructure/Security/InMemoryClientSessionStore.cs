using System.Collections.Concurrent;
using System.Security.Cryptography;
using FinFlow.Application.Abstractions;

namespace FinFlow.Infrastructure.Security;

/// <summary>
/// Токены личного кабинета клиента: срок жизни 30 минут, хранение в памяти процесса.
/// В промышленной эксплуатации заменяется распределённым кэшем.
/// </summary>
public class InMemoryClientSessionStore : IClientSessionStore
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);
    private readonly ConcurrentDictionary<string, ClientSessionPrincipal> _sessions = new(StringComparer.Ordinal);

    public (string Token, DateTime ExpiresAt) Issue(Guid clientId, string documentNumber, string fullName)
    {
        var token = "cl_" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var expiresAt = DateTime.UtcNow.Add(Lifetime);

        _sessions[token] = new ClientSessionPrincipal
        {
            ClientId = clientId,
            DocumentNumber = documentNumber,
            FullName = fullName,
            ExpiresAt = expiresAt
        };

        return (token, expiresAt);
    }

    public bool TryValidate(string token, out ClientSessionPrincipal principal)
    {
        principal = null;
        if (string.IsNullOrWhiteSpace(token) || !_sessions.TryGetValue(token, out var stored))
        {
            return false;
        }

        if (stored.ExpiresAt <= DateTime.UtcNow)
        {
            _sessions.TryRemove(token, out _);
            return false;
        }

        principal = stored;
        return true;
    }

    public void Revoke(string token)
    {
        if (!string.IsNullOrWhiteSpace(token))
        {
            _sessions.TryRemove(token, out _);
        }
    }
}
