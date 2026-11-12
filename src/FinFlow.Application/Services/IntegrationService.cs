using System.Text.Json;
using FinFlow.Application.Abstractions;
using FinFlow.Application.Common;
using FinFlow.Contracts;
using FinFlow.Domain.Entities;
using FinFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Application.Services;

/// <summary>
/// Интеграции (требования 20 и 21): приём документов АИС ИДО с однократной обработкой,
/// формирование квитанций и сверка с главной книгой АБС.
/// </summary>
public class IntegrationService
{
    private readonly IBankDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditWriter _audit;
    private readonly IAisIdoGateway _aisIdo;
    private readonly HoldService _holds;
    private readonly DayCloseService _dayClose;

    public IntegrationService(
        IBankDbContext db,
        IClock clock,
        IAuditWriter audit,
        IAisIdoGateway aisIdo,
        HoldService holds,
        DayCloseService dayClose)
    {
        _db = db;
        _clock = clock;
        _audit = audit;
        _aisIdo = aisIdo;
        _holds = holds;
        _dayClose = dayClose;
    }

    public async Task<IntegrationResultDto> AcceptAisIdoDocumentAsync(AisIdoDocumentRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.ExternalId))
        {
            throw new AppException("Укажите идентификатор документа АИС ИДО.");
        }

        if (request.Amount <= 0m)
        {
            throw new AppException("Сумма постановления должна быть больше нуля.");
        }

        var externalId = request.ExternalId.Trim();
        var existing = await _db.Documents
            .FirstOrDefaultAsync(d => d.SourceSystem == DocumentSource.AisIdo && d.ExternalId == externalId, cancellationToken);

        if (existing != null)
        {
            await _aisIdo.SendReceiptAsync(externalId, true, "Документ уже обработан ранее", cancellationToken);
            return new IntegrationResultDto
            {
                Accepted = true,
                Duplicate = true,
                Message = $"Документ {externalId} уже обработан: повторный приём не создаёт дубль (требование 23).",
                DocumentId = existing.Id
            };
        }

        var account = await FindAccountAsync(request.AccountNumber, cancellationToken);
        if (account == null)
        {
            await _aisIdo.SendReceiptAsync(externalId, false, "Счёт не найден", cancellationToken);
            return new IntegrationResultDto
            {
                Accepted = false,
                Duplicate = false,
                Message = $"Счёт {request.AccountNumber} не найден: документ не принят."
            };
        }

        var document = new OperationDocument
        {
            Number = $"АИС-{_clock.Today:yyyyMMdd}-{externalId}",
            SourceSystem = DocumentSource.AisIdo,
            ExternalId = externalId,
            Kind = request.Kind,
            ReceivedAt = _clock.UtcNow,
            Payload = JsonSerializer.Serialize(request)
        };

        _db.Documents.Add(document);
        await _db.SaveChangesAsync(cancellationToken);

        var hold = await _holds.AddAsync(account.Id, new CreateHoldRequest
        {
            Source = "AisIdo",
            BasisNumber = request.BasisNumber,
            BasisAuthority = request.BasisAuthority,
            Amount = request.Amount,
            IsArrest = !string.Equals(request.Kind, "Booking", StringComparison.OrdinalIgnoreCase),
            ValidFrom = request.ValidFrom ?? _clock.Today,
            ValidTo = request.ValidTo,
            Comment = request.Comment
        }, cancellationToken);

        await _aisIdo.SendReceiptAsync(externalId, true, "Постановление принято, ограничение применено", cancellationToken);

        await _audit.WriteAsync("AisIdoDocumentAccepted", nameof(Hold), hold.Id.ToString(), null,
            new { request.ExternalId, request.Kind, request.Amount, Account = request.AccountNumber }, cancellationToken);

        return new IntegrationResultDto
        {
            Accepted = true,
            Duplicate = false,
            Message = "Постановление принято, ограничение применено.",
            HoldId = hold.Id,
            AccountId = account.Id,
            DocumentId = document.Id
        };
    }

    public Task<ReconciliationDto> SyncLedgerAsync(DateOnly date, CancellationToken cancellationToken = default) =>
        _dayClose.ReconcileAsync(date, cancellationToken);

    private async Task<Account> FindAccountAsync(string number, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(number))
        {
            return null;
        }

        var digits = new string(number.Where(char.IsDigit).ToArray());
        var accountNumber = await _db.AccountNumbers
            .FirstOrDefaultAsync(n => n.Number == number || n.Number == digits, cancellationToken);

        if (accountNumber == null)
        {
            return null;
        }

        return await _db.Accounts.FirstOrDefaultAsync(a => a.Id == accountNumber.AccountId, cancellationToken);
    }
}
