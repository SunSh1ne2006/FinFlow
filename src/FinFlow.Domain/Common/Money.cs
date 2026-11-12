namespace FinFlow.Domain.Common;

/// <summary>
/// Единая политика округления (НФТ-12): денежные суммы хранятся decimal(18,4),
/// результат расчёта округляется до 0,01 один раз — по итогу периода, а не по каждому дню.
/// </summary>
public static class Money
{
    public const int Scale = 2;
    public const int StorageScale = 4;
    public const int RateScale = 6;

    /// <summary>Округление денежной суммы до 0,01 р.</summary>
    public static decimal Round(decimal value) => decimal.Round(value, Scale, MidpointRounding.AwayFromZero);

    /// <summary>Округление процентной ставки (справочно, 4 знака).</summary>
    public static decimal Percent(decimal value) => decimal.Round(value, 4, MidpointRounding.AwayFromZero);

    /// <summary>Округление процентной ставки для отображения (2 знака).</summary>
    public static decimal Percent2(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    public static decimal Max(decimal left, decimal right) => left > right ? left : right;

    public static decimal Abs(decimal value) => value < 0 ? -value : value;

    /// <summary>Эквивалент суммы в BYN по официальному курсу НБРБ на дату операции.</summary>
    public static decimal ToByn(decimal amount, decimal rate) => Round(amount * rate);
}
