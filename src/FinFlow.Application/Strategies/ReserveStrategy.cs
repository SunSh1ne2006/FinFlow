using FinFlow.Domain.Calculators;
using FinFlow.Domain.Common;

namespace FinFlow.Application.Strategies;

/// <summary>
/// Стратегия расчёта резерва под риски (требование 14, п. 1.9).
/// Позволяет заменить нормальное приближение квантиля другой методикой,
/// не изменяя сервис оценки рисков.
/// </summary>
public interface IReserveStrategy
{
    string Code { get; }
    string Name { get; }
    decimal Reserve(decimal expectedMonetaryValue, decimal standardDeviation);
}

/// <summary>
/// Резерв по формуле (1.20): Rрез = EMV + z × σ, квантиль P80 (z = 0,84).
/// </summary>
public class P80ReserveStrategy : IReserveStrategy
{
    public string Code => "P80";

    public string Name => "Квантиль P80 (нормальное приближение)";

    public decimal Reserve(decimal expectedMonetaryValue, decimal standardDeviation) =>
        Money.Round(expectedMonetaryValue + RiskCalculator.P80Quantile * standardDeviation);
}
