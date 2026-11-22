using FinFlow.Application.Services;
using FinFlow.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinFlow.Api.Controllers;

[ApiController]
[Route("api/operations")]
[Authorize(Roles = "Employee,Administrator")]
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

    /// <summary>Проведение операции: проверка доступного остатка и исполнение в одной транзакции.</summary>
    [HttpPost]
    public async Task<ActionResult<OperationDto>> Post([FromBody] CreateOperationRequest request, CancellationToken cancellationToken) =>
        Ok(await _operations.PostAsync(request, cancellationToken));

    [HttpPost("{id:guid}/reverse")]
    public async Task<ActionResult<OperationDto>> Reverse(Guid id, [FromBody] ReverseRequest request, CancellationToken cancellationToken) =>
        Ok(await _operations.ReverseAsync(id, request, cancellationToken));
}
