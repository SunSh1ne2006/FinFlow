using FinFlow.Application.Abstractions;
using FinFlow.Application.Common;
using FinFlow.Application.Mapping;
using FinFlow.Contracts;
using FinFlow.Domain.Calculators;
using FinFlow.Domain.Common;
using FinFlow.Domain.Entities;
using FinFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Application.Services;

/// <summary>
/// Закрытие операционного дня (п. 1.6.3, требование 12). Этапы фиксируются в OperationalDays,
/// поэтому повторный запуск продолжает обработку с того же этапа и не создаёт повторных начислений.
/// </summary>
public class DayCloseService
{
    private readonly IBankDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditWriter _audit;
    private readonly InterestService _interest;
    private readonly TaxService _tax;
    private readonly ILedgerGateway _ledger;
    private readonly IEventPublisher _events;

    public DayCloseService(
        IBankDbContext db,
        IClock clock,
        IAuditWriter audit,
        InterestService interest,
        TaxService tax,
        ILedgerGateway ledger,
        IEventPublisher events)
    {
        _db = db;
        _clock = clock;
        _audit = audit;
        _interest = interest;
        _tax = tax;
        _ledger = ledger;
        _events = events;
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
            if ((int)day.Stage < (int)DayCloseStage.PendingRejected)
            {
                await RejectUnconfirmedAsync(day, date, cancellationToken);
                day.Stage = DayCloseStage.PendingRejected;
                await _db.SaveChangesAsync(cancellationToken);
            }

            if ((int)day.Stage < (int)DayCloseStage.AccrualsDone)
            {
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
                await ProcessContractsAsync(day, date, cancellationToken);
                day.Stage = DayCloseStage.ContractsDone;
                await _db.SaveChangesAsync(cancellationToken);
            }

            if ((int)day.Stage < (int)DayCloseStage.PostingsDone)
            {
                await PostingsAsync(day, date, cancellationToken);
                day.Stage = DayCloseStage.PostingsDone;
                await _db.SaveChangesAsync(cancellationToken);
            }

            if ((int)day.Stage < (int)DayCloseStage.Reconciled)
            {
                var reconciliation = await ReconcileAsync(date, cancellationToken);
                if (!reconciliation.IsBalanced)
                {
                    throw new AppException($"Сверка с главной книгой выявила расхождение {reconciliation.Difference:0.00} р.", 500);
                }

                day.Stage = DayCloseStage.Reconciled;
                await _db.SaveChangesAsync(cancellationToken);
            }

            day.Status = OperationalDayStatus.Closed;
            day.Stage = DayCloseStage.Completed;
            day.ClosedAt = _clock.UtcNow;
            day.LastError = null;
            await _db.SaveChangesAsync(cancellationToken);

            await _audit.WriteAsync("DayClosed", nameof(OperationalDay), day.Id.ToString(), null,
                new { day.Date, day.AccrualCount, day.PostingCount, day.InterestAccrued, day.TaxWithheld }, cancellationToken);
            await _events.PublishAsync("DayClosed", new { day.Date, day.InterestAccrued }, cancellationToken);
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

    /// <summary>Шаг 1: операции без подтверждения отклоняются (A3 UC04), перенос на следующий день запрещён.</summary>
    private async Task RejectUnconfirmedAsync(OperationalDay day, DateOnly date, CancellationToken cancellationToken)
    {
        var pending = await _db.Operations
            .Where(o => o.Status == OperationStatus.Draft
                     || o.Status == OperationStatus.OnCheck
                     || o.Status == OperationStatus.OnControl)
            .ToListAsync(cancellationToken);

        foreach (var operation in pending)
        {
            operation.Status = OperationStatus.Rejected;
            operation.RejectionReason = "Не подтверждена в течение операционного дня (A3 UC04).";
        }

        day.RejectedOperations = pending.Count;
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Шаг 2: начисление процентов по формуле (1.5) по всем действующим договорам.</summary>
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

            _db.Postings.Add(new Posting
            {
                DayId = day.Id,
                DebitAccount = "7001",
                CreditAccount = "4001",
                Currency = contract.Account?.Currency ?? "BYN",
                Amount = accrual.Amount,
                Purpose = $"Начисление процентов {date:dd.MM.yyyy} по договору {contract.Number}"
            });

            day.AccrualCount += 1;
            day.InterestAccrued = Money.Round(day.InterestAccrued + accrual.Amount);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Шаг 3: капитализация или выплата процентов с удержанием налога по формуле (1.8).</summary>
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
            Document = NewSystemDocument("ВЫПЛ", OperationKind.InterestPayment, contract.ClientId),
            Kind = OperationKind.InterestPayment,
            Amount = total,
            Currency = account.Currency,
            Status = OperationStatus.Executed,
            ExecutedAt = _clock.UtcNow,
            Description = $"Выплата процентов по договору {contract.Number} на {date:dd.MM.yyyy}",
            CreatedAt = _clock.UtcNow
        };

        payment.Postings.Add(new Posting
        {
            DebitAccount = "4001",
            CreditAccount = account.BalanceAccount,
            Currency = account.Currency,
            Amount = total,
            Purpose = "Выплата процентов"
        });

        _db.Operations.Add(payment);

        if (taxTotal > 0m)
        {
            account.Balance = Money.Round(account.Balance - taxTotal);

            var taxOperation = new Operation
            {
                AccountId = account.Id,
                ContractId = contract.Id,
                Document = NewSystemDocument("НАЛОГ", OperationKind.TaxWithholding, contract.ClientId),
                Kind = OperationKind.TaxWithholding,
                Amount = taxTotal,
                Currency = account.Currency,
                Status = OperationStatus.Executed,
                ExecutedAt = _clock.UtcNow,
                Description = $"Удержание подоходного налога с процентов по договору {contract.Number}",
                CreatedAt = _clock.UtcNow
            };

            taxOperation.Postings.Add(new Posting
            {
                DebitAccount = account.BalanceAccount,
                CreditAccount = "6001",
                Currency = account.Currency,
                Amount = taxTotal,
                Purpose = "Подоходный налог с процентов"
            });

            _db.Operations.Add(taxOperation);
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

        var operation = new Operation
        {
            AccountId = account.Id,
            ContractId = contract.Id,
            Document = NewSystemDocument("КАП", OperationKind.Capitalization, contract.ClientId),
            Kind = OperationKind.Capitalization,
            Amount = total,
            Currency = account.Currency,
            Status = OperationStatus.Executed,
            ExecutedAt = _clock.UtcNow,
            Description = $"Капитализация процентов по договору {contract.Number} на {date:dd.MM.yyyy}",
            CreatedAt = _clock.UtcNow
        };

        operation.Postings.Add(new Posting
        {
            DebitAccount = "7001",
            CreditAccount = account.BalanceAccount,
            Currency = account.Currency,
            Amount = total,
            Purpose = "Капитализация процентов"
        });

        _db.Operations.Add(operation);
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Шаг 4: пролонгация и возврат вкладов с наступившим сроком.</summary>
    private async Task ProcessContractsAsync(OperationalDay day, DateOnly date, CancellationToken cancellationToken)
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

                var payout = new Operation
                {
                    AccountId = account.Id,
                    ContractId = contract.Id,
                    Document = NewSystemDocument("ВОЗВР", OperationKind.CashOut, contract.ClientId),
                    Kind = OperationKind.CashOut,
                    Amount = principal,
                    Currency = account.Currency,
                    Status = OperationStatus.Executed,
                    ExecutedAt = _clock.UtcNow,
                    Description = $"Возврат вклада по окончании срока по договору {contract.Number}",
                    CreatedAt = _clock.UtcNow
                };

                payout.Postings.Add(new Posting
                {
                    DebitAccount = account.BalanceAccount,
                    CreditAccount = "2020",
                    Currency = account.Currency,
                    Amount = principal,
                    Purpose = "Возврат вклада"
                });

                _db.Operations.Add(payout);
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

    /// <summary>Шаг 5: передача проводок в главную книгу АБС.</summary>
    private async Task PostingsAsync(OperationalDay day, DateOnly date, CancellationToken cancellationToken)
    {
        var from = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var to = from.AddDays(1);

        var postings = await _db.Postings
            .Where(p => p.CreatedAt >= from && p.CreatedAt < to)
            .ToListAsync(cancellationToken);

        foreach (var posting in postings)
        {
            posting.IsLedgerPosted = true;
        }

        day.PostingCount = postings.Count;
        await _db.SaveChangesAsync(cancellationToken);

        await _ledger.SendPostingsAsync(postings.Select(p => p.Id).ToList(), cancellationToken);
    }

    /// <summary>Сверка оборотов с главной книгой (требование 21, UC27).</summary>
    public async Task<ReconciliationDto> ReconcileAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var from = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var to = from.AddDays(1);

        var postings = await _db.Postings
            .Where(p => p.CreatedAt >= from && p.CreatedAt < to)
            .ToListAsync(cancellationToken);

        var debit = Money.Round(postings.Sum(p => p.Amount));
        var credit = Money.Round(postings.Sum(p => p.Amount));
        var (ledgerDebit, ledgerCredit) = await _ledger.GetTurnoverAsync(date, cancellationToken);

        var accrualTotal = await _db.InterestAccruals
            .Where(a => a.AccrualDate == date)
            .SumAsync(a => (decimal?)a.Amount, cancellationToken) ?? 0m;

        var taxTotal = await _db.TaxWithholdings
            .Where(t => t.WithheldAt >= from && t.WithheldAt < to)
            .SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0m;

        var difference = Money.Round(debit - ledgerDebit);
        var isBalanced = difference == 0m && Money.Round(credit - ledgerCredit) == 0m;

        return new ReconciliationDto
        {
            Date = date,
            DebitTotal = debit,
            CreditTotal = credit,
            Difference = difference,
            PostingCount = postings.Count,
            AccrualTotal = Money.Round(accrualTotal),
            TaxTotal = Money.Round(taxTotal),
            IsBalanced = isBalanced,
            Message = isBalanced
                ? $"Сверка выполнена: обороты совпадают, расхождений нет ({postings.Count} проводок)."
                : $"Расхождение с главной книгой: {difference:0.00} р."
        };
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

    private OperationDocument NewSystemDocument(string prefix, OperationKind kind, Guid? clientId) => new()
    {
        Number = $"{prefix}-{_clock.UtcNow:yyyyMMddHHmmssfff}",
        SourceSystem = DocumentSource.System,
        Kind = kind.ToString(),
        ReceivedAt = _clock.UtcNow,
        ClientId = clientId
    };
}
