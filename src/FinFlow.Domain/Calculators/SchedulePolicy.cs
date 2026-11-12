using FinFlow.Domain.Entities;

namespace FinFlow.Domain.Calculators;

/// <summary>
/// Календарные правила начисления, капитализации, выплаты процентов и пролонгации.
/// </summary>
public static class SchedulePolicy
{
    /// <summary>Дата того же числа через n месяцев с ограничением по длине месяца.</summary>
    public static DateOnly AddMonthsClamped(DateOnly date, int months)
    {
        var total = date.Month - 1 + months;
        var year = date.Year + total / 12;
        var month = total % 12 + 1;
        var day = Math.Min(date.Day, DateTime.DaysInMonth(year, month));
        return new DateOnly(year, month, day);
    }

    /// <summary>Наступила ли дата ежемесячной капитализации или выплаты процентов.</summary>
    public static bool IsMonthlyAnniversary(Contract contract, DateOnly date)
    {
        var open = contract.OpenDate;
        if (date <= open)
        {
            return false;
        }

        var daysInMonth = DateTime.DaysInMonth(date.Year, date.Month);
        var expectedDay = Math.Min(open.Day, daysInMonth);
        return date.Day == expectedDay;
    }

    /// <summary>Новый срок договора при пролонгации.</summary>
    public static DateOnly ProlongatedEndDate(Contract contract, DateOnly fromDate)
    {
        var months = contract.ProductVersion?.TermMonths ?? 0;
        if (months <= 0)
        {
            months = 12;
        }

        return AddMonthsClamped(fromDate, months);
    }

    /// <summary>Осталось ли начисление за дату уже выполненным (идемпотентность закрытия дня).</summary>
    public static bool AlreadyAccrued(Contract contract, DateOnly date) =>
        contract.Accruals != null && contract.Accruals.Any(a => a.AccrualDate == date);
}
