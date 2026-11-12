using FinFlow.Domain.Common;
using FinFlow.Domain.Enums;

namespace FinFlow.Domain.Calculators;

/// <summary>Исходные данные оценки риска: вероятность в долях и денежные последствия.</summary>
public sealed class RiskInput
{
    public string Code { get; init; }
    public decimal Probability { get; init; }
    /// <summary>Денежная оценка последствий C, тыс. р.</summary>
    public decimal MoneyImpact { get; init; }
    public decimal? MitigatedProbability { get; init; }
}

public sealed class RiskScore
{
    public string Code { get; init; }
    public decimal Probability { get; init; }
    public decimal MoneyImpact { get; init; }
    public int ProbabilityScore { get; init; }
    public int ImpactScore { get; init; }
    /// <summary>Интегральная оценка R = pб × Q (формула 1.17).</summary>
    public int Score { get; init; }
    public RiskZone Zone { get; init; }
    public decimal Emv { get; init; }
    public decimal? MitigatedProbability { get; init; }
    public decimal? ResidualEmv { get; init; }
}

public sealed class RiskSummary
{
    /// <summary>ΣEMV (формула 1.18).</summary>
    public decimal ExpectedMonetaryValue { get; init; }
    /// <summary>Стандартное отклонение суммарных потерь σ (формула 1.19).</summary>
    public decimal StandardDeviation { get; init; }
    /// <summary>Резерв на риски Rрез = EMV + z × σ (формула 1.20).</summary>
    public decimal Reserve { get; init; }
    public decimal Z { get; init; }
    public decimal ResidualEmv { get; init; }
    public decimal MitigationEffect { get; init; }
    public IReadOnlyList<RiskScore> Risks { get; init; }
}

/// <summary>
/// Количественная оценка рисков по СТБ ISO 31000-2020 (п. 1.9):
/// перевод вероятности и последствий в баллы, R = pб × Q (1.17),
/// EMV (1.18), σ (1.19) при допущении о независимости рисков, резерв P80 (1.20).
/// </summary>
public static class RiskCalculator
{
    public const decimal P80Quantile = 0.84m;

    public static int ProbabilityScore(decimal probability)
    {
        if (probability < 0m || probability > 1m)
        {
            throw new DomainException("Вероятность риска должна находиться в диапазоне от 0 до 1.");
        }

        if (probability <= 0.10m) return 1;
        if (probability <= 0.24m) return 2;
        if (probability <= 0.50m) return 3;
        if (probability <= 0.75m) return 4;
        return 5;
    }

    /// <summary>Балл влияния по денежным последствиям C, тыс. р.</summary>
    public static int ImpactScore(decimal moneyImpact)
    {
        if (moneyImpact < 0m)
        {
            throw new DomainException("Денежная оценка последствий не может быть отрицательной.");
        }

        if (moneyImpact <= 150m) return 1;
        if (moneyImpact <= 250m) return 2;
        if (moneyImpact <= 400m) return 3;
        if (moneyImpact <= 600m) return 4;
        return 5;
    }

    public static int Score(int probabilityScore, int impactScore) => probabilityScore * impactScore;

    public static RiskZone Zone(int score)
    {
        if (score <= 4) return RiskZone.Low;
        if (score <= 9) return RiskZone.Moderate;
        if (score <= 16) return RiskZone.High;
        return RiskZone.Critical;
    }

    public static RiskScore Evaluate(RiskInput input)
    {
        var probabilityScore = ProbabilityScore(input.Probability);
        var impactScore = ImpactScore(input.MoneyImpact);
        var score = Score(probabilityScore, impactScore);
        var emv = Money.Round(input.Probability * input.MoneyImpact);
        decimal? residual = null;
        if (input.MitigatedProbability.HasValue)
        {
            residual = Money.Round(input.MitigatedProbability.Value * input.MoneyImpact);
        }

        return new RiskScore
        {
            Code = input.Code,
            Probability = input.Probability,
            MoneyImpact = input.MoneyImpact,
            ProbabilityScore = probabilityScore,
            ImpactScore = impactScore,
            Score = score,
            Zone = Zone(score),
            Emv = emv,
            MitigatedProbability = input.MitigatedProbability,
            ResidualEmv = residual
        };
    }

    public static RiskSummary Summarize(IEnumerable<RiskInput> risks, decimal? quantile = null)
    {
        var list = risks.ToList();
        var scores = list.Select(Evaluate).ToList();
        var emv = Money.Round(scores.Sum(s => s.Emv));

        var variance = list.Sum(r => r.Probability * (1m - r.Probability) * r.MoneyImpact * r.MoneyImpact);
        var sigma = variance <= 0m ? 0m : (decimal)Math.Sqrt((double)variance);
        var z = quantile ?? P80Quantile;
        var reserve = Money.Round(emv + z * sigma);

        var residual = Money.Round(scores.Where(s => s.ResidualEmv.HasValue).Sum(s => s.ResidualEmv.Value));

        return new RiskSummary
        {
            ExpectedMonetaryValue = emv,
            StandardDeviation = decimal.Round(sigma, 1, MidpointRounding.AwayFromZero),
            Reserve = reserve,
            Z = z,
            ResidualEmv = residual,
            MitigationEffect = Money.Round(emv - residual),
            Risks = scores
        };
    }
}
