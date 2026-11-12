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
/// Счета по Плану счетов № 126 (требование 3): прежний и изменённый номер,
/// ограничения и доступный остаток, лимит овердрафта, закрытие счёта.
/// </summary>
public class AccountService
{
    private readonly IBankDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditWriter _audit;
    private readonly IBalanceService _balance;
    private readonly ICurrentUser _current;

    public AccountService(IBankDbContext db, IClock clock, IAuditWriter audit, IBalanceService balance, ICurrentUser current)
    {
        _db = db;
        _clock = clock;
        _audit = audit;
        _balance = balance;
        _current = current;
    }

    public async Task<IReadOnlyList<AccountListItemDto>> ListByClientAsync(Guid clientId, CancellationToken cancellationToken = default)
    {
        var accounts = await _db.Accounts
            .Include(a => a.Client)
            .Include(a => a.Numbers)
            .Include(a => a.Contracts)
            .ThenInclude(c => c.ProductVersion)
            .ThenInclude(v => v.Product)
            .Where(a => a.ClientId == clientId)
            .OrderBy(a => a.BalanceAccount)
            .ToListAsync(cancellationToken);

        var result = new List<AccountListItemDto>();
        foreach (var account in accounts)
        {
            var available = await _balance.GetAvailableAsync(account, _clock.Today, cancellationToken);
            var contract = account.Contracts?.OrderByDescending(c => c.OpenDate).FirstOrDefault();
            result.Add(account.ToListItem(_clock.Today, available, contract));
        }

        return result;
    }

