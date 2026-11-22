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
/// Проведение операций по счетам: проверка доступного остатка по формуле (1.1),
/// расчёт досрочного возврата, изменение остатка и исполнение операции в одной транзакции.
/// Исполненная операция не изменяется: исправление выполняется сторнированием.
/// </summary>
public class OperationService
{
    /// <summary>Сериализация исполнения операций в процессе сервера: остаток проверяется и списывается атомарно.</summary>
    private static readonly SemaphoreSlim ExecutionLock = new(1, 1);

    private static readonly OperationKind[] ManualKinds =
    {
        OperationKind.CashIn,
        OperationKind.TransferIn,
        OperationKind.CashOut,
        OperationKind.TransferOut,
        OperationKind.PartialWithdrawal,
        OperationKind.EarlyReturn
    };

    private readonly IBankDbContext _db;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly ICurrentUser _current;
    private readonly IBalanceService _balance;
    private readonly ParameterService _parameters;
    private readonly IAuditWriter _audit;

    public OperationService(
        IBankDbContext db,
        IUnitOfWork uow,
        IClock clock,
        ICurrentUser current,
        IBalanceService balance,
        ParameterService parameters,
        IAuditWriter audit)
    {
        _db = db;
        _uow = uow;
        _clock = clock;
        _current = current;
        _balance = balance;
        _parameters = parameters;
        _audit = audit;
    }

    /// <summary>Проведение операции по счёту: проверка условий, доступного остатка и исполнение.</summary>
    public async Task<OperationDto> PostAsync(CreateOperationRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new AppException("Не переданы параметры операции.");
        }

        var account = await _db.Accounts
            .Include(a => a.Client)
            .Include(a => a.Numbers)
            .FirstOrDefaultAsync(a => a.Id == request.AccountId, cancellationToken);

        if (account == null)
        {
            throw new NotFoundException("Счёт не найден.");
        }

        if (!account.IsOpen)
        {
            throw new ConflictException("Операции по закрытому счёту невозможны.");
        }

        if (account.Client == null || !account.Client.IsIdentified)
        {
            throw new ConflictException("Операция невозможна без идентифицированного клиента.");
        }

        var kind = Parsing.Enum<OperationKind>(request.Kind, "вид операции");
        if (!ManualKinds.Contains(kind))
        {
            throw new AppException($"Операция «{kind.DisplayName()}» выполняется системой автоматически.");
        }

        var amount = Money.Round(request.Amount);
        if (amount <= 0m)
        {
            throw new AppException("Сумма операции должна быть больше нуля.");
        }

        Contract contract = null;
        if (request.ContractId.HasValue)
        {
            contract = await _db.Contracts
                .Include(c => c.ProductVersion)
                .ThenInclude(v => v.Product)
                .FirstOrDefaultAsync(c => c.Id == request.ContractId.Value, cancellationToken);

            if (contract == null)
            {
                throw new NotFoundException("Договор не найден.");
            }

            if (contract.AccountId != account.Id)
            {
                throw new AppException("Договор не относится к указанному счёту.");
            }
        }

        var description = request.Description;

        if (kind == OperationKind.EarlyReturn)
        {
            if (contract == null)
            {
                throw new AppException("Досрочный возврат возможен только по договору вклада.");
            }

            if (!contract.IsActive || contract.ProductVersion == null)
            {
                throw new ConflictException("Договор не действует.");
            }

            var version = contract.ProductVersion;
            if (version.RequiresBankConsentForEarlyReturn && !contract.HasEarlyReturnConsent)
            {
                throw new ConflictException(
                    "Безотзывный вклад возвращается досрочно только с согласия банка.");
            }

            var demandRate = await _parameters.GetValueAsync(ParameterService.DemandRate, _clock.Today, 0.1m, cancellationToken);
            var days = Math.Max(0, _clock.Today.DayNumber - contract.OpenDate.DayNumber);
            var quote = EarlyReturnCalculator.Calculate(
                contract.Principal,
                version.RatePercent,
                days,
                version.ResolveDayBase(_clock.Today),
                demandRate,
                contract.PaidInterest,
                version.RequiresBankConsentForEarlyReturn,
                contract.HasEarlyReturnConsent);

            amount = quote.NetPayout;
            description = $"{description}. Перерасчёт по ставке до востребования {quote.DemandRatePercent:0.00} %: "
                        + $"проценты {quote.DemandInterest:0.00} р., удержано излишне выплаченных {quote.ExcessToWithhold:0.00} р.";
        }

        var sequence = await _db.Operations.CountAsync(cancellationToken) + 1;
        var operation = new Operation
        {
            AccountId = account.Id,
            Account = account,
            ContractId = contract?.Id,
            Contract = contract,
            DocumentNumber = string.IsNullOrWhiteSpace(request.DocumentNumber)
                ? $"ДОК-{_clock.Today:yyyyMMdd}-{sequence:D5}"
                : request.DocumentNumber.Trim(),
            Kind = kind,
            Amount = amount,
            Currency = account.Currency,
            Status = OperationStatus.Draft,
            Description = description,
            CreatedBy = _current.UserId,
            CreatedAt = _clock.UtcNow
        };

