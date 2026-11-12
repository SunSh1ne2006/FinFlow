using FinFlow.Application.Services;
using FinFlow.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinFlow.Api.Controllers;

[ApiController]
[Route("api/integrations")]
public class IntegrationsController : ControllerBase
{
    private readonly IntegrationService _integrations;

    public IntegrationsController(IntegrationService integrations)
    {
        _integrations = integrations;
    }

    /// <summary>
    /// Приём постановления АИС ИДО. В продуктивной среде вызов защищается сертификатом/ЭЦП шлюза АБС;
    /// в демонстрационной версии доступен без токена пользователя (требование 20).
    /// </summary>
    [HttpPost("ais-ido/documents")]
    [AllowAnonymous]
    public async Task<ActionResult<IntegrationResultDto>> AcceptAisIdo([FromBody] AisIdoDocumentRequest request, CancellationToken cancellationToken) =>
        Ok(await _integrations.AcceptAisIdoDocumentAsync(request, cancellationToken));

    [HttpPost("ledger/sync")]
    [Authorize(Roles = "Accountant,Administrator,Head")]
    public async Task<ActionResult<ReconciliationDto>> SyncLedger([FromQuery] DateOnly? date, CancellationToken cancellationToken) =>
        Ok(await _integrations.SyncLedgerAsync(date ?? DateOnly.FromDateTime(DateTime.Today), cancellationToken));
}
