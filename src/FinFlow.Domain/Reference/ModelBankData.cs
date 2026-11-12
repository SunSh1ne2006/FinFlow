using FinFlow.Domain.Calculators;
using FinFlow.Domain.Enums;

namespace FinFlow.Domain.Reference;

/// <summary>Риск модельного реестра (таблицы 1.14, 1.15, 1.17 записки).</summary>
public sealed class ModelRisk
{
    public string Code { get; init; }
    public string Event { get; init; }
    public string Cause { get; init; }
    public string Consequence { get; init; }
    public string Category { get; init; }
    public string OwnerRole { get; init; }
    public string Mitigation { get; init; }
    public string EarlyIndicator { get; init; }
    public decimal Probability { get; init; }
    /// <summary>Денежная оценка последствий C, тыс. р.</summary>
    public decimal MoneyImpact { get; init; }
    /// <summary>Вероятность после мероприятий p′ (таблица 1.17).</summary>
    public decimal MitigatedProbability { get; init; }
}

/// <summary>Элемент анализа чувствительности (таблица 1.18).</summary>
public sealed class SensitivityItem
{
    public string Factor { get; init; }
    public string BaseValue { get; init; }
    public string Change { get; init; }
    public string Impact { get; init; }
    public string Indicator { get; init; }
    public decimal ImpactValue { get; init; }
}

/// <summary>
/// Данные модельного банка из пояснительной записки. Используются для начального заполнения
/// справочников, проверки примеров (таблицы 1.5, 1.15, 1.18, п. 1.3.2, 1.3.5) и для режима
/// «модельный сценарий» в аналитическом контуре.
/// </summary>
public static class ModelBankData
{
    // Процентные параметры (п. 1.3.3, 1.3.5, 1.3.6).
    public const decimal TaxRatePercent = 13m;
    public const decimal DemandRatePercent = 0.1m;
    public const decimal RefinancingRatePercent = 9.25m;
    public const decimal RvsrByn1To3Years = 12.17m;
    public const decimal RvsrBynOver3Years = 14.33m;
    public const decimal TransferRatePercent = 9.05m;
    public const decimal OperatingCostPercent = 0.40m;

    // Допущения ликвидности (п. 1.3.4).
    public const decimal ProlongationShare = 0.40m;
    public const decimal EarlyWithdrawalShare = 0.002m;
    public const decimal RevocablePortfolio = 40_000m; // тыс. р.
    public const decimal OpeningCash = 20_000m;        // тыс. р.
    public const decimal MinimumCash = 10_000m;        // тыс. р.

    // Лимиты ролей и порог ПОД/ФТ (допущения для демонстрации).
    public const decimal OperatorLimit = 5_000m;
    public const decimal ControllerLimit = 50_000m;
    public const decimal AmlThreshold = 20_000m;

    /// <summary>Портфель привлечённых средств 240 000 тыс. р. (п. 1.3.5).</summary>
    public static IReadOnlyList<PortfolioSegment> PortfolioSegments { get; } = new List<PortfolioSegment>
    {
        new() { Name = "Безотзывные вклады в BYN", Volume = 120_000m, RatePercent = 11.8m },
        new() { Name = "Отзывные вклады в BYN", Volume = 40_000m, RatePercent = 9.0m },
        new() { Name = "Валютные вклады", Volume = 50_000m, RatePercent = 1.5m },
        new() { Name = "Остатки на текущих счетах", Volume = 30_000m, RatePercent = 0.1m }
    };

