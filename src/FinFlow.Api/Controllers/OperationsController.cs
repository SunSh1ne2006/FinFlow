using FinFlow.Application.Services;
using FinFlow.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinFlow.Api.Controllers;

[ApiController]
[Route("api/operations")]
[Authorize]
public class OperationsController : ControllerBase
{
    private readonly OperationService _operations;

    public OperationsController(OperationService operations)
    {
        _operations = operations;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OperationDto>>> Query(
        [FromQuery] Guid? accountId,
        [FromQuery] string status,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default) =>
        Ok(await _operations.QueryAsync(accountId, status, take, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OperationDto>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await _operations.GetAsync(id, cancellationToken));

    [HttpGet("queue")]
    [Authorize(Roles = "Controller,Administrator,Head")]
    public async Task<ActionResult<IReadOnlyList<OperationDto>>> Queue([FromQuery] int take = 100, CancellationToken cancellationToken = default) =>
        Ok(await _operations.QueueAsync(take, cancellationToken));

    [HttpGet("suspended")]
    [Authorize(Roles = "AmlOfficer,Administrator,Head")]
    public async Task<ActionResult<IReadOnlyList<OperationDto>>> Suspended([FromQuery] int take = 100, CancellationToken cancellationToken = default) =>
        Ok(await _operations.SuspendedAsync(take, cancellationToken));

    [HttpPost]
    [Authorize(Roles = "Operator,Administrator")]
    public async Task<ActionResult<OperationDto>> Create([FromBody] CreateOperationRequest request, CancellationToken cancellationToken) =>
        Ok(await _operations.CreateAsync(request, cancellationToken));

    [HttpPost("{id:guid}/submit")]
    [Authorize(Roles = "Operator,Administrator")]
    public async Task<ActionResult<OperationDto>> Submit(Guid id, CancellationToken cancellationToken) =>
        Ok(await _operations.SubmitAsync(id, cancellationToken));

    /// <summary>Подтверждение операции контролёром: автор не может подтвердить свою операцию.</summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = "Controller,Administrator,Head")]
    public async Task<ActionResult<OperationDto>> Approve(Guid id, [FromBody] ApproveRequest request, CancellationToken cancellationToken) =>
        Ok(await _operations.ApproveAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = "Controller,Administrator,Head,AmlOfficer")]
    public async Task<ActionResult<OperationDto>> Reject(Guid id, [FromBody] RejectRequest request, CancellationToken cancellationToken) =>
        Ok(await _operations.RejectAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/reverse")]
    [Authorize(Roles = "Controller,Administrator")]
    public async Task<ActionResult<OperationDto>> Reverse(Guid id, [FromBody] ReverseRequest request, CancellationToken cancellationToken) =>
        Ok(await _operations.ReverseAsync(id, request, cancellationToken));
}
