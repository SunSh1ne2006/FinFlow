using FinFlow.Application.Services;
using FinFlow.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinFlow.Api.Controllers;

[ApiController]
[Route("api/clients")]
[Authorize(Roles = "Operator,Controller,Accountant,Administrator,Head")]
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
    [Authorize(Roles = "Operator,Administrator")]
    public async Task<ActionResult<ClientDetailsDto>> Create([FromBody] CreateClientRequest request, CancellationToken cancellationToken) =>
        Ok(await _clients.CreateAsync(request, cancellationToken));

    [HttpPut("{id:guid}/kyc")]
    [Authorize(Roles = "Operator,Administrator,AmlOfficer")]
    public async Task<ActionResult<ClientDetailsDto>> UpdateKyc(Guid id, [FromBody] UpdateKycRequest request, CancellationToken cancellationToken) =>
        Ok(await _clients.UpdateKycAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/representatives")]
    [Authorize(Roles = "Operator,Administrator")]
    public async Task<ActionResult<RepresentativeDto>> AddRepresentative(Guid id, [FromBody] AddRepresentativeRequest request, CancellationToken cancellationToken) =>
        Ok(await _clients.AddRepresentativeAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/msi-sync")]
    [Authorize(Roles = "Operator,Administrator")]
    public async Task<ActionResult<ClientDetailsDto>> MsiSync(Guid id, CancellationToken cancellationToken) =>
        Ok(await _clients.SyncWithMsiAsync(id, cancellationToken));
}
