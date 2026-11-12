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
/// Проведение операций по счетам (требования 7 и 8, таблицы 1.10 и 1.11):
/// проверка условий договора, расчёт доступного остатка по формуле (1.1), ПОД/ФТ-контроль,
/// маршрут контроля, повторная проверка D и списание в одной транзакции (требование 24),
/// сторнирование вместо изменения исполненной операции (бизнес-правило 13).
/// </summary>
public class OperationService
{
    /// <summary>
    /// Сериализация исполнения операций в процессе сервера: исключает одновременную
    /// проверку остатка двумя каналами (касса и ДБО) до списания (требование 24).
    /// </summary>
    private static readonly SemaphoreSlim ExecutionLock = new(1, 1);

    private readonly IBankDbContext _db;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly ICurrentUser _current;
    private readonly IBalanceService _balance;
    private readonly ParameterService _parameters;
    private readonly IAuditWriter _audit;
    private readonly IEventPublisher _events;

    public OperationService(
        IBankDbContext db,
        IUnitOfWork uow,
        IClock clock,
        ICurrentUser current,
        IBalanceService balance,
        ParameterService parameters,
        IAuditWriter audit,
        IEventPublisher events)
    {
        _db = db;
        _uow = uow;
        _clock = clock;
        _current = current;
        _balance = balance;
        _parameters = parameters;
        _audit = audit;
        _events = events;
    }

    public async Task<OperationDto> CreateAsync(CreateOperationRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new AppException("Не переданы параметры операции.");
        }

