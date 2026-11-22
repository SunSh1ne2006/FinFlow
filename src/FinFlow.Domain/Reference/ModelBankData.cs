using FinFlow.Domain.Enums;

namespace FinFlow.Domain.Reference;

/// <summary>
/// Модельные параметры банка из пояснительной записки: ставки и роли сотрудников.
/// Используются для начального заполнения справочников.
/// </summary>
public static class ModelBankData
{
    /// <summary>Ставка подоходного налога с процентов по вкладам.</summary>
    public const decimal TaxRatePercent = 13m;

    /// <summary>Ставка до востребования для перерасчёта при досрочном возврате.</summary>
    public const decimal DemandRatePercent = 0.1m;

    /// <summary>Ставка рефинансирования (справочно).</summary>
    public const decimal RefinancingRatePercent = 9.25m;

    /// <summary>Три роли сотрудников банка и их рабочие места.</summary>
    public static IReadOnlyList<(RoleCode Code, string Name, string Client, string Description)> Roles { get; } =
        new List<(RoleCode, string, string, string)>
        {
            (RoleCode.Employee, "Сотрудник банка", "WPF",
                "Регистрация клиентов, открытие и закрытие счетов и вкладов, операции по счетам, закрытие операционного дня"),
            (RoleCode.Administrator, "Администратор", "WPF",
                "Учётные записи сотрудников, блокировка доступа, журнал аудита"),
            (RoleCode.Head, "Руководитель", "WPF",
                "Отчёт о работе сотрудников за период: клиенты, счета, вклады, операции")
        };
}