        await ExecutionLock.WaitAsync(cancellationToken);
        try
        {
            await using var transaction = await _uow.BeginAsync(cancellationToken);

            var snapshot = await _balance.GetSnapshotAsync(account.Id, _clock.Today, cancellationToken);
            operation.CheckedBalance = snapshot.Balance;
            operation.CheckedOverdraft = snapshot.OverdraftLimit;
            operation.CheckedHolds = snapshot.HoldsAmount;
            operation.CheckedAvailable = snapshot.AvailableBalance;

            if (kind.IsDebit() && !BalanceCalculator.CanDebit(amount, snapshot.AvailableBalance))
            {
                operation.Status = OperationStatus.Rejected;
                operation.RejectionReason =
                    $"Недостаточно средств: доступный остаток {snapshot.AvailableBalance:0.00} р., требуется {amount:0.00} р.";
            }
            else
            {
                var minBalance = contract?.ProductVersion?.MinBalance ?? 0m;
                if (kind.IsDebit() && minBalance > 0m && snapshot.Balance - amount < minBalance)
                {
                    operation.Status = OperationStatus.Rejected;
                    operation.RejectionReason = $"Нарушен неснижаемый остаток {minBalance:0.00} р.";
                }
                else
                {
                    account.Balance = Money.Round(kind.IsDebit()
                        ? account.Balance - amount
                        : account.Balance + amount);

                    operation.Status = OperationStatus.Executed;
                    operation.ExecutedAt = _clock.UtcNow;

                    await ApplyContractEffectsAsync(operation, account, cancellationToken);
                }
            }

            _db.Operations.Add(operation);
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(
                "Операция не выполнена: остаток счёта изменён параллельной операцией. Повторите попытку.");
        }
        finally
        {
            ExecutionLock.Release();
        }

        await _audit.WriteAsync("OperationPosted", nameof(Operation), operation.Id.ToString(), null,
            new { Kind = kind.ToString(), operation.Amount, operation.Status, operation.CheckedAvailable }, cancellationToken);

