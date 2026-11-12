using FinFlow.Domain.Common;
using FinFlow.Domain.Enums;

namespace FinFlow.Domain.Entities;

/// <summary>Учётная запись пользователя (требование 19, НФТ-8).</summary>
public class User : BaseEntity
{
    public string Login { get; set; }
    public string PasswordHash { get; set; }
    public string FullName { get; set; }
    public bool IsBlocked { get; set; }
    public int FailedAttempts { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;

    public List<UserRole> UserRoles { get; set; } = new();

    public IEnumerable<RoleCode> RoleCodes => UserRoles.Where(ur => ur.Role != null).Select(ur => ur.Role.Code);
}

/// <summary>Роль системы. Восемь ролей п. 1.5 (НФТ-9).</summary>
public class Role : BaseEntity
{
    public RoleCode Code { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }

    public List<UserRole> UserRoles { get; set; } = new();
}

/// <summary>Связь «пользователь – роль» (многие ко многим).</summary>
public class UserRole
{
    public Guid UserId { get; set; }
    public User User { get; set; }
    public Guid RoleId { get; set; }
    public Role Role { get; set; }
}
