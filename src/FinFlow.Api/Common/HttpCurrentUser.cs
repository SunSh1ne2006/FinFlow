using System.Security.Claims;
using FinFlow.Application.Abstractions;

namespace FinFlow.Api.Common;

/// <summary>Текущий пользователь из claims токена доступа.</summary>
public class HttpCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public HttpCurrentUser(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public Guid? UserId
    {
        get
        {
            var value = _accessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public string Login => _accessor.HttpContext?.User?.FindFirst(ClaimTypes.Name)?.Value;

    public IReadOnlyList<string> Roles => _accessor.HttpContext?.User?
        .FindAll(ClaimTypes.Role)
        .Select(claim => claim.Value)
        .ToList() ?? new List<string>();

    public bool IsInRole(string role) => Roles.Contains(role);

    public bool IsAuthenticated => _accessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

    public string IpAddress => _accessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();
}
