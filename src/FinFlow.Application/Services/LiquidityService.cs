using FinFlow.Application.Abstractions;
using FinFlow.Application.Common;
using FinFlow.Contracts;
using FinFlow.Domain.Calculators;
using FinFlow.Domain.Common;
using FinFlow.Domain.Entities;
using FinFlow.Domain.Enums;
using FinFlow.Domain.Reference;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Application.Services;

/// <summary>
/// Прогноз оттока, запаса и разрыва ликвидности по формулам (1.11)–(1.13).
/// Поддерживаются два сценария: «db» — по данным системы, «model» — модельный портфель
/// таблицы 1.5 пояснительной записки (используется для проверки расчёта).
/// </summary>
public class LiquidityService
{
    private readonly IBankDbContext _db;
    private readonly IClock _clock;
    private readonly ParameterService _parameters;
    private readonly InterestService _interest;

    public LiquidityService(IBankDbContext db, IClock clock, ParameterService parameters, InterestService interest)
    {
        _db = db;
        _clock = clock;
        _parameters = parameters;
        _interest = interest;
    }

    public async Task<LiquiditySettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var today = _clock.Today;

        var revocable = await _db.Contracts
            .Include(c => c.ProductVersion)
            .Where(c => c.Status == ContractStatus.Active || c.Status == ContractStatus.Prolonged)
            .ToListAsync(cancellationToken);

