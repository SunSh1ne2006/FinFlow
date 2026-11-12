namespace FinFlow.Application.Abstractions;

/// <summary>Текущий пользователь из токена аутентификации.</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
    string Login { get; }
    IReadOnlyList<string> Roles { get; }
    bool IsInRole(string role);
    bool IsAuthenticated { get; }
    string IpAddress { get; }
}
