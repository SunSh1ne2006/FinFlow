using FinFlow.Application.Abstractions;
using FinFlow.Application.Mapping;
using FinFlow.Contracts;
using FinFlow.Domain.Calculators;
using FinFlow.Domain.Common;
using FinFlow.Domain.Entities;
using FinFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Application.Services;

/// <summary>
/// Закрытие операционного дня: начисление процентов, капитализация или выплата с удержанием налога,
/// пролонгация и возврат вкладов. Этапы фиксируются в OperationalDays, поэтому повторный запуск
/// продолжает обработку с сохранённого этапа и не создаёт повторных начислений.
/// </summary>
public class DayCloseService
{
    private readonly IBankDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditWriter _audit;
    private readonly InterestService _interest;
    private readonly TaxService _tax;

    public DayCloseService(
        IBankDbContext db,
        IClock clock,
        IAuditWriter audit,
        InterestService interest,
        TaxService tax)
    {
        _db = db;
        _clock = clock;
        _audit = audit;
        _interest = interest;
        _tax = tax;
    }

    public async Task<OperationalDayDto> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        var day = await _db.OperationalDays
            .OrderByDescending(d => d.Date)
            .FirstOrDefaultAsync(cancellationToken);

        return day?.ToDto();
    }

    public async Task<IReadOnlyList<OperationalDayDto>> HistoryAsync(int take, CancellationToken cancellationToken = default)
    {
        take = take is < 1 or > 365 ? 30 : take;
        var days = await _db.OperationalDays
            .OrderByDescending(d => d.Date)
            .Take(take)
            .ToListAsync(cancellationToken);

        return days.Select(d => d.ToDto()).ToList();
    }

    /// <summary>Запуск закрытия дня. Повторный запуск после завершения не изменяет результат.</summary>
    public Task<OperationalDayDto> StartAsync(CancellationToken cancellationToken = default) => RunAsync(cancellationToken);

    /// <summary>Продолжение закрытия после сбоя — с сохранённого этапа.</summary>
    public Task<OperationalDayDto> ContinueAsync(CancellationToken cancellationToken = default) => RunAsync(cancellationToken);

    private async Task<OperationalDayDto> RunAsync(CancellationToken cancellationToken)
    {
        var date = _clock.Today;
        var day = await _db.OperationalDays.FirstOrDefaultAsync(d => d.Date == date, cancellationToken);

        if (day == null)
        {
            day = new OperationalDay
            {
                Date = date,
                Status = OperationalDayStatus.Open,
                Stage = DayCloseStage.Started,
                StartedAt = _clock.UtcNow
            };
            _db.OperationalDays.Add(day);
        }

        if (day.IsClosed)
        {
            return day.ToDto();
        }

        day.Status = OperationalDayStatus.Closing;
        day.StartedAt ??= _clock.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            if ((int)day.Stage < (int)DayCloseStage.AccrualsDone)
            {
                await RejectDraftsAsync(day, cancellationToken);
                await AccrueAsync(day, date, cancellationToken);
                day.Stage = DayCloseStage.AccrualsDone;
                await _db.SaveChangesAsync(cancellationToken);
            }

            if ((int)day.Stage < (int)DayCloseStage.PayoutsDone)
            {
                await PayoutsAsync(day, date, cancellationToken);
                day.Stage = DayCloseStage.PayoutsDone;
                await _db.SaveChangesAsync(cancellationToken);
            }

            if ((int)day.Stage < (int)DayCloseStage.ContractsDone)
            {
                await ProcessContractsAsync(date, cancellationToken);
                day.Stage = DayCloseStage.ContractsDone;
                await _db.SaveChangesAsync(cancellationToken);
            }

            day.Status = OperationalDayStatus.Closed;
            day.Stage = DayCloseStage.Completed;
            day.ClosedAt = _clock.UtcNow;
            day.LastError = null;
            await _db.SaveChangesAsync(cancellationToken);

            await _audit.WriteAsync("DayClosed", nameof(OperationalDay), day.Id.ToString(), null,
                new { day.Date, day.AccrualCount, day.InterestAccrued, day.TaxWithheld }, cancellationToken);
        }
        catch (Exception ex)
        {
            day.Status = OperationalDayStatus.Failed;
            day.LastError = ex.Message;
            await _db.SaveChangesAsync(cancellationToken);
            throw;
        }

        return day.ToDto();
    }

    /// <summary>Незавершённые операции (черновики) отклоняются: перенос на следующий день не допускается.</summary>
    private async Task RejectDraftsAsync(OperationalDay day, CancellationToken cancellationToken)
    {
        var pending = await _db.Operations
            .Where(o => o.Status == OperationStatus.Draft)
            .ToListAsync(cancellationToken);

        foreach (var operation in pending)
        {
            operation.Status = OperationStatus.Rejected;
            operation.RejectionReason = "Не проведена в течение операционного дня.";
        }

        day.RejectedOperations = pending.Count;
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Начисление процентов по формуле (1.5) по всем действующим договорам.</summary>
    private async Task AccrueAsync(OperationalDay day, DateOnly date, CancellationToken cancellationToken)
    {
        var contracts = await LoadActiveContractsAsync(cancellationToken);
        var alreadyAccrued = await _db.InterestAccruals
            .Where(a => a.AccrualDate == date)
            .Select(a => a.ContractId)
            .ToListAsync(cancellationToken);

        foreach (var contract in contracts)
        {
            if (contract.OpenDate > date || alreadyAccrued.Contains(contract.Id))
            {
                continue;
            }

            var accrual = await _interest.AccrueDayAsync(contract, date, day.Id, cancellationToken);
            if (accrual.Amount <= 0m)
            {
                continue;
            }

            day.AccrualCount += 1;
            day.InterestAccrued = Money.Round(day.InterestAccrued + accrual.Amount);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Капитализация или выплата процентов с удержанием налога по формуле (1.8).</summary>
    private async Task PayoutsAsync(OperationalDay day, DateOnly date, CancellationToken cancellationToken)
    {
        var contracts = await LoadActiveContractsAsync(cancellationToken);

        foreach (var contract in contracts)
        {
            var version = contract.ProductVersion;
            if (version == null)
            {
                continue;
            }

            var unpaidCount = await _db.InterestAccruals
                .CountAsync(a => a.ContractId == contract.Id && a.Status == AccrualStatus.Accrued, cancellationToken);

            if (unpaidCount == 0)
            {
                continue;
            }

            var isMaturity = contract.EndDate <= date;
            var isAnniversary = SchedulePolicy.IsMonthlyAnniversary(contract, date);
            var pay = isMaturity || (version.InterestPayout == InterestPayoutMode.Monthly && isAnniversary);
            var capitalize = !pay && version.Capitalization == CapitalizationMode.Monthly && isAnniversary;

            if (pay)
            {
                await PayInterestAsync(contract, date, day, cancellationToken);
            }
            else if (capitalize)
            {
                await CapitalizeAsync(contract, date, cancellationToken);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task PayInterestAsync(Contract contract, DateOnly date, OperationalDay day, CancellationToken cancellationToken)
    {
        var unpaid = await _db.InterestAccruals
            .Where(a => a.ContractId == contract.Id && a.Status == AccrualStatus.Accrued)
            .ToListAsync(cancellationToken);

        if (unpaid.Count == 0)
        {
            return;
        }

        var total = Money.Round(unpaid.Sum(a => a.Amount));
        if (total <= 0m)
        {
            return;
        }

        var account = await LoadAccountAsync(contract, cancellationToken);
        var taxTotal = 0m;

        foreach (var accrual in unpaid)
        {
            var withholding = await _tax.WithholdAsync(contract, accrual, cancellationToken);
            if (withholding != null)
            {
                taxTotal = Money.Round(taxTotal + withholding.Amount);
            }

            accrual.Status = AccrualStatus.Paid;
        }

        account.Balance = Money.Round(account.Balance + total);

        var payment = new Operation
        {
            AccountId = account.Id,
            ContractId = contract.Id,
            DocumentNumber = $"ВЫПЛ-{_clock.UtcNow:yyyyMMddHHmmssfff}",
            Kind = OperationKind.InterestPayment,
            Amount = total,
            Currency = account.Currency,
            Status = OperationStatus.Executed,
            ExecutedAt = _clock.UtcNow,
            Description = $"Выплата процентов по договору {contract.Number} на {date:dd.MM.yyyy}",
            CreatedAt = _clock.UtcNow
        };

        _db.Operations.Add(payment);

        if (taxTotal > 0m)
        {
            account.Balance = Money.Round(account.Balance - taxTotal);

            _db.Operations.Add(new Operation
            {
                AccountId = account.Id,
                ContractId = contract.Id,
                DocumentNumber = $"НАЛОГ-{_clock.UtcNow:yyyyMMddHHmmssfff}",
                Kind = OperationKind.TaxWithholding,
                Amount = taxTotal,
                Currency = account.Currency,
                Status = OperationStatus.Executed,
                ExecutedAt = _clock.UtcNow,
                Description = $"Удержание подоходного налога с процентов по договору {contract.Number}",
                CreatedAt = _clock.UtcNow
            });

            day.TaxWithheld = Money.Round(day.TaxWithheld + taxTotal);
        }

        contract.PaidInterest = Money.Round(contract.PaidInterest + total);
        contract.AccruedInterest = Money.Round(Math.Max(0m, contract.AccruedInterest - total));

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task CapitalizeAsync(Contract contract, DateOnly date, CancellationToken cancellationToken)
    {
        var unpaid = await _db.InterestAccruals
            .Where(a => a.ContractId == contract.Id && a.Status == AccrualStatus.Accrued)
            .ToListAsync(cancellationToken);

        if (unpaid.Count == 0)
        {
            return;
        }

        var total = Money.Round(unpaid.Sum(a => a.Amount));
        if (total <= 0m)
        {
            return;
        }

        var account = await LoadAccountAsync(contract, cancellationToken);
        contract.Principal = Money.Round(contract.Principal + total);
        contract.AccruedInterest = Money.Round(Math.Max(0m, contract.AccruedInterest - total));

        foreach (var accrual in unpaid)
        {
            accrual.Status = AccrualStatus.Capitalized;
        }

        _db.Operations.Add(new Operation
        {
            AccountId = account.Id,
            ContractId = contract.Id,
            DocumentNumber = $"КАП-{_clock.UtcNow:yyyyMMddHHmmssfff}",
            Kind = OperationKind.Capitalization,
            Amount = total,
            Currency = account.Currency,
            Status = OperationStatus.Executed,
            ExecutedAt = _clock.UtcNow,
            Description = $"Капитализация процентов по договору {contract.Number} на {date:dd.MM.yyyy}",
            CreatedAt = _clock.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Пролонгация и возврат вкладов с наступившим сроком.</summary>
    private async Task ProcessContractsAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var contracts = await LoadActiveContractsAsync(cancellationToken);

        foreach (var contract in contracts)
        {
            if (contract.EndDate > date)
            {
                continue;
            }

            var version = contract.ProductVersion;
            var isDeposit = version?.Product?.Kind == ProductKind.Deposit;

            if (isDeposit && version != null && version.AllowsProlongation && contract.Principal > 0m)
            {
                contract.EndDate = SchedulePolicy.ProlongatedEndDate(contract, date);
                contract.ProlongationCount += 1;
                contract.Status = ContractStatus.Prolonged;
                continue;
            }

            if (contract.Principal > 0m)
            {
                var account = await LoadAccountAsync(contract, cancellationToken);
                var principal = contract.Principal;

                account.Balance = Money.Round(account.Balance - principal);

                _db.Operations.Add(new Operation
                {
                    AccountId = account.Id,
                    ContractId = contract.Id,
                    DocumentNumber = $"ВОЗВР-{_clock.UtcNow:yyyyMMddHHmmssfff}",
                    Kind = OperationKind.CashOut,
                    Amount = principal,
                    Currency = account.Currency,
                    Status = OperationStatus.Executed,
                    ExecutedAt = _clock.UtcNow,
                    Description = $"Возврат вклада по окончании срока по договору {contract.Number}",
                    CreatedAt = _clock.UtcNow
                });

                contract.Principal = 0m;
            }

            contract.Status = ContractStatus.Closed;
            contract.ClosedAt = _clock.UtcNow;

            if (contract.Account != null)
            {
                contract.Account.Status = AccountStatus.Closed;
                contract.Account.ClosedAt = _clock.UtcNow;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<Contract>> LoadActiveContractsAsync(CancellationToken cancellationToken) => await _db.Contracts
        .Include(c => c.ProductVersion)
        .ThenInclude(v => v.Product)
        .Include(c => c.Account)
        .ThenInclude(a => a.Numbers)
        .Include(c => c.Client)
        .Where(c => c.Status == ContractStatus.Active
                 || c.Status == ContractStatus.Prolonged
                 || c.Status == ContractStatus.Restricted)
        .ToListAsync(cancellationToken);

    private async Task<Account> LoadAccountAsync(Contract contract, CancellationToken cancellationToken)
    {
        if (contract.Account != null)
        {
            return contract.Account;
        }

        return await _db.Accounts
            .Include(a => a.Numbers)
            .FirstOrDefaultAsync(a => a.Id == contract.AccountId, cancellationToken)
            ?? throw new NotFoundException("Счёт договора не найден.");
    }
}
