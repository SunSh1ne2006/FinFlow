using FinFlow.Application.Abstractions;
using FinFlow.Application.Common;
using FinFlow.Application.Mapping;
using FinFlow.Contracts;
using FinFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Application.Services;

/// <summary>Аутентификация, блокировка после пяти неудачных попыток, второй фактор (НФТ-8).</summary>
public class AuthService
{
    public const int MaxFailedAttempts = 5;
    /// <summary>Демонстрационный код второго фактора (в продуктивной среде — внешний провайдер).</summary>
    public const string DemoSecondFactor = "123456";

    private readonly IBankDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenStore _tokens;
    private readonly IClock _clock;
    private readonly IAuditWriter _audit;
    private readonly ICurrentUser _current;

    public AuthService(IBankDbContext db, IPasswordHasher hasher, ITokenStore tokens, IClock clock, IAuditWriter audit, ICurrentUser current)
    {
        _db = db;
        _hasher = hasher;
        _tokens = tokens;
        _clock = clock;
        _audit = audit;
        _current = current;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Login) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new AppException("Укажите логин и пароль.");
        }

        var login = request.Login.Trim();
        var user = await _db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Login == login, cancellationToken);

        if (user == null)
        {
            throw new AppException("Неверный логин или пароль.", 401);
        }

        if (user.IsBlocked || !user.IsActive)
        {
            throw new ForbiddenException("Учётная запись заблокирована. Обратитесь к администратору.");
        }

        if (!_hasher.Verify(request.Password, user.PasswordHash))
        {
            user.FailedAttempts += 1;
            if (user.FailedAttempts >= MaxFailedAttempts)
            {
                user.IsBlocked = true;
            }

            await _db.SaveChangesAsync(cancellationToken);
            await _audit.WriteAsync("LoginFailed", nameof(User), user.Id.ToString(),
                null, new { user.Login, user.FailedAttempts, user.IsBlocked }, cancellationToken);
            throw new AppException("Неверный логин или пароль.", 401);
        }

        if (user.TwoFactorEnabled)
        {
            if (string.IsNullOrWhiteSpace(request.SecondFactor))
            {
                return new LoginResponse
                {
                    SecondFactorRequired = true,
                    User = user.ToDto()
                };
            }

            if (!string.Equals(request.SecondFactor.Trim(), DemoSecondFactor, StringComparison.Ordinal))
            {
                throw new AppException("Неверный код второго фактора.", 401);
            }
        }

        user.FailedAttempts = 0;
        user.LastLoginAt = _clock.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        var roles = user.UserRoles
            .Where(ur => ur.Role != null)
            .Select(ur => ur.Role.Code.ToString())
            .ToList();

        var issued = _tokens.Issue(user.Id, user.Login, roles);
        await _audit.WriteAsync("Login", nameof(User), user.Id.ToString(), null,
            new { user.Login, Roles = roles }, cancellationToken);

        return new LoginResponse
        {
            Token = issued.Token,
            ExpiresAt = issued.ExpiresAt,
            User = user.ToDto()
        };
    }

    public async Task<UserDto> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        if (!_current.UserId.HasValue)
        {
            throw new AppException("Требуется аутентификация.", 401);
        }

        var user = await _db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == _current.UserId.Value, cancellationToken);

        if (user == null)
        {
            throw new AppException("Пользователь не найден.", 401);
        }

        return user.ToDto();
    }

    public void Logout(string token)
    {
        if (!string.IsNullOrWhiteSpace(token))
        {
            _tokens.Revoke(token);
        }
    }
}
