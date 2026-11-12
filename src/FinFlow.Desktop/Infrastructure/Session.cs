using FinFlow.Contracts;

namespace FinFlow.Desktop.Infrastructure;

/// <summary>Сеанс операционного контура: клиент API и данные пользователя.</summary>
public class Session
{
    public Session(ApiClient api, UserDto user)
    {
        Api = api;
        User = user;
    }

    public ApiClient Api { get; }
    public UserDto User { get; }

    public string Caption => User.FullName + " · " + string.Join(", ", User.RoleNames ?? new System.Collections.Generic.List<string>());

    public bool IsInRole(string role) => User.Roles != null && User.Roles.Contains(role);
}
