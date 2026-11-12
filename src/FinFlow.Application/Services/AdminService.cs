using FinFlow.Application.Abstractions;
using FinFlow.Application.Common;
using FinFlow.Application.Mapping;
using FinFlow.Contracts;
using FinFlow.Domain.Entities;
using FinFlow.Domain.Enums;
using FinFlow.Domain.Reference;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Application.Services;

/// <summary>Администрирование: пользователи, роли, блокировка учётных записей (требование 19, НФТ-8).</summary>
public class AdminService
{
    private readonly IBankDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IClock _clock;
    private readonly IAuditWriter _audit;

    public AdminService(IBankDbContext db, IPasswordHasher hasher, IClock clock, IAuditWriter audit)
    {
        _db = db;
        _hasher = hasher;
        _clock = clock;
        _audit = audit;
    }

    public async Task<IReadOnlyList<UserDto>> ListUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await _db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .OrderBy(u => u.Login)
            .ToListAsync(cancellationToken);

        return users.Select(u => u.ToDto()).ToList();
    }

    public async Task<UserDto> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Login) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new AppException("Укажите логин и пароль.");
        }

        if (request.Password.Length < 12)
        {
            throw new AppException("Пароль должен содержать не менее 12 символов (НФТ-8).");
        }

        var login = request.Login.Trim();
        if (await _db.Users.AnyAsync(u => u.Login == login, cancellationToken))
        {
            throw new ConflictException($"Пользователь {login} уже существует.");
        }

        var roleCode = Parsing.Enum<RoleCode>(request.Role, "роль", RoleCode.Operator);
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Code == roleCode, cancellationToken);
        if (role == null)
        {
            throw new NotFoundException($"Роль {roleCode} не настроена в системе.");
        }

        var user = new User
        {
            Login = login,
            FullName = string.IsNullOrWhiteSpace(request.FullName) ? login : request.FullName.Trim(),
            PasswordHash = _hasher.Hash(request.Password),
            TwoFactorEnabled = request.TwoFactorEnabled,
            IsActive = true
        };

        user.UserRoles.Add(new UserRole { User = user, Role = role });
        _db.Users.Add(user);

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("UserCreated", nameof(User), user.Id.ToString(), null,
            new { user.Login, Role = roleCode.ToString() }, cancellationToken);

        return user.ToDto();
    }

    public async Task<UserDto> SetBlockedAsync(Guid id, bool blocked, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (user == null)
        {
            throw new NotFoundException("Пользователь не найден.");
        }

        user.IsBlocked = blocked;
        if (!blocked)
        {
            user.FailedAttempts = 0;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(blocked ? "UserBlocked" : "UserUnblocked", nameof(User), user.Id.ToString(),
            new { IsBlocked = !blocked }, new { user.IsBlocked }, cancellationToken);

        return user.ToDto();
    }

    public IReadOnlyList<RoleInfoDto> Roles() => ModelBankData.Roles
        .Select(r => new RoleInfoDto
        {
            Code = (int)r.Code,
            CodeName = r.Code.ToString(),
            Name = r.Name,
            Client = r.Client,
            Description = r.Description
        })
        .ToList();
}
