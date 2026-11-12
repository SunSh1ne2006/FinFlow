using FinFlow.Domain.Common;

namespace FinFlow.Domain.Calculators;

public sealed class TaxCalculation
{
    public decimal TaxableBase { get; init; }
    public decimal Amount { get; init; }
    public decimal RatePercent { get; init; }
    public bool IsExempt { get; init; }
    public string Reason { get; init; }
}

/// <summary>
/// Подоходный налог с процентов по вкладам (п. 1.3.3, формула 1.8).
/// Налог удерживается с процентов, начисленных по ставке выше ставки до востребования,
/// по вкладам в BYN сроком менее одного года и в иностранной валюте сроком менее двух лет.
/// </summary>
public static class TaxCalculator
{
    public const string Byn = "BYN";

    public static bool IsExempt(string currency, int termDays)
    {
        if (string.Equals(currency, Byn, StringComparison.OrdinalIgnoreCase))
        {
            return termDays >= 365;
        }

        return termDays >= 730;
    }

    public static TaxCalculation Calculate(
        decimal accruedInterest,
        decimal demandRateInterest,
        string currency,
        int termDays,
        decimal taxRatePercent,
        bool isIndividual = true)
    {
        if (!isIndividual)
        {
            return new TaxCalculation
            {
                TaxableBase = 0m,
                Amount = 0m,
                RatePercent = taxRatePercent,
                IsExempt = true,
                Reason = "Налогообложение процентов юридических лиц выполняется в ином порядке (вне границ разработки)."
            };
        }

        if (IsExempt(currency, termDays))
        {
            return new TaxCalculation
            {
                TaxableBase = 0m,
                Amount = 0m,
                RatePercent = taxRatePercent,
                IsExempt = true,
                Reason = string.Equals(currency, Byn, StringComparison.OrdinalIgnoreCase)
                    ? "Вклад в BYN сроком не менее одного года: проценты освобождены от налога."
                    : "Вклад в иностранной валюте сроком не менее двух лет: проценты освобождены от налога."
            };
        }

        var taxableBase = accruedInterest - demandRateInterest;
        if (taxableBase < 0m)
        {
            taxableBase = 0m;
        }

        taxableBase = Money.Round(taxableBase);
        var amount = Money.Round(taxableBase * taxRatePercent / 100m);

        return new TaxCalculation
        {
            TaxableBase = taxableBase,
            Amount = amount,
            RatePercent = taxRatePercent,
            IsExempt = amount == 0m,
            Reason = amount == 0m ? "Превышение над ставкой до востребования отсутствует." : null
        };
    }
}