    public async Task<AccountDetailsDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var account = await LoadAccountAsync(id, cancellationToken);
        return await BuildDetailsAsync(account, cancellationToken);
    }

    /// <summary>Поиск счёта по прежнему или изменённому номеру (требование 3).</summary>
    public async Task<AccountDetailsDto> GetByNumberAsync(string number, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(number))
        {
            throw new AppException("Укажите номер счёта.");
        }

        var digits = new string(number.Where(char.IsDigit).ToArray());
        var accountNumber = await _db.AccountNumbers
            .FirstOrDefaultAsync(n => n.Number == number || n.Number == digits, cancellationToken);

        if (accountNumber == null)
        {
            throw new NotFoundException($"Счёт с номером {number} не найден.");
        }

        return await GetAsync(accountNumber.AccountId, cancellationToken);
    }

    public async Task<AccountDetailsDto> OpenAsync(OpenAccountRequest request, CancellationToken cancellationToken = default)
    {
        var client = await _db.Clients
            .Include(c => c.KycProfile)
            .FirstOrDefaultAsync(c => c.Id == request.ClientId, cancellationToken);

        if (client == null)
        {
            throw new NotFoundException("Клиент не найден.");
        }

        if (!client.IsIdentified)
        {
            throw new AppException("Открытие счёта невозможно без идентификации клиента (требование 1).", 409);
        }

        if (client.KycProfile == null || client.KycProfile.IsOverdue(_clock.Today))
        {
            throw new AppException("Анкета ПОД/ФТ отсутствует или требует актуализации.", 409);
        }

        var product = await _db.Products
            .Include(p => p.Versions)
            .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.IsActive, cancellationToken);

        if (product == null)
        {
            throw new NotFoundException("Продукт не найден или неактивен.");
        }

        var version = product.CurrentVersion(_clock.Today);
        if (version == null)
        {
            throw new AppException("У продукта нет утверждённой версии условий.", 409);
        }

        var currency = string.IsNullOrWhiteSpace(request.Currency) ? product.Currency : request.Currency.Trim().ToUpperInvariant();
        var sequence = await _db.Accounts.CountAsync(a => a.BalanceAccount == product.BalanceAccount, cancellationToken) + 1;

        var account = new Account
        {
            ClientId = client.Id,
            BalanceAccount = product.BalanceAccount,
            Currency = currency,
            Balance = 0m,
            OverdraftLimit = request.OverdraftLimit < 0m ? 0m : request.OverdraftLimit,
            Status = AccountStatus.Open,
            CreatedBy = _current.UserId,
            CreatedAt = _clock.UtcNow
        };

        var previousNumber = AccountNumberGenerator.Build(product.BalanceAccount, client.Id, sequence, NumberKind.Previous);
        var modifiedNumber = AccountNumberGenerator.Build(product.BalanceAccount, client.Id, sequence, NumberKind.Modified);
        var modifiedFrom = _clock.Today >= AccountNumberGenerator.ModifiedNumbersStart
            ? _clock.Today
            : AccountNumberGenerator.ModifiedNumbersStart;

        account.Numbers.Add(new AccountNumber { Number = previousNumber, Kind = NumberKind.Previous, ValidFrom = _clock.Today });
        account.Numbers.Add(new AccountNumber { Number = modifiedNumber, Kind = NumberKind.Modified, ValidFrom = modifiedFrom });

        _db.Accounts.Add(account);

        var termMonths = version.TermMonths > 0 ? version.TermMonths : 12;
        var endDate = product.Kind == ProductKind.CurrentAccount
            ? DateOnly.MaxValue
            : SchedulePolicy.AddMonthsClamped(_clock.Today, termMonths);

        var contract = new Contract
        {
            Number = $"Д-{_clock.Today:yyyyMMdd}-{sequence:D4}",
            ClientId = client.Id,
            Account = account,
            ProductVersionId = version.Id,
            OpenDate = _clock.Today,
            EndDate = endDate,
            Principal = request.InitialAmount,
            Status = ContractStatus.Active,
            CreatedBy = _current.UserId
        };

        _db.Contracts.Add(contract);

        if (request.InitialAmount > 0m)
        {
            var amount = Money.Round(request.InitialAmount);
            account.Balance += amount;

            var document = new OperationDocument
            {
                Number = $"ПКО-{_clock.Today:yyyyMMdd}-{sequence:D4}",
                SourceSystem = DocumentSource.Branch,
                Kind = "CashIn",
                ReceivedAt = _clock.UtcNow,
                ClientId = client.Id
            };

            var operation = new Operation
            {
                Account = account,
                Contract = contract,
                Document = document,
                Kind = OperationKind.CashIn,
                Amount = amount,
                Currency = currency,
                Status = OperationStatus.Executed,
                Description = "Первоначальный взнос при открытии счёта",
                ExecutedAt = _clock.UtcNow,
                ApprovedBy = _current.UserId,
                ApprovedAt = _clock.UtcNow,
                CreatedBy = _current.UserId,
                CheckedBalance = account.Balance - amount,
                CheckedAvailable = account.Balance
            };

            operation.Postings.Add(new Posting
            {
                DebitAccount = "2020",
                CreditAccount = account.BalanceAccount,
                Currency = currency,
                Amount = amount,
                Purpose = "Первоначальный взнос"
            });

            _db.Operations.Add(operation);
        }

        await _db.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync("AccountOpened", nameof(Account), account.Id.ToString(), null,
            new { account.BalanceAccount, previousNumber, modifiedNumber, Contract = contract.Number }, cancellationToken);

        return await GetAsync(account.Id, cancellationToken);
    }

    public async Task<AccountDetailsDto> SetOverdraftAsync(Guid id, decimal limit, CancellationToken cancellationToken = default)
    {
        var account = await LoadAccountAsync(id, cancellationToken);

        if (limit < 0m)
        {
            throw new AppException("Лимит овердрафта не может быть отрицательным.");
        }

        var old = new { account.OverdraftLimit };
        account.OverdraftLimit = Money.Round(limit);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("OverdraftChanged", nameof(Account), account.Id.ToString(), old,
            new { account.OverdraftLimit }, cancellationToken);

        return await BuildDetailsAsync(account, cancellationToken);
    }

    public async Task<AccountDetailsDto> CloseAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var account = await LoadAccountAsync(id, cancellationToken);

        var snapshot = await _balance.GetSnapshotAsync(id, _clock.Today, cancellationToken);
        if (snapshot.HasArrest)
        {
            throw new ConflictException("Закрытие невозможно: по счёту действует арест или приостановление операций.");
        }

        if (snapshot.AvailableBalance != 0m)
        {
            throw new ConflictException($"Закрытие невозможно: на счёте остались средства ({snapshot.AvailableBalance:0.00} р.).");
        }

        account.Status = AccountStatus.Closed;
        account.ClosedAt = _clock.UtcNow;

        foreach (var contract in account.Contracts.Where(c => c.IsActive))
        {
            contract.Status = ContractStatus.Closed;
            contract.ClosedAt = _clock.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("AccountClosed", nameof(Account), account.Id.ToString(), null,
            new { account.Status, account.ClosedAt }, cancellationToken);

        return await BuildDetailsAsync(account, cancellationToken);
    }

    private async Task<Account> LoadAccountAsync(Guid id, CancellationToken cancellationToken)
    {
        var account = await _db.Accounts
            .Include(a => a.Client)
            .Include(a => a.Numbers)
            .Include(a => a.Holds)
            .Include(a => a.Contracts)
            .ThenInclude(c => c.ProductVersion)
            .ThenInclude(v => v.Product)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (account == null)
        {
            throw new NotFoundException("Счёт не найден.");
        }

        return account;
    }

    private async Task<AccountDetailsDto> BuildDetailsAsync(Account account, CancellationToken cancellationToken)
    {
        var snapshot = await _balance.GetSnapshotAsync(account.Id, _clock.Today, cancellationToken);
        var contract = account.Contracts?.OrderByDescending(c => c.OpenDate).FirstOrDefault();

        var operations = await _db.Operations
            .Include(o => o.Account)
            .ThenInclude(a => a.Numbers)
            .Include(o => o.Account)
            .ThenInclude(a => a.Client)
            .Include(o => o.Document)
            .Include(o => o.Contract)
            .Where(o => o.AccountId == account.Id)
            .OrderByDescending(o => o.CreatedAt)
            .Take(10)
            .ToListAsync(cancellationToken);

        var logins = await LoadLoginsAsync(operations, cancellationToken);
        var recent = operations.Select(o => o.ToDto(logins)).ToList();

        return account.ToDetails(snapshot, contract, recent);
    }

    private async Task<IReadOnlyDictionary<Guid, string>> LoadLoginsAsync(IEnumerable<Operation> operations, CancellationToken cancellationToken)
    {
        var ids = operations
            .SelectMany(o => new[] { o.CreatedBy, o.ApprovedBy })
            .Where(id => id.HasValue)
            .Select(id => id.Value)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await _db.Users
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Login, cancellationToken);
    }
}
