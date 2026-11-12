using FinFlow.Domain.Common;

namespace FinFlow.Domain.Calculators;

public sealed class EarlyReturnQuote
{
    public decimal Principal { get; init; }
    public int Days { get; init; }
    public int YearDays { get; init; }
    public decimal ContractRatePercent { get; init; }
    public decimal DemandRatePercent { get; init; }
    public decimal ContractInterest { get; init; }
    public decimal DemandInterest { get; init; }
    public decimal PaidInterest { get; init; }
    /// <summary>Излишне выплаченные проценты, удерживаемые из вклада (U).</summary>
    public decimal ExcessToWithhold { get; init; }
    public decimal NetPayout { get; init; }
    public bool RequiresBankConsent { get; init; }
    public bool ConsentGranted { get; init; }
    public bool Allowed { get; init; }
    public string Reason { get; init; }
}

/// <summary>
/// Досрочный возврат вклада (п. 1.3.3): перерасчёт процентов по ставке до востребования
/// по формулам (1.9) и (1.10). Безотзывный вклад возвращается только с согласия банка.
/// </summary>
public static class EarlyReturnCalculator
{
    public static EarlyReturnQuote Calculate(
        decimal principal,
        decimal contractRatePercent,
        int days,
        int yearDays,
        decimal demandRatePercent,
        decimal paidInterest,
        bool requiresBankConsent,
        bool consentGranted)
    {
        if (days < 0)
        {
            throw new DomainException("Фактический срок нахождения средств во вкладе не может быть отрицательным.");
        }

        var contractInterest = InterestCalculator.AccrueFlat(principal, contractRatePercent, days, yearDays);
        var demandInterest = InterestCalculator.AccrueFlat(principal, demandRatePercent, days, yearDays);
        var excess = paidInterest - demandInterest;
        if (excess < 0m)
        {
            excess = 0m;
        }

        excess = Money.Round(excess);
        var netPayout = principal + demandInterest - paidInterest;
        if (netPayout < 0m)
        {
            netPayout = 0m;
        }

        var allowed = !requiresBankConsent || consentGranted;

        return new EarlyReturnQuote
        {
            Principal = principal,
            Days = days,
            YearDays = yearDays,
            ContractRatePercent = contractRatePercent,
            DemandRatePercent = demandRatePercent,
            ContractInterest = contractInterest,
            DemandInterest = demandInterest,
            PaidInterest = Money.Round(paidInterest),
            ExcessToWithhold = excess,
            NetPayout = Money.Round(netPayout),
            RequiresBankConsent = requiresBankConsent,
            ConsentGranted = consentGranted,
            Allowed = allowed,
            Reason = allowed
                ? null
                : "Безотзывный вклад возвращается досрочно только с согласия банка (Декрет № 7, бизнес-правило 8)."
        };
    }
}
