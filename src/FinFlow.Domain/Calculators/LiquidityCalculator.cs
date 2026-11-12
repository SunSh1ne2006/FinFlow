using FinFlow.Domain.Common;

namespace FinFlow.Domain.Calculators;

/// <summary>Исходные данные прогноза ликвидности (допущения КУАП).</summary>
public sealed class LiquidityAssumptions
{
    public decimal OpeningCash { get; init; }
    public decimal MinimumCash { get; init; }
    /// <summary>Портфель отзывных вкладов Sотз.</summary>
    public decimal RevocablePortfolio { get; init; }
    /// <summary>Доля пролонгации ρ (0–1).</summary>
    public decimal ProlongationShare { get; init; }
    /// <summary>Дневная доля досрочного изъятия отзывных вкладов λ.</summary>
    public decimal EarlyWithdrawalShare { get; init; }
}

public sealed class LiquidityDayInput
{
    public DateOnly Date { get; init; }
    /// <summary>Сумма вкладов, срок возврата которых наступает в этот день Mt.</summary>
    public decimal MaturingPrincipal { get; init; }
    public decimal Inflow { get; init; }
    public decimal InterestPayable { get; init; }
}

public sealed class LiquidityDayResult
{
    public DateOnly Date { get; init; }
    public decimal MaturingPrincipal { get; init; }
    public decimal ProlongedPrincipal { get; init; }
    public decimal EarlyWithdrawal { get; init; }
    public decimal InterestPayable { get; init; }
    public decimal Inflow { get; init; }
    public decimal Outflow { get; init; }
    public decimal Cash { get; init; }
    public decimal Gap { get; init; }
}

public sealed class LiquidityForecastResult
{
    public decimal OpeningCash { get; init; }
    public decimal MinimumCash { get; init; }
    public IReadOnlyList<LiquidityDayResult> Days { get; init; }
    public decimal MaxGap => Days.Count == 0 ? 0m : Days.Max(d => d.Gap);
    public decimal MinCash => Days.Count == 0 ? OpeningCash : Days.Min(d => d.Cash);
    public int GapDays => Days.Count(d => d.Gap > 0m);
    public DateOnly? FirstGapDate => Days.Where(d => d.Gap > 0m).Select(d => (DateOnly?)d.Date).FirstOrDefault();
}

/// <summary>
/// Прогноз оттока, запаса и разрыва ликвидности (п. 1.3.4):
/// формула (1.11) Out_t = (1−ρ) × ΣS_i + λ × Sотз + I_t;
/// формула (1.12) Cash_t = Cash_(t−1) + In_t − Out_t;
/// формула (1.13) Gap_t = max(0; Cash_min − Cash_t).
/// Единица измерения задаётся вызывающим кодом (в записке — тыс. р.).
/// </summary>
public static class LiquidityCalculator
{
    public static LiquidityForecastResult Forecast(LiquidityAssumptions assumptions, IEnumerable<LiquidityDayInput> days)
    {
        if (assumptions.ProlongationShare < 0m || assumptions.ProlongationShare > 1m)
        {
            throw new DomainException("Доля пролонгации должна находиться в диапазоне от 0 до 1.");
        }

        if (assumptions.EarlyWithdrawalShare < 0m)
        {
            throw new DomainException("Дневная доля досрочного изъятия не может быть отрицательной.");
        }

        var results = new List<LiquidityDayResult>();
        var cash = assumptions.OpeningCash;

        foreach (var day in days.OrderBy(d => d.Date))
        {
            var prolonged = Money.Round(day.MaturingPrincipal * assumptions.ProlongationShare);
            var maturityOutflow = day.MaturingPrincipal - prolonged;
            var earlyWithdrawal = Money.Round(assumptions.RevocablePortfolio * assumptions.EarlyWithdrawalShare);
            var outflow = Money.Round(maturityOutflow + earlyWithdrawal + day.InterestPayable);
            cash = Money.Round(cash + day.Inflow - outflow);
            var gap = assumptions.MinimumCash - cash;
            if (gap < 0m)
            {
                gap = 0m;
            }

            results.Add(new LiquidityDayResult
            {
                Date = day.Date,
                MaturingPrincipal = Money.Round(day.MaturingPrincipal),
                ProlongedPrincipal = prolonged,
                EarlyWithdrawal = earlyWithdrawal,
                InterestPayable = Money.Round(day.InterestPayable),
                Inflow = Money.Round(day.Inflow),
                Outflow = outflow,
                Cash = cash,
                Gap = Money.Round(gap)
            });
        }

        return new LiquidityForecastResult
        {
            OpeningCash = Money.Round(assumptions.OpeningCash),
            MinimumCash = Money.Round(assumptions.MinimumCash),
            Days = results
        };
    }
}
