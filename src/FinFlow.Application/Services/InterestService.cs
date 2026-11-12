using FinFlow.Application.Abstractions;
using FinFlow.Domain.Common;
using FinFlow.Domain.Calculators;
using FinFlow.Domain.Entities;
using FinFlow.Domain.Enums;
using FinFlow.Domain.Reference;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Application.Services;

/// <summary>
/// Начисление процентов по формуле (1.5): фактическое число дней, ставка каждого дня,
/// округление один раз по итогу начисления. Идемпотентность обеспечивается парой
/// (ContractId, AccrualDate) — повторный запуск закрытия дня не создаёт дубль (требование 12).
/// </summary>
public class InterestService
{
    private readonly IBankDbContext _db;
    private readonly IClock _clock;
    private readonly ParameterService _parameters;

    public InterestService(IBankDbContext db, IClock clock, ParameterService parameters)
    {
        _db = db;
        _clock = clock;
        _parameters = parameters;
    }

    /// <summary>Ставка дня: фиксированная из версии условий или плавающая (рефинансирование + надбавка).</summary>
    public async Task<decimal> ResolveRateAsync(Contract contract, DateOnly date, CancellationToken cancellationToken = default)
    {
        var version = contract.ProductVersion;
        if (version == null)
        {
            return 0m;
        }

        if (version.RateType == RateType.Floating)
        {
            var baseRate = await _parameters.GetValueAsync(ParameterService.RefinancingRate, date,
                ModelBankData.RefinancingRatePercent, cancellationToken);
            return InterestCalculator.FloatingRate(baseRate, version.SpreadPercent);
        }

        return version.RatePercent;
    }

    /// <summary>База начисления: тело вклада или остаток текущего счёта на конец дня.</summary>
    public static decimal AccrualBase(Contract contract)
    {
        var isCurrentAccount = contract.ProductVersion?.Product?.Kind == ProductKind.CurrentAccount;
        if (isCurrentAccount)
        {
            return contract.Account?.Balance ?? 0m;
        }

        return contract.Principal;
    }

    public async Task<decimal> ComputeDailyAmountAsync(Contract contract, DateOnly date, CancellationToken cancellationToken = default)
    {
        var rate = await ResolveRateAsync(contract, date, cancellationToken);
        var yearDays = contract.ProductVersion?.ResolveDayBase(date) ?? InterestCalculator.YearDays(date);
        return InterestCalculator.AccrueFlat(AccrualBase(contract), rate, 1, yearDays);
    }

    /// <summary>Начисление за день. Если начисление уже существует — возвращается оно (идемпотентность).</summary>
    public async Task<InterestAccrual> AccrueDayAsync(Contract contract, DateOnly date, Guid? dayId, CancellationToken cancellationToken = default)
    {
        var existing = await _db.InterestAccruals
            .FirstOrDefaultAsync(a => a.ContractId == contract.Id && a.AccrualDate == date, cancellationToken);

        if (existing != null)
        {
            return existing;
        }

        var rate = await ResolveRateAsync(contract, date, cancellationToken);
        var baseAmount = AccrualBase(contract);
        var yearDays = contract.ProductVersion?.ResolveDayBase(date) ?? InterestCalculator.YearDays(date);
        var amount = InterestCalculator.AccrueFlat(baseAmount, rate, 1, yearDays);

        var accrual = new InterestAccrual
        {
            ContractId = contract.Id,
            AccrualDate = date,
            Base = baseAmount,
            RatePercent = rate,
            YearDays = yearDays,
            Amount = amount,
            Status = AccrualStatus.Accrued,
            OperationalDayId = dayId
        };

        _db.InterestAccruals.Add(accrual);
        contract.AccruedInterest += amount;
        return accrual;
    }

    /// <summary>Оценка процентов к выплате за день по портфелю — для прогноза ликвидности.</summary>
    public async Task<decimal> EstimateDailyInterestAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var accrued = await _db.InterestAccruals
            .Where(a => a.AccrualDate == date)
            .SumAsync(a => (decimal?)a.Amount, cancellationToken) ?? 0m;

        if (accrued > 0m)
        {
            return accrued;
        }

        var contracts = await _db.Contracts
            .Include(c => c.ProductVersion)
            .ThenInclude(v => v.Product)
            .Include(c => c.Account)
            .Where(c => c.Status == ContractStatus.Active || c.Status == ContractStatus.Prolonged)
            .ToListAsync(cancellationToken);

        var total = 0m;
        foreach (var contract in contracts)
        {
            var rate = await ResolveRateAsync(contract, date, cancellationToken);
            var yearDays = contract.ProductVersion?.ResolveDayBase(date) ?? InterestCalculator.YearDays(date);
            total += InterestCalculator.AccrueFlat(AccrualBase(contract), rate, 1, yearDays);
        }

        return Money.Round(total);
    }
}
