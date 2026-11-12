using FinFlow.Domain.Calculators;
using Xunit;

namespace FinFlow.Tests;

/// <summary>
/// Контрольные примеры пояснительной записки: формулы (1.1)–(1.10).
/// Значения взяты из п. 1.3.1–1.3.3 и должны воспроизводиться расчётами без расхождений.
/// </summary>
public class DomainCalculationsTests
{
    /// <summary>(1.1) D = max(0; B + L − H); при аресте L = 0.</summary>
    [Theory]
    [InlineData(12000, 5000, 3000, false, 14000)]
    [InlineData(-2000, 5000, 0, false, 3000)]
    [InlineData(3000, 5000, 4000, true, 0)]
    public void Available_balance_matches_formula_1_1(decimal balance, decimal limit, decimal holds, bool arrest, decimal expected)
    {
        var available = BalanceCalculator.Available(balance, limit, holds, arrest);
        Assert.Equal(expected, available);
    }

    /// <summary>(1.2) B_t = B_0 + ΣC_j − ΣW_k.</summary>
    [Fact]
    public void Turnover_matches_formula_1_2()
    {
        var balance = BalanceCalculator.BalanceAfter(10000m, new[] { 5000m, 2500m }, new[] { 1000m });

        Assert.Equal(16500m, balance);
    }

    /// <summary>(1.5) I = Σ S_d·r_d/(100·D_y): 10 000 р. под 11,5 % за 31 день = 97,67 р.</summary>
    [Fact]
    public void Interest_matches_formula_1_5()
    {
        var interest = InterestCalculator.AccrueFlat(10000m, 11.5m, 31, 365);

        Assert.Equal(97.67m, interest);
    }

    /// <summary>(1.5) Начисление по дням даёт тот же результат при неизменном остатке.</summary>
    [Fact]
    public void Daily_accrual_is_equivalent_to_flat_accrual()
    {
        var days = Enumerable.Range(0, 31).Select(_ => new DailyRate { Balance = 10000m, RatePercent = 11.5m });

        Assert.Equal(97.67m, InterestCalculator.Accrue(days, 365));
    }

    /// <summary>(1.6) Капитализация ежемесячно: 10 000 р. → 10 290,26 р. за 3 месяца.</summary>
    [Fact]
    public void Compound_amount_matches_formula_1_6()
    {
        var amount = InterestCalculator.CompoundAmount(10000m, 11.5m, 12, 3);

        Assert.Equal(10290.26m, amount);
    }

    /// <summary>(1.7) Эффективная ставка при ежемесячной капитализации 11,5 % = 12,13 %.</summary>
    [Fact]
    public void Effective_rate_matches_formula_1_7()
    {
        var effective = InterestCalculator.EffectiveRate(11.5m, 12);

        Assert.Equal(12.13m, effective);
    }

    /// <summary>(1.8) Налог 13 % от 97,67 р. = 12,70 р.</summary>
    [Fact]
    public void Tax_matches_formula_1_8()
    {
        var tax = TaxCalculator.Calculate(97.67m, 0m, "BYN", 90, 13m);

        Assert.Equal(97.67m, tax.TaxableBase);
        Assert.Equal(12.70m, tax.Amount);
        Assert.False(tax.IsExempt);
    }

    /// <summary>(1.8) Вклад в BYN на срок не менее года освобождён от налога.</summary>
    [Fact]
    public void Tax_is_exempt_for_annual_byn_deposit()
    {
        var tax = TaxCalculator.Calculate(97.67m, 0m, "BYN", 365, 13m);

        Assert.True(tax.IsExempt);
        Assert.Equal(0m, tax.Amount);
    }

    /// <summary>
    /// (1.9)–(1.10) Досрочный возврат 5 000 р. под 9 % через 120 дней:
    /// I_выпл = 147,95 р., I_дср (0,1 %) = 1,64 р., удержание U = 146,31 р.
    /// </summary>
    [Fact]
    public void Early_return_matches_formulas_1_9_and_1_10()
    {
        var quote = EarlyReturnCalculator.Calculate(
            principal: 5000m,
            contractRatePercent: 9m,
            days: 120,
            yearDays: 365,
            demandRatePercent: 0.1m,
            paidInterest: 0m,
            requiresBankConsent: false,
            consentGranted: false);

        Assert.Equal(147.95m, quote.ContractInterest);
        Assert.Equal(1.64m, quote.DemandInterest);
        Assert.Equal(5001.64m, quote.NetPayout);
        Assert.True(quote.Allowed);
    }

    /// <summary>(1.10) Удержание переплаты: 147,95 − 1,64 = 146,31 р.</summary>
    [Fact]
    public void Early_return_withholds_excess_interest()
    {
        var quote = EarlyReturnCalculator.Calculate(
            principal: 5000m,
            contractRatePercent: 9m,
            days: 120,
            yearDays: 365,
            demandRatePercent: 0.1m,
            paidInterest: 147.95m,
            requiresBankConsent: false,
            consentGranted: false);

        Assert.Equal(146.31m, quote.ExcessToWithhold);
        Assert.Equal(4853.69m, quote.NetPayout);
    }

    /// <summary>Безотзывный вклад возвращается досрочно только с согласия банка.</summary>
    [Fact]
    public void Early_return_requires_bank_consent_for_irrevocable_deposit()
    {
        var quote = EarlyReturnCalculator.Calculate(5000m, 9m, 120, 365, 0.1m, 0m, true, false);

        Assert.False(quote.Allowed);
        Assert.NotNull(quote.Reason);
    }
}