    /// <summary>Реестр рисков R-01 – R-12 (таблицы 1.14, 1.15, 1.17).</summary>
    public static IReadOnlyList<ModelRisk> Risks { get; } = new List<ModelRisk>
    {
        new()
        {
            Code = "R-01", Event = "Массовый отток отзывных вкладов",
            Cause = "Негативные новости, снижение ставок, рост курса",
            Consequence = "Разрыв ликвидности, дорогое привлечение",
            Category = "Ликвидность", OwnerRole = "Казначейство",
            Mitigation = "Резерв ликвидности, ставочные меры, мониторинг λ",
            EarlyIndicator = "Изъятия выше 0,3 % портфеля в день",
            Probability = 0.20m, MoneyImpact = 900m, MitigatedProbability = 0.10m
        },
        new()
        {
            Code = "R-02", Event = "Процентный риск",
            Cause = "Изменение ставки рефинансирования и РВСР",
            Consequence = "Снижение маржи по фиксированным ставкам",
            Category = "Процентный", OwnerRole = "КУАП",
            Mitigation = "Плавающие ставки, сроковая структура",
            EarlyIndicator = "Изменение ставки рефинансирования",
            Probability = 0.40m, MoneyImpact = 500m, MitigatedProbability = 0.25m
        },
        new()
        {
            Code = "R-03", Event = "Ошибка параметризации и начисления процентов",
            Cause = "Ручной ввод условий, отсутствие версий",
            Consequence = "Переплата, претензии клиентов",
            Category = "Операционный", OwnerRole = "Операционное управление",
            Mitigation = "Версии продукта, двойной контроль условий, тесты",
            EarlyIndicator = "Расхождение контрольного пересчёта",
            Probability = 0.30m, MoneyImpact = 350m, MitigatedProbability = 0.10m
        },
        new()
        {
            Code = "R-04", Event = "Нарушение требований ПОД/ФТ",
            Cause = "Неполная анкета, пропуск порога",
            Consequence = "Санкции регулятора",
            Category = "Комплаенс", OwnerRole = "Подразделение ПОД/ФТ",
            Mitigation = "Автоматические пороги, приостановление",
            EarlyIndicator = "Доля анкет без актуализации",
            Probability = 0.15m, MoneyImpact = 700m, MitigatedProbability = 0.08m
        },
        new()
        {
            Code = "R-05", Event = "Операция по арестованному счёту",
            Cause = "Задержка применения ограничения",
            Consequence = "Ответственность банка за списанную сумму",
            Category = "Операционный", OwnerRole = "Операционное управление",
            Mitigation = "Онлайн-обмен с АИС ИДО, L = 0 при аресте",
            EarlyIndicator = "Время от поступления до применения",
            Probability = 0.25m, MoneyImpact = 300m, MitigatedProbability = 0.08m
        },
        new()
        {
            Code = "R-06", Event = "Внутреннее мошенничество",
            Cause = "Совмещение функций, слабый контроль",
            Consequence = "Хищение средств клиентов",
            Category = "Операционный", OwnerRole = "Служба безопасности",
            Mitigation = "Разделение обязанностей, аудит",
            EarlyIndicator = "Операции без контролёра",
            Probability = 0.10m, MoneyImpact = 600m, MitigatedProbability = 0.05m
        },
        new()
        {
            Code = "R-07", Event = "Внешнее мошенничество в ДБО",
            Cause = "Социальная инженерия, компрометация",
            Consequence = "Возмещение клиентам, репутация",
            Category = "Информационная безопасность", OwnerRole = "Управление ИБ",
            Mitigation = "Адаптивный контроль, лимиты, уведомления",
            EarlyIndicator = "Нетипичные операции клиента",
            Probability = 0.35m, MoneyImpact = 400m, MitigatedProbability = 0.20m
        },
        new()
        {
            Code = "R-08", Event = "Превышение РВСР и регуляторных требований",
            Cause = "Ставки утверждаются без контроля",
            Consequence = "Повышенные требования к капиталу",
            Category = "Комплаенс", OwnerRole = "КУАП",
            Mitigation = "Контроль РВСР при утверждении продукта",
            EarlyIndicator = "Ставка продукта выше РВСР",
            Probability = 0.20m, MoneyImpact = 450m, MitigatedProbability = 0.10m
        },
        new()
        {
            Code = "R-09", Event = "Несвоевременная доработка под изменение нормативки",
            Cause = "Переход на изменённые номера счетов",
            Consequence = "Возврат документов, простой",
            Category = "Комплаенс", OwnerRole = "ИТ-подразделение",
            Mitigation = "Хранение двух номеров, тестовый прогон",
            EarlyIndicator = "Доля счетов без нового номера",
            Probability = 0.15m, MoneyImpact = 500m, MitigatedProbability = 0.08m
        },
        new()
        {
            Code = "R-10", Event = "Концентрация на крупных вкладчиках",
            Cause = "Зависимость от 20 крупнейших клиентов",
            Consequence = "Резкий отток при уходе клиента",
            Category = "Ликвидность", OwnerRole = "Казначейство",
            Mitigation = "Лимиты концентрации, индивидуальные условия",
            EarlyIndicator = "Доля 20 крупнейших выше 25 %",
            Probability = 0.25m, MoneyImpact = 600m, MitigatedProbability = 0.15m
        },
        new()
        {
            Code = "R-11", Event = "Утечка данных, нарушение банковской тайны",
            Cause = "Ошибка прав доступа, атака",
            Consequence = "Штрафы, претензии, репутация",
            Category = "Информационная безопасность", OwnerRole = "Управление ИБ",
            Mitigation = "Шифрование, 2FA, аудит доступа",
            EarlyIndicator = "Аномальные выгрузки",
            Probability = 0.10m, MoneyImpact = 800m, MitigatedProbability = 0.05m
        },
        new()
        {
            Code = "R-12", Event = "Отказ ИТ-инфраструктуры",
            Cause = "Сбой сервера, ошибка резервирования",
            Consequence = "Простой ЦБУ и ДБО",
            Category = "Информационная безопасность", OwnerRole = "ИТ-подразделение",
            Mitigation = "Кластер, резервное копирование, тест восстановления",
            EarlyIndicator = "Сбой репликации",
            Probability = 0.10m, MoneyImpact = 700m, MitigatedProbability = 0.05m
        }
    };

