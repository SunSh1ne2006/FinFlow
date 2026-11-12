using FinFlow.Domain.Enums;

namespace FinFlow.Application.Common;

/// <summary>Разбор строковых значений из запросов клиентов в перечисления предметной области.</summary>
public static class Parsing
{
    public static TEnum Enum<TEnum>(string value, string fieldName, TEnum fallback = default) where TEnum : struct, System.Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        if (System.Enum.TryParse<TEnum>(value, true, out var parsed) && System.Enum.IsDefined(typeof(TEnum), parsed))
        {
            return parsed;
        }

        throw new AppException($"Недопустимое значение поля «{fieldName}»: {value}.");
    }

    public static string Text(this ClientType type) => type switch
    {
        ClientType.Individual => "Физическое лицо",
        ClientType.LegalEntity => "Юридическое лицо",
        ClientType.Entrepreneur => "Индивидуальный предприниматель",
        _ => type.ToString()
    };

    public static string Text(this IdentificationStatus status) => status switch
    {
        IdentificationStatus.NotIdentified => "Не идентифицирован",
        IdentificationStatus.Identified => "Идентифицирован",
        IdentificationStatus.RequiresUpdate => "Требует актуализации",
        _ => status.ToString()
    };

    public static string Text(this KycRiskLevel level) => level switch
    {
        KycRiskLevel.Low => "Низкий",
        KycRiskLevel.Medium => "Средний",
        KycRiskLevel.High => "Высокий",
        _ => level.ToString()
    };

    public static string Text(this ProductKind kind) => kind switch
    {
        ProductKind.Deposit => "Вклад",
        ProductKind.CurrentAccount => "Текущий счёт",
        _ => kind.ToString()
    };

    public static string Text(this DepositType type) => type switch
    {
        DepositType.Demand => "До востребования",
        DepositType.Term => "Срочный",
        DepositType.Conditional => "Условный",
        _ => "Не вклад"
    };

    public static string Text(this HoldSource source) => source switch
    {
        HoldSource.AisIdo => "АИС ИДО",
        HoldSource.Bank => "Решение банка",
        HoldSource.CardAuthorization => "Авторизационная блокировка",
        HoldSource.Booking => "Бронирование",
        _ => source.ToString()
    };

    public static string Text(this AccountStatus status) => status switch
    {
        AccountStatus.Open => "Открыт",
        AccountStatus.Closed => "Закрыт",
        _ => status.ToString()
    };

    public static string Text(this DayCloseStage stage) => stage switch
    {
        DayCloseStage.Started => "Начало закрытия",
        DayCloseStage.PendingRejected => "Отклонение неподтверждённых операций",
        DayCloseStage.AccrualsDone => "Начисление процентов",
        DayCloseStage.PayoutsDone => "Капитализация и выплата процентов",
        DayCloseStage.ContractsDone => "Пролонгация и возврат вкладов",
        DayCloseStage.PostingsDone => "Формирование проводок",
        DayCloseStage.Reconciled => "Сверка с главной книгой",
        DayCloseStage.Completed => "Закрытие завершено",
        _ => stage.ToString()
    };

    public static string Text(this OperationalDayStatus status) => status switch
    {
        OperationalDayStatus.Open => "Открыт",
        OperationalDayStatus.Closing => "Закрывается",
        OperationalDayStatus.Closed => "Закрыт",
        OperationalDayStatus.Failed => "Ошибка закрытия",
        _ => status.ToString()
    };
}
