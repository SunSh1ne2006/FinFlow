namespace FinFlow.Domain.Calculators;

/// <summary>Маршрут операции после выполнения проверок шагов 2–5 таблицы 1.10.</summary>
public enum OperationRoute
{
    /// <summary>Проверки пройдены, подтверждение контролёра не требуется.</summary>
    AutoApprove = 1,
    /// <summary>Сумма выше лимита операциониста – операция уходит в очередь контроля.</summary>
    Control = 2,
    /// <summary>Выявлены признаки ПОД/ФТ – операция приостановлена.</summary>
    Suspend = 3
}

public sealed class WorkflowLimits
{
    public decimal OperatorLimit { get; init; }
    public decimal AmlThreshold { get; init; }
}

/// <summary>
/// Правила маршрутизации операции (требования 8 и 27, бизнес-правило 12):
/// операция выше лимита операциониста требует подтверждения контролёром;
/// операция с признаками ПОД/ФТ приостанавливается до решения специалиста.
/// </summary>
public static class WorkflowPolicy
{
    public static OperationRoute Evaluate(decimal amount, WorkflowLimits limits, bool hasAmlSigns)
    {
        if (hasAmlSigns)
        {
            return OperationRoute.Suspend;
        }

        if (limits.OperatorLimit > 0m && amount > limits.OperatorLimit)
        {
            return OperationRoute.Control;
        }

        return OperationRoute.AutoApprove;
    }

    /// <summary>Признаки ПОД/ФТ: сумма не ниже порога или повышенный риск клиента.</summary>
    public static bool HasAmlSigns(decimal amount, decimal threshold, bool highRiskClient) =>
        highRiskClient || (threshold > 0m && amount >= threshold);
}