        return await GetAsync(operation.Id, cancellationToken);
    }

    /// <summary>Сторнирование исполненной операции: обратная запись с сохранением исходной операции.</summary>
    public async Task<OperationDto> ReverseAsync(Guid id, ReverseRequest request, CancellationToken cancellationToken = default)
    {
        var reason = string.IsNullOrWhiteSpace(request?.Reason) ? "Ошибочная операция" : request.Reason.Trim();

        await ExecutionLock.WaitAsync(cancellationToken);
        try
        {
            await using var transaction = await _uow.BeginAsync(cancellationToken);
            var original = await LoadOperationAsync(id, cancellationToken);

            if (original.Status != OperationStatus.Executed)
            {
                throw new ConflictException("Сторнировать можно только исполненную операцию.");
            }

            if (!ManualKinds.Contains(original.Kind))
            {
                throw new ConflictException("Системные операции и досрочный возврат не сторнируются.");
            }

            var alreadyReversed = await _db.Operations
                .AnyAsync(o => o.ReversalOfId == original.Id, cancellationToken);
            if (alreadyReversed)
            {
                throw new ConflictException("Операция уже сторнирована.");
            }

            var sequence = await _db.Operations.CountAsync(cancellationToken) + 1;
            var reversal = new Operation
            {
                AccountId = original.AccountId,
                Account = original.Account,
                ContractId = original.ContractId,
                Contract = original.Contract,
                DocumentNumber = $"СТОРНО-{_clock.Today:yyyyMMdd}-{sequence:D5}",
                Kind = OperationKind.Reversal,
                Amount = original.Amount,
                Currency = original.Currency,
                Status = OperationStatus.Draft,
                Description = $"Сторно операции {original.DocumentNumber}: {reason}",
                ReversalOfId = original.Id,
                CreatedBy = _current.UserId,
                CreatedAt = _clock.UtcNow
            };

            var snapshot = await _balance.GetSnapshotAsync(original.AccountId, _clock.Today, cancellationToken);
            reversal.CheckedBalance = snapshot.Balance;
            reversal.CheckedOverdraft = snapshot.OverdraftLimit;
            reversal.CheckedHolds = snapshot.HoldsAmount;
            reversal.CheckedAvailable = snapshot.AvailableBalance;

            var increaseBalance = original.Kind.IsDebit();
            var account = original.Account ?? await _db.Accounts.FirstAsync(a => a.Id == original.AccountId, cancellationToken);
            account.Balance = Money.Round(increaseBalance
                ? account.Balance + original.Amount
                : account.Balance - original.Amount);

            reversal.Status = OperationStatus.Executed;
            reversal.ExecutedAt = _clock.UtcNow;

            original.Status = OperationStatus.Reversed;

            await ApplyReversalToContractAsync(original, cancellationToken);

            _db.Operations.Add(reversal);
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            await _audit.WriteAsync("OperationReversed", nameof(Operation), original.Id.ToString(),
                new { Status = OperationStatus.Executed.ToString() },
                new { Status = original.Status.ToString(), ReversalId = reversal.Id, Reason = reason }, cancellationToken);

            return await GetAsync(reversal.Id, cancellationToken);
        }
        finally
        {
            ExecutionLock.Release();
        }
    }

    public async Task<OperationDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var operation = await BaseQuery().FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (operation == null)
        {
            throw new NotFoundException("Операция не найдена.");
        }

        var dtos = await ToDtosAsync(new List<Operation> { operation }, cancellationToken);
        return dtos[0];
    }

    public async Task<IReadOnlyList<OperationDto>> QueryAsync(Guid? accountId, string status, int take, CancellationToken cancellationToken = default)
    {
        take = take is < 1 or > 500 ? 100 : take;
        var query = BaseQuery();

        if (accountId.HasValue)
        {
            query = query.Where(o => o.AccountId == accountId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var parsed = Parsing.Enum<OperationStatus>(status, "состояние операции");
            query = query.Where(o => o.Status == parsed);
        }

        var items = await query
            .OrderByDescending(o => o.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        return await ToDtosAsync(items, cancellationToken);
    }

    private IQueryable<Operation> BaseQuery() => _db.Operations
        .Include(o => o.Account)
        .ThenInclude(a => a.Client)
        .Include(o => o.Account)
        .ThenInclude(a => a.Numbers)
        .Include(o => o.Contract);

    private async Task<IReadOnlyList<OperationDto>> ToDtosAsync(IReadOnlyList<Operation> operations, CancellationToken cancellationToken)
    {
        var ids = operations
            .Select(o => o.CreatedBy)
            .Where(id => id.HasValue)
            .Select(id => id.Value)
            .Distinct()
            .ToList();

        var logins = ids.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.Users.Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Login, cancellationToken);

        return operations.Select(o => o.ToDto(logins)).ToList();
    }

    private async Task<Operation> LoadOperationAsync(Guid id, CancellationToken cancellationToken)
    {
        var operation = await _db.Operations
            .Include(o => o.Account)
            .ThenInclude(a => a.Client)
            .Include(o => o.Account)
            .ThenInclude(a => a.Numbers)
            .Include(o => o.Contract)
            .ThenInclude(c => c.ProductVersion)
            .ThenInclude(v => v.Product)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (operation == null)
        {
            throw new NotFoundException("Операция не найдена.");
        }

        return operation;
    }

    /// <summary>Влияние операции на договор вклада: тело вклада и досрочный возврат.</summary>
    private async Task ApplyContractEffectsAsync(Operation operation, Account account, CancellationToken cancellationToken)
    {
        if (!operation.ContractId.HasValue)
        {
            return;
        }

        var contract = operation.Contract ?? await _db.Contracts
            .Include(c => c.ProductVersion)
            .FirstOrDefaultAsync(c => c.Id == operation.ContractId.Value, cancellationToken);

        if (contract == null)
        {
            return;
        }

        switch (operation.Kind)
        {
            case OperationKind.CashIn:
            case OperationKind.TransferIn:
                contract.Principal = Money.Round(contract.Principal + operation.Amount);
                break;
            case OperationKind.CashOut:
            case OperationKind.TransferOut:
            case OperationKind.PartialWithdrawal:
                contract.Principal = Money.Round(contract.Principal - operation.Amount);
                break;
            case OperationKind.EarlyReturn:
                contract.Status = ContractStatus.EarlyReturned;
                contract.ClosedAt = _clock.UtcNow;
                contract.Principal = 0m;
                contract.AccruedInterest = 0m;
                account.Status = AccountStatus.Closed;
                account.ClosedAt = _clock.UtcNow;
                break;
        }
    }

    private async Task ApplyReversalToContractAsync(Operation original, CancellationToken cancellationToken)
    {
        if (!original.ContractId.HasValue)
        {
            return;
        }

        var contract = original.Contract ?? await _db.Contracts
            .FirstOrDefaultAsync(c => c.Id == original.ContractId.Value, cancellationToken);

        if (contract == null)
        {
            return;
        }

        switch (original.Kind)
        {
            case OperationKind.CashIn:
            case OperationKind.TransferIn:
                contract.Principal = Money.Round(Math.Max(0m, contract.Principal - original.Amount));
                break;
            case OperationKind.CashOut:
            case OperationKind.TransferOut:
            case OperationKind.PartialWithdrawal:
                contract.Principal = Money.Round(contract.Principal + original.Amount);
                break;
        }
    }
}
