using FinFlow.Application.Abstractions;
using FinFlow.Application.Common;
using FinFlow.Application.Mapping;
using FinFlow.Contracts;
using FinFlow.Domain.Common;
using FinFlow.Domain.Entities;
using FinFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Application.Services;

/// <summary>
/// Ведение ограничений (требование 6): аресты и приостановления из АИС ИДО,
/// бронирования и авторизационные блокировки. Сумма действующих ограничений образует H.
/// </summary>
public class HoldService
{
    private readonly IBankDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditWriter _audit;
    private readonly ICurrentUser _current;

    public HoldService(IBankDbContext db, IClock clock, IAuditWriter audit, ICurrentUser current)
    {
        _db = db;
        _clock = clock;
        _audit = audit;
        _current = current;
    }

    public async Task<IReadOnlyList<HoldDto>> ListActiveAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var holds = await _db.Holds
            .Where(h => h.AccountId == accountId && h.Status == HoldStatus.Active)
            .OrderByDescending(h => h.CreatedAt)
            .ToListAsync(cancellationToken);

        return holds.Select(h => h.ToDto()).ToList();
    }

    public async Task<HoldDto> AddAsync(Guid accountId, CreateHoldRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null || request.Amount <= 0m)
        {
            throw new AppException("Сумма ограничения должна быть больше нуля.");
        }

        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);
        if (account == null)
        {
            throw new NotFoundException("Счёт не найден.");
        }

        if (!account.IsOpen)
        {
            throw new ConflictException("По закрытому счёту ограничение не устанавливается.");
        }

        var validFrom = request.ValidFrom ?? _clock.Today;
        if (request.ValidTo.HasValue && request.ValidTo.Value < validFrom)
        {
            throw new AppException("Дата окончания ограничения не может быть раньше даты начала.");
        }

        var hold = new Hold
        {
            AccountId = accountId,
            Source = Parsing.Enum<HoldSource>(request.Source, "источник ограничения", HoldSource.AisIdo),
            BasisNumber = request.BasisNumber,
            BasisAuthority = request.BasisAuthority,
            Amount = Money.Round(request.Amount),
            IsArrest = request.IsArrest,
            ValidFrom = validFrom,
            ValidTo = request.ValidTo,
            Status = HoldStatus.Active,
            Comment = request.Comment,
            CreatedBy = _current.UserId,
            CreatedAt = _clock.UtcNow
        };

        _db.Holds.Add(hold);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync("HoldApplied", nameof(Hold), hold.Id.ToString(), null,
            new { hold.Amount, hold.IsArrest, hold.BasisNumber, hold.Source }, cancellationToken);

        return hold.ToDto();
    }

    public async Task<HoldDto> ReleaseAsync(Guid holdId, CancellationToken cancellationToken = default)
    {
        var hold = await _db.Holds.FirstOrDefaultAsync(h => h.Id == holdId, cancellationToken);
        if (hold == null)
        {
            throw new NotFoundException("Ограничение не найдено.");
        }

        if (hold.Status != HoldStatus.Active)
        {
            throw new ConflictException("Ограничение уже снято.");
        }

        hold.Status = HoldStatus.Released;
        hold.ReleasedAt = _clock.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync("HoldReleased", nameof(Hold), hold.Id.ToString(),
            new { Status = HoldStatus.Active.ToString() },
            new { Status = hold.Status.ToString(), hold.ReleasedAt }, cancellationToken);

        return hold.ToDto();
    }
}
