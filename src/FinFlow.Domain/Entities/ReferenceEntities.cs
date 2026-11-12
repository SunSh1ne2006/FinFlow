using FinFlow.Domain.Common;
using FinFlow.Domain.Enums;

namespace FinFlow.Domain.Entities;

/// <summary>Справочник параметров с датой действия (требование 16, бизнес-правило 14).</summary>
public class Parameter : BaseEntity
{
    public string Code { get; set; }
    public string Name { get; set; }
    public string Unit { get; set; }
    public decimal Value { get; set; }
    public string TextValue { get; set; }
    public DateOnly ValidFrom { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Риск реестра R-01 – R-12 (требование 14).</summary>
public class Risk : BaseEntity
{
    public string Code { get; set; }
    public string Event { get; set; }
    public string Cause { get; set; }
    public string Consequence { get; set; }
    public string Category { get; set; }
    public string OwnerRole { get; set; }
    public RiskStatus Status { get; set; } = RiskStatus.Active;
    public string Mitigation { get; set; }
    public string EarlyIndicator { get; set; }

    public List<RiskAssessment> Assessments { get; set; } = new();

    public RiskAssessment LatestAssessment => Assessments
        .OrderByDescending(a => a.Date).ThenByDescending(a => a.CreatedAt).FirstOrDefault();
}

/// <summary>Версионируемая оценка риска: вероятность, денежные последствия, оценка после мероприятий.</summary>
public class RiskAssessment : BaseEntity
{
    public Guid RiskId { get; set; }
    public Risk Risk { get; set; }
    public DateOnly Date { get; set; }
    public decimal Probability { get; set; }
    /// <summary>Денежная оценка последствий C, тыс. р.</summary>
    public decimal MoneyImpact { get; set; }
    public decimal? MitigatedProbability { get; set; }
    public string Notes { get; set; }
}

/// <summary>Запись журнала аудита. Только вставка, хранение не менее 5 лет (требование 19).</summary>
public class AuditRecord : BaseEntity
{
    public DateTime At { get; set; } = DateTime.UtcNow;
    public Guid? UserId { get; set; }
    public string UserLogin { get; set; }
    public string Action { get; set; }
    public string EntityName { get; set; }
    public string EntityId { get; set; }
    public string OldValue { get; set; }
    public string NewValue { get; set; }
    public string IpAddress { get; set; }
}
