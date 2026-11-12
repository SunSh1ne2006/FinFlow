namespace FinFlow.Contracts;

public class LoginRequest
{
    public string Login { get; set; }
    public string Password { get; set; }
    /// <summary>Второй фактор для контролёра и администратора (НФТ-8).</summary>
    public string SecondFactor { get; set; }
}

public class LoginResponse
{
    public string Token { get; set; }
    public DateTime ExpiresAt { get; set; }
    public UserDto User { get; set; }
    public bool SecondFactorRequired { get; set; }
}

public class UserDto
{
    public Guid Id { get; set; }
    public string Login { get; set; }
    public string FullName { get; set; }
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> RoleNames { get; set; } = Array.Empty<string>();
    public bool TwoFactorEnabled { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public bool IsBlocked { get; set; }
}