        var account = await _db.Accounts
            .Include(a => a.Client)
            .ThenInclude(c => c.KycProfile)
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
            throw new ConflictException("Операция невозможна без идентифицированного клиента (бизнес-правило 4).");
        }

        var kind = Parsing.Enum<OperationKind>(request.Kind, "вид операции");
        var source = Parsing.Enum<DocumentSource>(request.SourceSystem, "источник документа", DocumentSource.Branch);
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

        if (!string.IsNullOrWhiteSpace(request.ExternalId))
        {
            var externalId = request.ExternalId.Trim();
            var existingDocument = await _db.Documents
                .FirstOrDefaultAsync(d => d.SourceSystem == source && d.ExternalId == externalId, cancellationToken);

            if (existingDocument != null)
            {
                var existingOperation = await _db.Operations
                    .FirstOrDefaultAsync(o => o.DocumentId == existingDocument.Id, cancellationToken);

                throw new ConflictException(existingOperation != null
                    ? $"Документ {externalId} уже исполнен операцией {existingOperation.Id} (требование 23)."
                    : $"Документ {externalId} уже зарегистрирован в системе (требование 23).");
            }
        }

        var sequence = await _db.Operations.CountAsync(cancellationToken) + 1;

        var document = new OperationDocument
        {
            Number = string.IsNullOrWhiteSpace(request.DocumentNumber)
                ? $"ДОК-{_clock.Today:yyyyMMdd}-{sequence:D5}"
                : request.DocumentNumber.Trim(),
            SourceSystem = source,
            ExternalId = string.IsNullOrWhiteSpace(request.ExternalId) ? null : request.ExternalId.Trim(),
            Kind = kind.ToString(),
            ReceivedAt = _clock.UtcNow,
            ClientId = account.ClientId
        };

        var operation = new Operation
        {
            Account = account,
            Contract = contract,
            Document = document,
            Kind = kind,
            Amount = amount,
            Currency = account.Currency,
            Status = OperationStatus.Draft,
            Description = request.Description,
            CreatedBy = _current.UserId,
            CreatedAt = _clock.UtcNow,
            IdempotencyKey = Guid.NewGuid().ToString("N")
        };

        _db.Operations.Add(operation);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync("OperationCreated", nameof(Operation), operation.Id.ToString(), null,
            new { kind = kind.ToString(), operation.Amount, account.BalanceAccount }, cancellationToken);

        return await GetAsync(operation.Id, cancellationToken);
    }

    public async Task<OperationDto> SubmitAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await ExecutionLock.WaitAsync(cancellationToken);
        try
        {
            await using var transaction = await _uow.BeginAsync(cancellationToken);
            var operation = await LoadOperationAsync(id, cancellationToken);

            if (operation.Status != OperationStatus.Draft)
            {
                throw new ConflictException("На проверку можно отправить только операцию в состоянии «Черновик».");
            }

            await CheckAndRouteAsync(operation, cancellationToken);

            if (operation.Status == OperationStatus.Approved)
            {
                await ExecuteCoreAsync(operation, cancellationToken);
            }

            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            ExecutionLock.Release();
        }

        var result = await GetAsync(id, cancellationToken);
        await _audit.WriteAsync("OperationSubmitted", nameof(Operation), id.ToString(), null,
            new { result.Status, result.CheckedAvailable, result.RejectionReason }, cancellationToken);
        return result;
    }

    /// <summary>Подтверждение операции контролёром (UC04): автор не может подтвердить свою операцию.</summary>
    public async Task<OperationDto> ApproveAsync(Guid id, ApproveRequest request, CancellationToken cancellationToken = default)
    {
        await ExecutionLock.WaitAsync(cancellationToken);
        try
        {
            await using var transaction = await _uow.BeginAsync(cancellationToken);
            var operation = await LoadOperationAsync(id, cancellationToken);

            if (operation.Status != OperationStatus.OnControl)
            {
                throw new ConflictException("Подтверждение доступно только для операции в состоянии «На контроле».");
            }

            if (operation.CreatedBy.HasValue && operation.CreatedBy == _current.UserId)
            {
                throw new ForbiddenException("Автор операции не может подтвердить её сам (бизнес-правило 12, НФТ-9).");
            }

            operation.ApprovedBy = _current.UserId;
            operation.ApprovedAt = _clock.UtcNow;
            operation.Signature = string.IsNullOrWhiteSpace(request?.Signature)
                ? $"SIG-{_clock.UtcNow:yyyyMMddHHmmss}-{_current.Login}"
                : request.Signature;

            var snapshot = await _balance.GetSnapshotAsync(operation.AccountId, _clock.Today, cancellationToken);
            operation.CheckedBalance = snapshot.Balance;
            operation.CheckedOverdraft = snapshot.OverdraftLimit;
            operation.CheckedHolds = snapshot.HoldsAmount;
            operation.CheckedAvailable = snapshot.AvailableBalance;

            if (operation.Kind.IsDebit() && !BalanceCalculator.CanDebit(operation.Amount, snapshot.AvailableBalance))
            {
                operation.Status = OperationStatus.Rejected;
                operation.RejectionReason =
                    $"Изменился доступный остаток на момент подтверждения: доступно {snapshot.AvailableBalance:0.00} р., требуется {operation.Amount:0.00} р.";
            }
            else
            {
                operation.Status = OperationStatus.Approved;
                await ExecuteCoreAsync(operation, cancellationToken);
            }

            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            ExecutionLock.Release();
        }

        var result = await GetAsync(id, cancellationToken);
        await _audit.WriteAsync("OperationApproved", nameof(Operation), id.ToString(), null,
            new { result.Status, result.ApprovedBy, result.RejectionReason }, cancellationToken);
        return result;
    }

    public async Task<OperationDto> RejectAsync(Guid id, RejectRequest request, CancellationToken cancellationToken = default)
    {
        var operation = await LoadOperationAsync(id, cancellationToken);

        if (operation.Status is OperationStatus.Executed or OperationStatus.Posted or OperationStatus.Reversed)
        {
            throw new ConflictException("Исполненная операция не может быть отклонена: применяется сторнирование.");
        }

        if (operation.Status == OperationStatus.Rejected)
        {
            return await GetAsync(id, cancellationToken);
        }

        operation.Status = OperationStatus.Rejected;
        operation.RejectionReason = string.IsNullOrWhiteSpace(request?.Reason)
            ? "Операция отклонена без указания причины."
            : request.Reason.Trim();

        await SaveAsync(cancellationToken);
        await _audit.WriteAsync("OperationRejected", nameof(Operation), id.ToString(), null,
            new { operation.RejectionReason }, cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    /// <summary>Решение специалиста ПОД/ФТ по приостановленной операции (требование 27).</summary>
    public async Task<OperationDto> AmlDecisionAsync(Guid id, AmlDecisionRequest request, CancellationToken cancellationToken = default)
    {
        await ExecutionLock.WaitAsync(cancellationToken);
        try
        {
            await using var transaction = await _uow.BeginAsync(cancellationToken);
            var operation = await LoadOperationAsync(id, cancellationToken);

            if (operation.Status != OperationStatus.Suspended)
            {
                throw new ConflictException("Решение принимается только по приостановленной операции.");
            }

            if (request != null && request.Approve)
            {
                operation.Status = OperationStatus.Approved;
                operation.ApprovedBy = _current.UserId;
                operation.ApprovedAt = _clock.UtcNow;
                await ExecuteCoreAsync(operation, cancellationToken);
            }
            else
            {
                operation.Status = OperationStatus.Rejected;
                operation.RejectionReason = string.IsNullOrWhiteSpace(request?.Reason)
                    ? "Отказано по результатам ПОД/ФТ-контроля."
                    : request.Reason.Trim();
            }

            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            ExecutionLock.Release();
        }

        var result = await GetAsync(id, cancellationToken);
        await _audit.WriteAsync("AmlDecision", nameof(Operation), id.ToString(), null,
            new { request?.Approve, request?.Reason, result.Status }, cancellationToken);
        return result;
    }

    /// <summary>Сторнирование исполненной операции: обратная операция с записью в журнале.</summary>
    public async Task<OperationDto> ReverseAsync(Guid id, ReverseRequest request, CancellationToken cancellationToken = default)
    {
        await ExecutionLock.WaitAsync(cancellationToken);
        try
        {
            await using var transaction = await _uow.BeginAsync(cancellationToken);
            var original = await LoadOperationAsync(id, cancellationToken);

            if (original.Status is not (OperationStatus.Executed or OperationStatus.Posted))
            {
                throw new ConflictException("Сторнировать можно только исполненную или отражённую операцию.");
            }

            var reason = string.IsNullOrWhiteSpace(request?.Reason) ? "Ошибочная операция" : request.Reason.Trim();
            var originalPostings = await _db.Postings
                .Where(p => p.OperationId == original.Id)
                .ToListAsync(cancellationToken);

            foreach (var posting in originalPostings)
            {
                posting.IsReversed = true;
            }

            original.Status = OperationStatus.Reversed;

            var sequence = await _db.Operations.CountAsync(cancellationToken) + 1;
            var reversal = new Operation
            {
                AccountId = original.AccountId,
                Account = original.Account,
                ContractId = original.ContractId,
                Contract = original.Contract,
                Document = new OperationDocument
                {
                    Number = $"СТОРНО-{_clock.Today:yyyyMMdd}-{sequence:D5}",
                    SourceSystem = DocumentSource.Branch,
                    Kind = "Reversal",
                    ReceivedAt = _clock.UtcNow,
                    ClientId = original.Account?.ClientId
                },
                Kind = OperationKind.Reversal,
                Amount = original.Amount,
                Currency = original.Currency,
                Status = OperationStatus.Approved,
                Description = $"Сторно операции {original.Document?.Number}: {reason}",
                ReversalOfId = original.Id,
                CreatedBy = _current.UserId,
                ApprovedBy = _current.UserId,
                ApprovedAt = _clock.UtcNow,
                CreatedAt = _clock.UtcNow
            };

            _db.Operations.Add(reversal);
            await ExecuteCoreAsync(reversal, cancellationToken);
            await SaveAsync(cancellationToken);
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

    /// <summary>Очередь контроля: операции, требующие подтверждения контролёром (UC04).</summary>
    public async Task<IReadOnlyList<OperationDto>> QueueAsync(int take, CancellationToken cancellationToken = default)
    {
        var items = await BaseQuery()
            .Where(o => o.Status == OperationStatus.OnControl)
            .OrderBy(o => o.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        return await ToDtosAsync(items, cancellationToken);
    }

    public async Task<IReadOnlyList<OperationDto>> SuspendedAsync(int take, CancellationToken cancellationToken = default)
    {
        var items = await BaseQuery()
            .Where(o => o.Status == OperationStatus.Suspended)
            .OrderBy(o => o.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        return await ToDtosAsync(items, cancellationToken);
    }

    private IQueryable<Operation> BaseQuery() => _db.Operations
        .Include(o => o.Account)
        .ThenInclude(a => a.Client)
        .Include(o => o.Account)
        .ThenInclude(a => a.Numbers)
        .Include(o => o.Document)
        .Include(o => o.Contract);

    private async Task<IReadOnlyList<OperationDto>> ToDtosAsync(IReadOnlyList<Operation> operations, CancellationToken cancellationToken)
    {
        var ids = operations
            .SelectMany(o => new[] { o.CreatedBy, o.ApprovedBy })
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
            .ThenInclude(c => c.KycProfile)
            .Include(o => o.Account)
            .ThenInclude(a => a.Numbers)
            .Include(o => o.Document)
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

    /// <summary>Шаги 3–5 таблицы 1.10: договор, доступный остаток, ПОД/ФТ и маршрут контроля.</summary>
    private async Task CheckAndRouteAsync(Operation operation, CancellationToken cancellationToken)
    {
        operation.Status = OperationStatus.OnCheck;
        var contract = operation.Contract;

        if (operation.Kind == OperationKind.EarlyReturn)
        {
            if (contract == null)
            {
                Reject(operation, "Досрочный возврат возможен только по договору вклада.");
                return;
            }

            var version = contract.ProductVersion;
            if (version == null)
            {
                Reject(operation, "Не найдена версия условий договора.");
                return;
            }

            if (version.RequiresBankConsentForEarlyReturn && !contract.HasEarlyReturnConsent)
            {
                Reject(operation,
                    "Безотзывный вклад возвращается досрочно только с согласия банка (Декрет № 7, бизнес-правило 8).");
                return;
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

            operation.Amount = quote.NetPayout;
            operation.Description =
                $"{operation.Description}. Перерасчёт по ставке до востребования {quote.DemandRatePercent:0.00} %: " +
                $"проценты {quote.DemandInterest:0.00} р., удержано излишне выплаченных {quote.ExcessToWithhold:0.00} р.";
        }

        var snapshot = await _balance.GetSnapshotAsync(operation.AccountId, _clock.Today, cancellationToken);
        operation.CheckedBalance = snapshot.Balance;
        operation.CheckedOverdraft = snapshot.OverdraftLimit;
        operation.CheckedHolds = snapshot.HoldsAmount;
        operation.CheckedAvailable = snapshot.AvailableBalance;

        if (operation.Kind.IsDebit())
        {
            if (!BalanceCalculator.CanDebit(operation.Amount, snapshot.AvailableBalance))
            {
                Reject(operation,
                    $"Недостаточно средств: доступный остаток {D(snapshot):0.00} р., требуется {operation.Amount:0.00} р.");
                return;
            }

            var minBalance = contract?.ProductVersion?.MinBalance ?? 0m;
            if (minBalance > 0m && snapshot.Balance - operation.Amount < minBalance)
            {
                Reject(operation, $"Нарушен неснижаемый остаток {minBalance:0.00} р. (бизнес-правило 9).");
                return;
            }
        }

        var limits = await LoadLimitsAsync(cancellationToken);
        var highRisk = operation.Account?.Client?.KycProfile?.RiskLevel == KycRiskLevel.High;
        var amlSigns = WorkflowPolicy.HasAmlSigns(operation.Amount, limits.AmlThreshold, highRisk) && operation.Kind.IsCash();
        var route = WorkflowPolicy.Evaluate(operation.Amount, limits, amlSigns);

        switch (route)
        {
            case OperationRoute.AutoApprove:
                operation.Status = OperationStatus.Approved;
                operation.ApprovedBy = null;
                operation.ApprovedAt = null;
                break;
            case OperationRoute.Control:
                operation.Status = OperationStatus.OnControl;
                break;
            default:
                operation.Status = OperationStatus.Suspended;
                break;
        }

        await _events.PublishAsync("OperationChecked",
            new { operation.Id, Status = operation.Status.ToString(), operation.CheckedAvailable }, cancellationToken);
    }

    /// <summary>Исполнение операции с повторной проверкой доступного остатка (шаг 7 таблицы 1.10).</summary>
    private async Task ExecuteCoreAsync(Operation operation, CancellationToken cancellationToken)
    {
        var snapshot = await _balance.GetSnapshotAsync(operation.AccountId, _clock.Today, cancellationToken);
        operation.CheckedBalance = snapshot.Balance;
        operation.CheckedOverdraft = snapshot.OverdraftLimit;
        operation.CheckedHolds = snapshot.HoldsAmount;
        operation.CheckedAvailable = snapshot.AvailableBalance;

        var isDebit = await IsDebitEffectAsync(operation, cancellationToken);

        if (isDebit && !BalanceCalculator.CanDebit(operation.Amount, snapshot.AvailableBalance))
        {
            operation.Status = OperationStatus.Rejected;
            operation.RejectionReason =
                $"Изменился доступный остаток: доступно {snapshot.AvailableBalance:0.00} р., требуется {operation.Amount:0.00} р.";
            return;
        }

        var account = await _db.Accounts
            .Include(a => a.Numbers)
            .FirstOrDefaultAsync(a => a.Id == operation.AccountId, cancellationToken);

        if (account == null)
        {
            throw new NotFoundException("Счёт операции не найден.");
        }

        account.Balance = Money.Round(isDebit ? account.Balance - operation.Amount : account.Balance + operation.Amount);

        operation.Status = OperationStatus.Executed;
        operation.ExecutedAt = _clock.UtcNow;

        await AddPostingAsync(operation, account, isDebit, cancellationToken);
        await ApplyContractEffectsAsync(operation, cancellationToken);

        await _events.PublishAsync("OperationExecuted",
            new { operation.Id, operation.Amount, Balance = account.Balance }, cancellationToken);
    }

    /// <summary>Направление влияния на остаток: для сторно оно противоположно исходной операции.</summary>
    private async Task<bool> IsDebitEffectAsync(Operation operation, CancellationToken cancellationToken)
    {
        if (operation.Kind != OperationKind.Reversal || !operation.ReversalOfId.HasValue)
        {
            return operation.Kind.IsDebit();
        }

        var originalKind = await _db.Operations
            .Where(o => o.Id == operation.ReversalOfId.Value)
            .Select(o => (OperationKind?)o.Kind)
            .FirstOrDefaultAsync(cancellationToken);

        return originalKind.HasValue && !originalKind.Value.IsDebit();
    }

    private async Task AddPostingAsync(Operation operation, Account account, bool isDebit, CancellationToken cancellationToken)
    {
        if (operation.Kind == OperationKind.Reversal && operation.ReversalOfId.HasValue)
        {
            var originals = await _db.Postings
                .Where(p => p.OperationId == operation.ReversalOfId.Value)
                .ToListAsync(cancellationToken);

            foreach (var original in originals)
            {
                operation.Postings.Add(new Posting
                {
                    DebitAccount = original.CreditAccount,
                    CreditAccount = original.DebitAccount,
                    Currency = original.Currency,
                    Amount = original.Amount,
                    Purpose = $"Сторно: {original.Purpose}"
                });
            }

            return;
        }

        var contraAccount = operation.Kind switch
        {
            OperationKind.CashIn => "2020",
            OperationKind.CashOut => "2020",
            OperationKind.PartialWithdrawal => "2020",
            OperationKind.EarlyReturn => "2020",
            OperationKind.InterestPayment => "4001",
            OperationKind.Capitalization => "4001",
            OperationKind.TaxWithholding => "6001",
            _ => "1201"
        };

        var debitAccount = isDebit ? account.BalanceAccount : contraAccount;
        var creditAccount = isDebit ? contraAccount : account.BalanceAccount;

        operation.Postings.Add(new Posting
        {
            DebitAccount = debitAccount,
            CreditAccount = creditAccount,
            Currency = operation.Currency,
            Amount = operation.Amount,
            Purpose = operation.Kind.DisplayName()
        });
    }

    /// <summary>Влияние операции на договор: тело вклада, досрочный возврат, восстановление при сторно.</summary>
    private async Task ApplyContractEffectsAsync(Operation operation, CancellationToken cancellationToken)
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
                if (contract.Account != null)
                {
                    contract.Account.Status = AccountStatus.Closed;
                    contract.Account.ClosedAt = _clock.UtcNow;
                }

                break;
            case OperationKind.Reversal:
                if (operation.ReversalOfId.HasValue)
                {
                    var original = await _db.Operations
                        .FirstOrDefaultAsync(o => o.Id == operation.ReversalOfId.Value, cancellationToken);

                    if (original != null)
                    {
                        if (original.Kind == OperationKind.EarlyReturn)
                        {
                            contract.Status = ContractStatus.Active;
                            contract.ClosedAt = null;
                        }
                    }
                }

                break;
        }
    }

    private async Task<WorkflowLimits> LoadLimitsAsync(CancellationToken cancellationToken)
    {
        var operatorLimit = await _parameters.GetValueAsync(ParameterService.OperatorLimit, _clock.Today, 5000m, cancellationToken);
        var amlThreshold = await _parameters.GetValueAsync(ParameterService.AmlThreshold, _clock.Today, 20000m, cancellationToken);
        return new WorkflowLimits { OperatorLimit = operatorLimit, AmlThreshold = amlThreshold };
    }

    private static void Reject(Operation operation, string reason)
    {
        operation.Status = OperationStatus.Rejected;
        operation.RejectionReason = reason;
    }

    /// <summary>Доступный остаток для записи причины отказа с расшифровкой B, L, H.</summary>
    private static decimal D(BalanceSnapshotDto snapshot) => snapshot.AvailableBalance;

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(
                "Операция не выполнена: остаток счёта изменён параллельной операцией. Повторите попытку (требование 24).");
        }
    }
}