        return new LiquiditySettingsDto
        {
            OpeningCash = await _parameters.GetValueAsync(ParameterService.OpeningCash, today, 20_000_000m, cancellationToken),
            MinimumCash = await _parameters.GetValueAsync(ParameterService.MinimumCash, today, 10_000_000m, cancellationToken),
            RevocablePortfolio = Money.Round(revocable.Where(c => c.ProductVersion?.IsRevocable == true).Sum(c => c.Principal)),
            ProlongationShare = await _parameters.GetValueAsync(ParameterService.ProlongationShare, today, 0.40m, cancellationToken),
            EarlyWithdrawalShare = await _parameters.GetValueAsync(ParameterService.EarlyWithdrawalShare, today, 0.002m, cancellationToken),
            ExpectedDailyInflow = await _parameters.GetValueAsync(ParameterService.ExpectedDailyInflow, today, 2_000_000m, cancellationToken),
            Unit = "р."
        };
    }

    public async Task<LiquiditySettingsDto> UpdateSettingsAsync(LiquiditySettingsDto request, CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new AppException("Не переданы допущения прогноза.");
        }

        if (request.ProlongationShare is < 0m or > 1m)
        {
            throw new AppException("Доля пролонгации должна находиться в диапазоне от 0 до 1.");
        }

        if (request.EarlyWithdrawalShare is < 0m or > 1m)
        {
            throw new AppException("Дневная доля досрочного изъятия должна находиться в диапазоне от 0 до 1.");
        }

        var today = _clock.Today;
        await _parameters.SetAsync(new SetParameterRequest
        {
            Code = ParameterService.ProlongationShare, Name = "Доля пролонгации вкладов", Unit = "доля",
            Value = request.ProlongationShare, ValidFrom = today
        }, cancellationToken);

        await _parameters.SetAsync(new SetParameterRequest
        {
            Code = ParameterService.EarlyWithdrawalShare, Name = "Дневная доля досрочного изъятия", Unit = "доля",
            Value = request.EarlyWithdrawalShare, ValidFrom = today
        }, cancellationToken);

        await _parameters.SetAsync(new SetParameterRequest
        {
            Code = ParameterService.MinimumCash, Name = "Минимальный запас ликвидности (КУАП)", Unit = "р.",
            Value = request.MinimumCash, ValidFrom = today
        }, cancellationToken);

        await _parameters.SetAsync(new SetParameterRequest
        {
            Code = ParameterService.OpeningCash, Name = "Запас ликвидности на начало прогноза", Unit = "р.",
            Value = request.OpeningCash, ValidFrom = today
        }, cancellationToken);

        await _parameters.SetAsync(new SetParameterRequest
        {
            Code = ParameterService.ExpectedDailyInflow, Name = "Ожидаемые поступления дня", Unit = "р.",
            Value = request.ExpectedDailyInflow, ValidFrom = today
        }, cancellationToken);

        return await GetSettingsAsync(cancellationToken);
    }

    public async Task<LiquidityForecastDto> ForecastAsync(int days, string scenario, CancellationToken cancellationToken = default)
    {
        if (string.Equals(scenario, "model", StringComparison.OrdinalIgnoreCase))
        {
            return ForecastModel();
        }

        return await ForecastDbAsync(days, cancellationToken);
    }

    private LiquidityForecastDto ForecastModel()
    {
        var settings = new LiquiditySettingsDto
        {
            OpeningCash = ModelBankData.OpeningCash,
            MinimumCash = ModelBankData.MinimumCash,
            RevocablePortfolio = ModelBankData.RevocablePortfolio,
            ProlongationShare = ModelBankData.ProlongationShare,
            EarlyWithdrawalShare = ModelBankData.EarlyWithdrawalShare,
            ExpectedDailyInflow = 0m,
            Unit = "тыс. р."
        };

        var forecast = LiquidityCalculator.Forecast(ModelBankData.LiquidityDemo, ModelBankData.LiquidityDemoDays());
        return Map(forecast, settings, "model");
    }

    private async Task<LiquidityForecastDto> ForecastDbAsync(int days, CancellationToken cancellationToken)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        var horizon = days is < 1 or > 365 ? 90 : days;
        var today = _clock.Today;
        var last = today.AddDays(horizon - 1);

        var contracts = await _db.Contracts
            .Include(c => c.ProductVersion)
            .Where(c => (c.Status == ContractStatus.Active || c.Status == ContractStatus.Prolonged)
                     && c.EndDate >= today && c.EndDate <= last)
            .ToListAsync(cancellationToken);

        var maturing = contracts
            .GroupBy(c => c.EndDate)
            .ToDictionary(g => g.Key, g => g.Sum(c => c.Principal));

        var dailyInterest = await _interest.EstimateDailyInterestAsync(today, cancellationToken);

        var inputs = new List<LiquidityDayInput>();
        for (var i = 0; i < horizon; i++)
        {
            var date = today.AddDays(i);
            inputs.Add(new LiquidityDayInput
            {
                Date = date,
                MaturingPrincipal = maturing.TryGetValue(date, out var amount) ? amount : 0m,
                Inflow = settings.ExpectedDailyInflow,
                InterestPayable = dailyInterest
            });
        }

        var assumptions = new LiquidityAssumptions
        {
            OpeningCash = settings.OpeningCash,
            MinimumCash = settings.MinimumCash,
            RevocablePortfolio = settings.RevocablePortfolio,
            ProlongationShare = settings.ProlongationShare,
            EarlyWithdrawalShare = settings.EarlyWithdrawalShare
        };

        var forecast = LiquidityCalculator.Forecast(assumptions, inputs);
        await PersistAsync(forecast, "db", cancellationToken);
        return Map(forecast, settings, "db");
    }

    private async Task PersistAsync(LiquidityForecastResult forecast, string scenario, CancellationToken cancellationToken)
    {
        var calcId = Guid.NewGuid();
        var calculatedAt = _clock.UtcNow;

        foreach (var day in forecast.Days)
        {
            _db.LiquidityForecasts.Add(new LiquidityForecast
            {
                CalcId = calcId,
                CalcDate = calculatedAt,
                Bucket = day.Date,
                Inflow = day.Inflow,
                Outflow = day.Outflow,
                Cash = day.Cash,
                MinimumCash = forecast.MinimumCash,
                Gap = day.Gap,
                Scenario = scenario
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private LiquidityForecastDto Map(LiquidityForecastResult forecast, LiquiditySettingsDto settings, string scenario)
    {
        return new LiquidityForecastDto
        {
            Scenario = scenario,
            Settings = settings,
            Days = forecast.Days.Select(d => new LiquidityDayDto
            {
                Date = d.Date,
                MaturingPrincipal = d.MaturingPrincipal,
                ProlongedPrincipal = d.ProlongedPrincipal,
                EarlyWithdrawal = d.EarlyWithdrawal,
                InterestPayable = d.InterestPayable,
                Inflow = d.Inflow,
                Outflow = d.Outflow,
                Cash = d.Cash,
                Gap = d.Gap
            }).ToList(),
            MaxGap = forecast.MaxGap,
            MinCash = forecast.MinCash,
            GapDays = forecast.GapDays,
            FirstGapDate = forecast.FirstGapDate,
            CalculatedAt = _clock.UtcNow
        };
    }
}
