using FinFlow.Application.Abstractions;
using FinFlow.Contracts;
using FinFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Application.Services;

/// <summary>ПОД/ФТ-контроль: очередь приостановленных операций и решения специалиста (требование 27).</summary>
public class AmlService
{
    private readonly IBankDbContext _db;
    private readonly OperationService _operations;

    public AmlService(IBankDbContext db, OperationService operations)
    {
        _db = db;
        _operations = operations;
    }

    public async Task<IReadOnlyList<AmlQueueItemDto>> QueueAsync(int take, CancellationToken cancellationToken = default)
    {
        take = take is < 1 or > 200 ? 50 : take;
        var operations = await _operations.SuspendedAsync(take, cancellationToken);
        var result = new List<AmlQueueItemDto>();

        foreach (var operation in operations)
        {
            var account = await _db.Accounts
                .Include(a => a.Client)
                .ThenInclude(c => c.KycProfile)
                .FirstOrDefaultAsync(a => a.Id == operation.AccountId, cancellationToken);

            var riskLevel = account?.Client?.KycProfile?.RiskLevel;
            var isHighRisk = riskLevel == KycRiskLevel.High;

            result.Add(new AmlQueueItemDto
            {
                Operation = operation,
                ClientRiskLevel = riskLevel?.ToString(),
                KycStatus = account?.Client?.IdentificationStatus.ToString(),
                SuspicionReason = isHighRisk
                    ? "Клиент отнесён к высокому уровню риска ПОД/ФТ."
                    : $"Сумма операции {operation.Amount:0.00} {operation.Currency} достигла порога обязательного контроля."
            });
        }

        return result;
    }

    public Task<OperationDto> DecideAsync(Guid operationId, AmlDecisionRequest request, CancellationToken cancellationToken = default) =>
        _operations.AmlDecisionAsync(operationId, request, cancellationToken);
}