    /// <summary>Допущения прогноза ликвидности (п. 1.3.4).</summary>
    public static LiquidityAssumptions LiquidityDemo => new()
    {
        OpeningCash = OpeningCash,
        MinimumCash = MinimumCash,
        RevocablePortfolio = RevocablePortfolio,
        ProlongationShare = ProlongationShare,
        EarlyWithdrawalShare = EarlyWithdrawalShare
    };

    /// <summary>
    /// Пятидневный горизонт таблицы 1.5. Проценты к выплате определены как остаток табличного оттока
    /// после вычитания погашения вкладов и досрочных изъятий: день 3 – 820 тыс. р. (п. 1.3.4).
    /// </summary>
    public static IReadOnlyList<LiquidityDayInput> LiquidityDemoDays()
    {
        var start = new DateOnly(2026, 11, 16);
        var rows = new (decimal Maturing, decimal Inflow, decimal Outflow)[]
        {
            (0m, 1_500m, 4_000m),
            (0m, 2_000m, 3_500m),
            (14_000m, 800m, 9_300m),
            (0m, 3_000m, 1_500m),
            (0m, 2_500m, 1_000m)
        };

        var result = new List<LiquidityDayInput>();
        for (var i = 0; i < rows.Length; i++)
        {
            var (maturing, inflow, outflow) = rows[i];
            var early = Math.Round(RevocablePortfolio * EarlyWithdrawalShare, 2, MidpointRounding.AwayFromZero);
            var maturityPart = maturing * (1m - ProlongationShare);
            var interest = outflow - maturityPart - early;
            result.Add(new LiquidityDayInput
            {
                Date = start.AddDays(i),
                MaturingPrincipal = maturing,
                Inflow = inflow,
                InterestPayable = interest < 0m ? 0m : interest
            });
        }

        return result;
    }

    /// <summary>Анализ чувствительности (таблица 1.18).</summary>
    public static IReadOnlyList<SensitivityItem> Sensitivity { get; } = new List<SensitivityItem>
    {
        new()
        {
            Factor = "Ставка по срочным вкладам в BYN", BaseValue = "160 000 тыс. р.",
            Change = "+1 п. п.", Impact = "+1 600 тыс. р. в год", Indicator = "Процентные расходы, M",
            ImpactValue = 1_600m
        },
        new()
        {
            Factor = "Доля пролонгации ρ", BaseValue = "0,40 в день 3",
            Change = "−10 п. п.", Impact = "+1 400 тыс. р. оттока", Indicator = "Gap3: 2 500 → 3 900",
            ImpactValue = 1_400m
        },
        new()
        {
            Factor = "Досрочное изъятие λ", BaseValue = "0,002 от 40 000 тыс. р.",
            Change = "+0,1 п. п.", Impact = "+40 тыс. р. в день, ≈ +880 в месяц", Indicator = "Out_t",
            ImpactValue = 880m
        },
        new()
        {
            Factor = "Курс иностранной валюты", BaseValue = "50 000 тыс. р.",
            Change = "+10 %", Impact = "+5 000 тыс. р. эквивалента", Indicator = "Обязательства в BYN",
            ImpactValue = 5_000m
        }
    };

    /// <summary>Восемь ролей п. 1.5 и их клиентские приложения.</summary>
    public static IReadOnlyList<(RoleCode Code, string Name, string Client, string Description)> Roles { get; } =
        new List<(RoleCode, string, string, string)>
        {
            (RoleCode.Operator, "Операционист ЦБУ", "WPF", "Идентификация клиента, открытие и закрытие счетов и вкладов, приём распоряжений, кассовые и безналичные операции"),
            (RoleCode.Controller, "Контролёр", "WPF", "Подтверждение операций выше лимита, проверка документов, сторнирование"),
            (RoleCode.Accountant, "Бухгалтер операционного управления", "WPF", "Закрытие дня, начисление процентов, сверка с главной книгой, налог"),
            (RoleCode.Administrator, "Администратор", "WPF", "Пользователи, роли, справочники, журналы, резервное копирование"),
            (RoleCode.Head, "Руководитель (член КУАП)", "Веб", "Портфель, стоимость ресурсов, маржа, разрыв ликвидности, риски"),
            (RoleCode.Treasurer, "Специалист казначейства", "Веб", "Прогноз оттока, пролонгация, ставки, лимиты"),
            (RoleCode.RiskManager, "Риск-менеджер", "Веб", "Реестр рисков, EMV, резерв, чувствительность, матрица"),
            (RoleCode.AmlOfficer, "Специалист по ПОД/ФТ", "Веб", "Рассмотрение приостановленных операций, анкеты, отчётность")
        };
}
