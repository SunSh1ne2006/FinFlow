using FinFlow.Application.Abstractions;
using FinFlow.Application.Common;
using FinFlow.Application.Mapping;
using FinFlow.Contracts;
using FinFlow.Domain.Calculators;
using FinFlow.Domain.Entities;
using FinFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Application.Services;

/// <summary>
/// Договоры вкладов и текущих счетов (требование 4): открытие, согласие банка
/// на досрочный возврат безотзывного вклада, расчёт досрочного возврата (формулы 1.9, 1.10).
/// </summary>
public class ContractService
{
    private readonly IBankDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditWriter _audit;
    private readonly ICurrentUser _current;
    private readonly ParameterService _parameters;
    private readonly AccountService _accounts;

    public ContractService(
        IBankDbContext db,
        IClock clock,
        IAuditWriter audit,
        ICurrentUser current,
        ParameterService parameters,
        AccountService accounts)
    {
        _db = db;
        _clock = clock;
        _audit = audit;
        _current = current;
        _parameters = parameters;
        _accounts = accounts;
    }

    public async Task<IReadOnlyList<ContractDto>> ListAsync(Guid? clientId, string status, CancellationToken cancellationToken = default)
    {
        var query = _db.Contracts
            .Include(c => c.Client)
            .Include(c => c.Account)
            .ThenInclude(a => a.Numbers)
            .Include(c => c.ProductVersion)
            .ThenInclude(v => v.Product)
            .AsQueryable();

        if (clientId.HasValue)
        {
            query = query.Where(c => c.ClientId == clientId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var parsed = Parsing.Enum<ContractStatus>(status, "состояние договора");
            query = query.Where(c => c.Status == parsed);
        }

        var contracts = await query
            .OrderByDescending(c => c.OpenDate)
            .Take(200)
            .ToListAsync(cancellationToken);

        return contracts.Select(c => c.ToDto()).ToList();
    }

    public async Task<ContractDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var contract = await LoadContractAsync(id, cancellationToken);
        return contract.ToDto();
    }

    public async Task<ContractDto> OpenAsync(OpenContractRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null || request.Amount <= 0m)
        {
            throw new AppException("Укажите сумму вклада.");
        }

        var version = await _db.ProductVersions
            .Include(v => v.Product)
            .FirstOrDefaultAsync(v => v.Id == request.ProductVersionId, cancellationToken);

        if (version == null)
        {
            throw new NotFoundException("Версия условий продукта не найдена.");
        }

        if (!version.IsApproved)
        {
            throw new ConflictException("Нельзя открыть договор по неутверждённой версии условий.");
        }

        if (request.Amount < version.MinAmount)
        {
            throw new AppException($"Минимальная сумма по продукту – {version.MinAmount:0.00} р.");
        }

        var account = await _accounts.OpenAsync(new OpenAccountRequest
        {
            ClientId = request.ClientId,
            ProductId = version.ProductId,
            Currency = version.Product.Currency,
            InitialAmount = request.Amount
        }, cancellationToken);

        if (request.TermMonths.HasValue && request.TermMonths.Value > 0 && account.Contract != null)
        {
            var contract = await LoadContractAsync(account.Contract.Id, cancellationToken);
            contract.EndDate = SchedulePolicy.AddMonthsClamped(contract.OpenDate, request.TermMonths.Value);
            contract.Notes = request.Notes;
            await _db.SaveChangesAsync(cancellationToken);
            return contract.ToDto();
        }

        return account.Contract;
    }

    /// <summary>Согласие банка на досрочный возврат безотзывного вклада (бизнес-правило 8).</summary>
    public async Task<ContractDto> ConsentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var contract = await LoadContractAsync(id, cancellationToken);

        if (!contract.ProductVersion.RequiresBankConsentForEarlyReturn)
        {
            throw new AppException("Для этого вклада согласие банка не требуется: вклад отзывный.");
        }

        if (contract.Status is ContractStatus.Closed or ContractStatus.EarlyReturned)
        {
            throw new ConflictException("Договор уже закрыт.");
        }

        contract.EarlyReturnConsentBy = _current.UserId;
        contract.EarlyReturnConsentAt = _clock.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("EarlyReturnConsent", nameof(Contract), contract.Id.ToString(), null,
            new { contract.EarlyReturnConsentBy, contract.EarlyReturnConsentAt }, cancellationToken);

        return contract.ToDto();
    }

    public async Task<EarlyReturnQuoteDto> QuoteEarlyReturnAsync(Guid id, int? days, CancellationToken cancellationToken = default)
    {
        var contract = await LoadContractAsync(id, cancellationToken);

        if (!contract.IsActive)
        {
            throw new ConflictException("Договор не действует.");
        }

        var version = contract.ProductVersion;
        var actualDays = days ?? (int)Math.Max(0, (_clock.Today.DayNumber - contract.OpenDate.DayNumber));
        var yearDays = version.ResolveDayBase(_clock.Today);
        var demandRate = await _parameters.GetValueAsync(ParameterService.DemandRate, _clock.Today, 0.1m, cancellationToken);

        var quote = EarlyReturnCalculator.Calculate(
            contract.Principal,
            version.RatePercent,
            actualDays,
            yearDays,
            demandRate,
            contract.PaidInterest,
            version.RequiresBankConsentForEarlyReturn,
            contract.HasEarlyReturnConsent);

        return new EarlyReturnQuoteDto
        {
            ContractId = contract.Id,
            Principal = quote.Principal,
            Days = quote.Days,
            YearDays = quote.YearDays,
            ContractRatePercent = quote.ContractRatePercent,
            DemandRatePercent = quote.DemandRatePercent,
            ContractInterest = quote.ContractInterest,
            DemandInterest = quote.DemandInterest,
            PaidInterest = quote.PaidInterest,
            ExcessToWithhold = quote.ExcessToWithhold,
            NetPayout = quote.NetPayout,
            RequiresBankConsent = quote.RequiresBankConsent,
            ConsentGranted = quote.ConsentGranted,
            Allowed = quote.Allowed,
            Reason = quote.Reason
        };
    }

    public async Task<IReadOnlyList<InterestAccrualDto>> GetAccrualsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var accruals = await _db.InterestAccruals
            .Where(a => a.ContractId == id)
            .OrderByDescending(a => a.AccrualDate)
            .Take(200)
            .ToListAsync(cancellationToken);

        return accruals.Select(a => a.ToDto()).ToList();
    }

    private async Task<Contract> LoadContractAsync(Guid id, CancellationToken cancellationToken)
    {
        var contract = await _db.Contracts
            .Include(c => c.Client)
            .Include(c => c.Account)
            .ThenInclude(a => a.Numbers)
            .Include(c => c.ProductVersion)
            .ThenInclude(v => v.Product)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (contract == null)
        {
            throw new NotFoundException("Договор не найден.");
        }

        return contract;
    }
}
