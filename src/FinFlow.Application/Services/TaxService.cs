using FinFlow.Application.Abstractions;
using FinFlow.Contracts;
using FinFlow.Domain.Calculators;
using FinFlow.Domain.Common;
using FinFlow.Domain.Entities;
using FinFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Application.Services;

/// <summary>
/// Подоходный налог с процентов по формуле (1.8): банк является налоговым агентом,
/// налог удерживается по каждой сумме процентов с превышения над ставкой до востребования
/// при отсутствии условий освобождения (п. 1.3.3).
/// </summary>
public class TaxService
{
    private readonly IBankDbContext _db;
    private readonly IClock _clock;
    private readonly ParameterService _parameters;

    public TaxService(IBankDbContext db, IClock clock, ParameterService parameters)
    {
        _db = db;
        _clock = clock;
        _parameters = parameters;
    }

    public async Task<TaxCalculation> ComputeAsync(Contract contract, InterestAccrual accrual, CancellationToken cancellationToken = default)
    {
        var taxRate = await _parameters.GetValueAsync(ParameterService.TaxRate, accrual.AccrualDate, 13m, cancellationToken);
        var demandRate = await _parameters.GetValueAsync(ParameterService.DemandRate, accrual.AccrualDate, 0.1m, cancellationToken);
        var yearDays = accrual.YearDays > 0 ? accrual.YearDays : InterestCalculator.YearDays(accrual.AccrualDate);
        var demandInterest = InterestCalculator.AccrueFlat(accrual.Base, demandRate, 1, yearDays);
        var termDays = Math.Max(0, accrual.AccrualDate.DayNumber - contract.OpenDate.DayNumber);
        var currency = contract.Account?.Currency ?? "BYN";
        var isIndividual = contract.Client?.Type != ClientType.LegalEntity;

        return TaxCalculator.Calculate(accrual.Amount, demandInterest, currency, termDays, taxRate, isIndividual);
    }

    /// <summary>Создание удержания налога по начислению. Возвращает null, если налог не удерживается.</summary>
    public async Task<TaxWithholding> WithholdAsync(Contract contract, InterestAccrual accrual, CancellationToken cancellationToken = default)
    {
        var existing = await _db.TaxWithholdings
            .FirstOrDefaultAsync(t => t.AccrualId == accrual.Id, cancellationToken);

        if (existing != null)
        {
            return existing;
        }

        var calculation = await ComputeAsync(contract, accrual, cancellationToken);
        if (calculation.Amount <= 0m)
        {
            return null;
        }

        var withholding = new TaxWithholding
        {
            AccrualId = accrual.Id,
            ContractId = contract.Id,
            TaxBase = calculation.TaxableBase,
            RatePercent = calculation.RatePercent,
            Amount = calculation.Amount,
            WithheldAt = _clock.UtcNow,
            IsExempt = calculation.IsExempt,
            ExemptionReason = calculation.Reason
        };

        _db.TaxWithholdings.Add(withholding);
        return withholding;
    }

    public async Task<TaxReportDto> ReportAsync(DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
    {
        var rows = await (
            from withholding in _db.TaxWithholdings
            join accrual in _db.InterestAccruals on withholding.AccrualId equals accrual.Id
            join contract in _db.Contracts on withholding.ContractId equals contract.Id
            join client in _db.Clients on contract.ClientId equals client.Id
            join account in _db.Accounts on contract.AccountId equals account.Id
            where accrual.AccrualDate >= fromDate && accrual.AccrualDate <= toDate
            orderby accrual.AccrualDate descending
            select new
            {
                ContractNumber = contract.Number,
                ClientName = client.Name,
                Currency = account.Currency,
                Accrued = accrual.Amount,
                TaxableBase = withholding.TaxBase,
                Tax = withholding.Amount,
                IsExempt = withholding.IsExempt,
                Reason = withholding.ExemptionReason
            }).ToListAsync(cancellationToken);

        return new TaxReportDto
        {
            From = fromDate,
            To = toDate,
            TotalAccrued = Money.Round(rows.Sum(r => r.Accrued)),
            TotalTaxable = Money.Round(rows.Sum(r => r.TaxableBase)),
            TotalTax = Money.Round(rows.Sum(r => r.Tax)),
            Rows = rows.Select(r => new TaxReportRowDto
            {
                ContractNumber = r.ContractNumber,
                ClientName = r.ClientName,
                Currency = r.Currency,
                Accrued = r.Accrued,
                TaxableBase = r.TaxableBase,
                Tax = r.Tax,
                IsExempt = r.IsExempt,
                ExemptionReason = r.Reason
            }).ToList()
        };
    }
}
