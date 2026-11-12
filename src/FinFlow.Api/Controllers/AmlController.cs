using FinFlow.Application.Services;
using FinFlow.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinFlow.Api.Controllers;

[ApiController]
[Route("api/aml")]
[Authorize(Roles = "AmlOfficer,Administrator,Head")]
public class AmlController : ControllerBase
{
    private readonly AmlService _aml;

    public AmlController(AmlService aml)
    {
        _aml = aml;
    }

    /// <summary>Приостановленные операции, требующие рассмотрения специалистом ПОД/ФТ.</summary>
    [HttpGet("queue")]
    public async Task<ActionResult<IReadOnlyList<AmlQueueItemDto>>> Queue([FromQuery] int take = 50, CancellationToken cancellationToken = default) =>
        Ok(await _aml.QueueAsync(take, cancellationToken));

    [HttpPost("{operationId:guid}/decision")]
    public async Task<ActionResult<OperationDto>> Decide(Guid operationId, [FromBody] AmlDecisionRequest request, CancellationToken cancellationToken) =>
        Ok(await _aml.DecideAsync(operationId, request, cancellationToken));
}
