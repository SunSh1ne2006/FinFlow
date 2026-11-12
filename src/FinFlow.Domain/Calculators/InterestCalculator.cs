using FinFlow.Domain.Common;

namespace FinFlow.Domain.Calculators;

/// <summary>Остаток и ставка за один день периода (формула 1.5).</summary>
public sealed class DailyRate
{
    public decimal Balance { get; init; }
    public decimal RatePercent { get; init; }
}

/// <summary>
/// Начисление процентов по фактическому числу дней и ставке каждого дня (п. 1.3.2).
/// Формула (1.5): I = Σ Sd × rd / (100 × Dy), округление до 0,01 выполняется один раз по итогу периода.
/// </summary>
public static class InterestCalculator
{
    public static decimal Accrue(IEnumerable<DailyRate> days, int yearDays)
    {
        if (yearDays <= 0)
        {
            throw new DomainException("База начисления (число дней в году) должна быть положительной.");
        }

        var total = 0m;
        foreach (var day in days)
        {
            total += day.Balance * day.RatePercent / (100m * yearDays);
        }

        return Money.Round(total);
    }

    /// <summary>Проценты за период с неизменным остатком (пример п. 1.3.2: 97,67 р.).</summary>
    public static decimal AccrueFlat(decimal balance, decimal ratePercent, int days, int yearDays)
    {
        if (days < 0)
        {
            throw new DomainException("Число дней начисления не может быть отрицательным.");
        }

        if (yearDays <= 0)
        {
            throw new DomainException("База начисления (число дней в году) должна быть положительной.");
        }

        return Money.Round(balance * ratePercent * days / (100m * yearDays));
    }

    /// <summary>Сумма вклада после n периодов капитализации по формуле (1.6).</summary>
    public static decimal CompoundAmount(decimal principal, decimal ratePercent, int periodsPerYear, int periods)
    {
        if (periodsPerYear <= 0)
        {
            throw new DomainException("Число периодов капитализации в году должно быть положительным.");
        }

        var factor = Pow(1m + ratePercent / 100m / periodsPerYear, periods);
        return Money.Round(principal * factor);
    }

    /// <summary>Эффективная годовая ставка по формуле (1.7), %.</summary>
    public static decimal EffectiveRate(decimal ratePercent, int periodsPerYear)
    {
        if (periodsPerYear <= 0)
        {
            throw new DomainException("Число периодов капитализации в году должно быть положительным.");
        }

        var factor = Pow(1m + ratePercent / 100m / periodsPerYear, periodsPerYear);
        return Money.Percent2((factor - 1m) * 100m);
    }

    /// <summary>Ставка дня для плавающей ставки: база (ставка рефинансирования) + надбавка.</summary>
    public static decimal FloatingRate(decimal baseRatePercent, decimal spreadPercent) =>
        Money.Percent(baseRatePercent + spreadPercent);

    /// <summary>Число дней в году для даты: 366 для високосного года, иначе 365.</summary>
    public static int YearDays(DateOnly date) => DateTime.IsLeapYear(date.Year) ? 366 : 365;

    private static decimal Pow(decimal value, int power)
    {
        if (power < 0)
        {
            throw new DomainException("Показатель степени не может быть отрицательным.");
        }

        var result = 1m;
        for (var i = 0; i < power; i++)
        {
            result *= value;
        }

        return result;
    }
}
