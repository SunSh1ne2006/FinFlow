using FinFlow.Domain.Common;

namespace FinFlow.Domain.Calculators;

/// <summary>
/// Расчёт показателей остатка (п. 1.3.1 записки).
/// Формула (1.1): D = max(0; B + L − H), при аресте или приостановлении L = 0.
/// </summary>
public static class BalanceCalculator
{
    /// <summary>Доступный остаток D по формуле (1.1).</summary>
    public static decimal Available(decimal balance, decimal overdraftLimit, decimal holdsAmount, bool hasArrest)
    {
        var limit = hasArrest ? 0m : overdraftLimit;
        var available = balance + limit - holdsAmount;
        return available < 0m ? 0m : Money.Round(available);
    }

    /// <summary>Использованная часть овердрафта по формуле (1.3).</summary>
    public static decimal OverdraftUsed(decimal balance) => balance < 0m ? Money.Round(-balance) : 0m;

    /// <summary>Коэффициент использования лимита овердрафта по формуле (1.4), %.</summary>
    public static decimal OverdraftUtilizationPercent(decimal balance, decimal overdraftLimit)
    {
        if (overdraftLimit <= 0m)
        {
            return 0m;
        }

        return Money.Round(OverdraftUsed(balance) / overdraftLimit * 100m);
    }

    /// <summary>Балансовый остаток после операций за период по формуле (1.2).</summary>
    public static decimal BalanceAfter(decimal openingBalance, IEnumerable<decimal> credits, IEnumerable<decimal> debits)
    {
        var balance = openingBalance;
        foreach (var credit in credits)
        {
            balance += credit;
        }

        foreach (var debit in debits)
        {
            balance -= debit;
        }

        return Money.Round(balance);
    }

    /// <summary>Проверка достаточности средств для расходной операции (бизнес-правило 5).</summary>
    public static bool CanDebit(decimal amount, decimal availableBalance) => amount > 0m && amount <= availableBalance;
}
