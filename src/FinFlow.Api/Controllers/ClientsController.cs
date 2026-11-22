using FinFlow.Application.Services;
using FinFlow.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinFlow.Api.Controllers;

[ApiController]
[Route("api/clients")]
[Authorize(Roles = "Employee")]
public class ClientsController : ControllerBase
{
    private readonly ClientService _clients;

    public ClientsController(ClientService clients)
    {
        _clients = clients;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ClientListItemDto>>> List(
        [FromQuery] string query,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await _clients.ListAsync(query, page, pageSize, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ClientDetailsDto>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await _clients.GetAsync(id, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<ClientDetailsDto>> Create([FromBody] CreateClientRequest request, CancellationToken cancellationToken) =>
        Ok(await _clients.CreateAsync(request, cancellationToken));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ClientDetailsDto>> Update(Guid id, [FromBody] UpdateClientRequest request, CancellationToken cancellationToken) =>
        Ok(await _clients.UpdateAsync(id, request, cancellationToken));
}
