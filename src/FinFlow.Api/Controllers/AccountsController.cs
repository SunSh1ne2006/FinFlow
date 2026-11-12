using FinFlow.Application.Services;
using FinFlow.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinFlow.Api.Controllers;

[ApiController]
[Route("api/accounts")]
[Authorize(Roles = "Operator,Controller,Accountant,Administrator,Head,Treasurer")]
public class AccountsController : ControllerBase
{
    private readonly AccountService _accounts;
    private readonly HoldService _holds;
    private readonly IBalanceService _balance;

    public AccountsController(AccountService accounts, HoldService holds, IBalanceService balance)
    {
        _accounts = accounts;
        _holds = holds;
        _balance = balance;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AccountListItemDto>>> List([FromQuery] Guid clientId, CancellationToken cancellationToken) =>
        Ok(await _accounts.ListByClientAsync(clientId, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AccountDetailsDto>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await _accounts.GetAsync(id, cancellationToken));

    /// <summary>Поиск по прежнему или изменённому номеру счёта (требование 3).</summary>
    [HttpGet("lookup")]
    public async Task<ActionResult<AccountDetailsDto>> Lookup([FromQuery] string number, CancellationToken cancellationToken) =>
        Ok(await _accounts.GetByNumberAsync(number, cancellationToken));

    [HttpGet("{id:guid}/balance")]
    public async Task<ActionResult<BalanceSnapshotDto>> Balance(Guid id, [FromQuery] DateOnly? date, CancellationToken cancellationToken)
    {
        var actualDate = date ?? DateOnly.FromDateTime(DateTime.Today);
        return Ok(await _balance.GetSnapshotAsync(id, actualDate, cancellationToken));
    }

    [HttpPost]
    [Authorize(Roles = "Operator")]
    public async Task<ActionResult<AccountDetailsDto>> Open([FromBody] OpenAccountRequest request, CancellationToken cancellationToken) =>
        Ok(await _accounts.OpenAsync(request, cancellationToken));

    [HttpPut("{id:guid}/overdraft")]
    [Authorize(Roles = "Operator,Controller,Administrator")]
    public async Task<ActionResult<AccountDetailsDto>> SetOverdraft(Guid id, [FromBody] SetOverdraftRequest request, CancellationToken cancellationToken) =>
        Ok(await _accounts.SetOverdraftAsync(id, request.OverdraftLimit, cancellationToken));

    [HttpPost("{id:guid}/holds")]
    [Authorize(Roles = "Operator,Controller,Administrator")]
    public async Task<ActionResult<HoldDto>> AddHold(Guid id, [FromBody] CreateHoldRequest request, CancellationToken cancellationToken) =>
        Ok(await _holds.AddAsync(id, request, cancellationToken));

    [HttpGet("{id:guid}/holds")]
    public async Task<ActionResult<IReadOnlyList<HoldDto>>> ListHolds(Guid id, CancellationToken cancellationToken) =>
        Ok(await _holds.ListActiveAsync(id, cancellationToken));

    [HttpPost("holds/{holdId:guid}/release")]
    [Authorize(Roles = "Operator,Controller,Administrator")]
    public async Task<ActionResult<HoldDto>> ReleaseHold(Guid holdId, CancellationToken cancellationToken) =>
        Ok(await _holds.ReleaseAsync(holdId, cancellationToken));

    [HttpPost("{id:guid}/close")]
    [Authorize(Roles = "Operator,Controller,Administrator")]
    public async Task<ActionResult<AccountDetailsDto>> Close(Guid id, CancellationToken cancellationToken) =>
        Ok(await _accounts.CloseAsync(id, cancellationToken));
}
