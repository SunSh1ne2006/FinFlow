using FinFlow.Domain.Common;

namespace FinFlow.Domain.Calculators;

/// <summary>Сегмент портфеля привлечённых средств: объём и ставка.</summary>
public sealed class PortfolioSegment
{
    public string Name { get; init; }
    public decimal Volume { get; init; }
    public decimal RatePercent { get; init; }
    public decimal InterestExpense => Money.Round(Volume * RatePercent / 100m);
}

public sealed class MarginReport
{
    public decimal TotalVolume { get; init; }
    /// <summary>Средневзвешенная стоимость ресурсов r̄, % (формула 1.14).</summary>
    public decimal WeightedRate { get; init; }
    public decimal AnnualInterestExpense { get; init; }
    public decimal TransferRate { get; init; }
    public decimal CostPercent { get; init; }
    /// <summary>Годовая маржа M (формула 1.15).</summary>
    public decimal Margin { get; init; }
    /// <summary>Резерв на риски Rрез (формула 1.20).</summary>
    public decimal RiskReserve { get; init; }
    /// <summary>Маржа с учётом резерва П (формула 1.16).</summary>
    public decimal NetMargin { get; init; }
    public IReadOnlyList<PortfolioSegment> Segments { get; init; }
}

/// <summary>
/// Стоимость ресурсов и процентная маржа (п. 1.3.5):
/// r̄ = Σ(S_i × r_i) / ΣS_i (1.14); M = V × (r_тр − r̄ − c) / 100 (1.15);
/// П = M − Rрез (1.16).
/// </summary>
public static class MarginCalculator
{
    public static decimal WeightedRate(IEnumerable<PortfolioSegment> segments)
    {
        var list = segments.ToList();
        var volume = list.Sum(s => s.Volume);
        if (volume <= 0m)
        {
            return 0m;
        }

        var weighted = list.Sum(s => s.Volume * s.RatePercent);
        return Money.Percent(weighted / volume);
    }

    public static MarginReport Report(
        IEnumerable<PortfolioSegment> segments,
        decimal transferRatePercent,
        decimal costPercent,
        decimal riskReserve)
    {
        var list = segments.ToList();
        var volume = list.Sum(s => s.Volume);
        var weightedRate = WeightedRate(list);
        var expense = Money.Round(list.Sum(s => s.InterestExpense));
        var margin = Money.Round(volume * (transferRatePercent - weightedRate - costPercent) / 100m);
        var netMargin = Money.Round(margin - riskReserve);

        return new MarginReport
        {
            TotalVolume = Money.Round(volume),
            WeightedRate = weightedRate,
            AnnualInterestExpense = expense,
            TransferRate = transferRatePercent,
            CostPercent = costPercent,
            Margin = margin,
            RiskReserve = Money.Round(riskReserve),
            NetMargin = netMargin,
            Segments = list
        };
    }
}
