using FinFlow.Application.Abstractions;
using FinFlow.Application.Common;
using FinFlow.Application.Mapping;
using FinFlow.Contracts;
using FinFlow.Domain.Calculators;
using FinFlow.Domain.Entities;
using FinFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Application.Services;

public interface IBalanceService
{
    Task<BalanceSnapshotDto> GetSnapshotAsync(Guid accountId, DateOnly date, CancellationToken cancellationToken = default);
    Task<decimal> GetAvailableAsync(Account account, DateOnly date, CancellationToken cancellationToken = default);
}

/// <summary>
/// Расчёт доступного остатка D по формуле (1.1) по действующим ограничениям.
/// D не хранится в базе: он вычисляется на момент запроса, поэтому новый арест
/// учитывается сразу (требования 5 и 6).
/// </summary>
public class BalanceService : IBalanceService
{
    private readonly IBankDbContext _db;
    private readonly IClock _clock;

    public BalanceService(IBankDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<BalanceSnapshotDto> GetSnapshotAsync(Guid accountId, DateOnly date, CancellationToken cancellationToken = default)
    {
        var account = await _db.Accounts
            .Include(a => a.Numbers)
            .Include(a => a.Contracts)
            .ThenInclude(c => c.ProductVersion)
            .FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);

        if (account == null)
        {
            throw new NotFoundException("Счёт не найден.");
        }

        var holds = await _db.Holds
            .Where(h => h.AccountId == accountId)
            .ToListAsync(cancellationToken);

        var active = holds.Where(h => h.IsActiveOn(date)).ToList();
        var holdsAmount = active.Sum(h => h.Amount);
        var hasArrest = active.Any(h => h.IsArrest);
        var available = BalanceCalculator.Available(account.Balance, account.OverdraftLimit, holdsAmount, hasArrest);
        var contract = account.Contracts?.FirstOrDefault(c => c.IsActive) ?? account.Contracts?.FirstOrDefault();

        return new BalanceSnapshotDto
        {
            AccountId = account.Id,
            AccountNumber = account.CurrentNumber(date),
            Balance = account.Balance,
            OverdraftLimit = account.OverdraftLimit,
            HoldsAmount = holdsAmount,
            HasArrest = hasArrest,
            AvailableBalance = available,
            OverdraftUsed = BalanceCalculator.OverdraftUsed(account.Balance),
            OverdraftUtilizationPercent = BalanceCalculator.OverdraftUtilizationPercent(account.Balance, account.OverdraftLimit),
            MinBalance = contract?.ProductVersion?.MinBalance ?? 0m,
            Date = date
        };
    }

    public async Task<decimal> GetAvailableAsync(Account account, DateOnly date, CancellationToken cancellationToken = default)
    {
        if (account == null)
        {
            throw new NotFoundException("Счёт не найден.");
        }

        var holds = await _db.Holds
            .Where(h => h.AccountId == account.Id)
            .ToListAsync(cancellationToken);

        var active = holds.Where(h => h.IsActiveOn(date)).ToList();
        return BalanceCalculator.Available(account.Balance, account.OverdraftLimit, active.Sum(h => h.Amount), active.Any(h => h.IsArrest));
    }
}
