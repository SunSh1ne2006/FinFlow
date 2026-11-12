using FinFlow.Domain.Calculators;
using FinFlow.Domain.Reference;
using Xunit;

namespace FinFlow.Tests;

/// <summary>Стоимость ресурсов и маржа: формулы (1.14)–(1.16).</summary>
public class MarginTests
{
    /// <summary>(1.14) Средневзвешенная ставка r̄ = 1 854 000 / 240 000 = 7,725 %.</summary>
    [Fact]
    public void Weighted_rate_matches_formula_1_14()
    {
        var rate = MarginCalculator.WeightedRate(ModelBankData.PortfolioSegments);

        Assert.Equal(7.725m, rate);
    }

    /// <summary>(1.15) M = 240 000 × (9,05 − 7,725 − 0,40) / 100 = 2 220 тыс. р.</summary>
    [Fact]
    public void Margin_matches_formula_1_15()
    {
        var report = MarginCalculator.Report(
            ModelBankData.PortfolioSegments,
            ModelBankData.TransferRatePercent,
            ModelBankData.OperatingCostPercent,
            riskReserve: 0m);

        Assert.Equal(240000m, report.TotalVolume);
        Assert.Equal(7.725m, report.WeightedRate);
        Assert.Equal(2220m, report.Margin);
        Assert.Equal(2220m, report.NetMargin);
    }

    /// <summary>(1.16) П = 2 220 − 1 979 = 241 тыс. р.</summary>
    [Fact]
    public void Net_margin_subtracts_risk_reserve()
    {
        var report = MarginCalculator.Report(
            ModelBankData.PortfolioSegments,
            ModelBankData.TransferRatePercent,
            ModelBankData.OperatingCostPercent,
            riskReserve: 1979m);

        Assert.Equal(2220m, report.Margin);
        Assert.Equal(1979m, report.RiskReserve);
        Assert.Equal(241m, report.NetMargin);
    }
}
