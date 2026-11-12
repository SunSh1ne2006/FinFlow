using FinFlow.Application.Services;
using FinFlow.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinFlow.Api.Controllers;

[ApiController]
[Route("api/contracts")]
[Authorize(Roles = "Operator,Controller,Accountant,Administrator,Head,Treasurer")]
public class ContractsController : ControllerBase
{
    private readonly ContractService _contracts;

    public ContractsController(ContractService contracts)
    {
        _contracts = contracts;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ContractDto>>> List(
        [FromQuery] Guid? clientId,
        [FromQuery] string status,
        CancellationToken cancellationToken) =>
        Ok(await _contracts.ListAsync(clientId, status, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ContractDto>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await _contracts.GetAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Roles = "Operator")]
    public async Task<ActionResult<ContractDto>> Open([FromBody] OpenContractRequest request, CancellationToken cancellationToken) =>
        Ok(await _contracts.OpenAsync(request, cancellationToken));

    /// <summary>Согласие банка на досрочный возврат безотзывного вклада (бизнес-правило 8).</summary>
    [HttpPost("{id:guid}/consent")]
    [Authorize(Roles = "Controller,Head,Administrator")]
    public async Task<ActionResult<ContractDto>> Consent(Guid id, CancellationToken cancellationToken) =>
        Ok(await _contracts.ConsentAsync(id, cancellationToken));

    [HttpGet("{id:guid}/early-return-quote")]
    public async Task<ActionResult<EarlyReturnQuoteDto>> EarlyReturnQuote(Guid id, [FromQuery] int? days, CancellationToken cancellationToken) =>
        Ok(await _contracts.QuoteEarlyReturnAsync(id, days, cancellationToken));

    [HttpGet("{id:guid}/accruals")]
    public async Task<ActionResult<IReadOnlyList<InterestAccrualDto>>> Accruals(Guid id, CancellationToken cancellationToken) =>
        Ok(await _contracts.GetAccrualsAsync(id, cancellationToken));
}
