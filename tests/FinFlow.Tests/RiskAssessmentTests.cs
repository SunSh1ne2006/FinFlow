using FinFlow.Domain.Calculators;
using FinFlow.Domain.Enums;
using FinFlow.Domain.Reference;
using Xunit;

namespace FinFlow.Tests;

/// <summary>Оценка рисков: R = p×Q, EMV, σ и квантиль P80 (п. 1.3.6).</summary>
public class RiskAssessmentTests
{
    private static RiskSummary Summary() =>
        RiskCalculator.Summarize(ModelBankData.Risks.Select(r => new RiskInput
        {
            Code = r.Code,
            Probability = r.Probability,
            MoneyImpact = r.MoneyImpact,
            MitigatedProbability = r.MitigatedProbability
        }));

    /// <summary>ΣEMV = 1 330 тыс. р.</summary>
    [Fact]
    public void Expected_monetary_value_is_1330()
    {
        Assert.Equal(1330m, Summary().ExpectedMonetaryValue);
    }

    /// <summary>Среднеквадратическое отклонение σ = 772,6 тыс. р.</summary>
    [Fact]
    public void Standard_deviation_is_772_point_6()
    {
        Assert.Equal(772.6m, Summary().StandardDeviation);
    }

    /// <summary>Резерв P80: 1 330 + 0,84 × 772,63 = 1 979 тыс. р.</summary>
    [Fact]
    public void Reserve_is_1979()
    {
        var summary = Summary();

        Assert.Equal(0.84m, summary.Z);
        Assert.Equal(1979m, summary.Reserve);
    }

    /// <summary>Остаточный EMV после мероприятий = 690 тыс. р.</summary>
    [Fact]
    public void Residual_emv_after_mitigation_is_690()
    {
        Assert.Equal(690m, Summary().ResidualEmv);
    }

    /// <summary>Критические и высокие риски: балл R ≥ 10 — R-01, R-02, R-04, R-10.</summary>
    [Fact]
    public void High_risks_match_score_threshold()
    {
        var high = Summary().Risks
            .Where(r => r.Score >= 10)
            .Select(r => r.Code)
            .OrderBy(code => code)
            .ToList();

        Assert.Equal(new[] { "R-01", "R-02", "R-04", "R-10" }, high);
    }

    /// <summary>Матрица: R = 3 × 4 = 12 → зона «Высокий».</summary>
    [Fact]
    public void Risk_matrix_zone_is_high_for_score_12()
    {
        Assert.Equal(12, RiskCalculator.Score(3, 4));
        Assert.Equal(RiskZone.High, RiskCalculator.Zone(12));
        Assert.Equal("Высокий", RiskCalculator.Zone(12).DisplayName());
    }
}
