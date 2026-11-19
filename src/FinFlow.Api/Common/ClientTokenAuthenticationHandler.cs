using System.Security.Claims;
using System.Text.Encodings.Web;
using FinFlow.Application.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace FinFlow.Api.Common;

/// <summary>
/// Аутентификация клиента интернет-банка по токену личного кабинета.
/// Отдельная схема: токен клиента не принимается API операционного контура
/// и наоборот.
/// </summary>
public class ClientTokenAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "FinFlowClientToken";
    public const string ClientRole = "Client";
    private const string BearerPrefix = "Bearer ";

    private readonly IClientSessionStore _sessions;

    public ClientTokenAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IClientSessionStore sessions)
        : base(options, logger, encoder)
    {
        _sessions = sessions;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var values))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var header = values.ToString();
        if (string.IsNullOrWhiteSpace(header) || !header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var token = header.Substring(BearerPrefix.Length).Trim();
        if (!_sessions.TryValidate(token, out var principal))
        {
            return Task.FromResult(AuthenticateResult.Fail("Сеанс личного кабинета истёк. Войдите снова."));
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, principal.ClientId.ToString()),
            new(ClaimTypes.Name, principal.DocumentNumber),
            new(ClaimTypes.GivenName, principal.FullName),
            new(ClaimTypes.Role, ClientRole)
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
