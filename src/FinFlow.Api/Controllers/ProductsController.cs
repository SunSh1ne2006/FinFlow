using FinFlow.Application.Services;
using FinFlow.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinFlow.Api.Controllers;

[ApiController]
[Route("api/products")]
[Authorize]
public class ProductsController : ControllerBase
{
    private readonly ProductService _products;

    public ProductsController(ProductService products)
    {
        _products = products;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProductDto>>> List([FromQuery] string kind, CancellationToken cancellationToken) =>
        Ok(await _products.ListAsync(kind, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductDto>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await _products.GetAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Roles = "Administrator,Head,Treasurer")]
    public async Task<ActionResult<ProductDto>> Create([FromBody] CreateProductRequest request, CancellationToken cancellationToken) =>
        Ok(await _products.CreateAsync(request, cancellationToken));

    [HttpPost("{id:guid}/versions")]
    [Authorize(Roles = "Administrator,Head,Treasurer")]
    public async Task<ActionResult<ProductVersionDto>> AddVersion(Guid id, [FromBody] CreateProductVersionRequest request, CancellationToken cancellationToken) =>
        Ok(await _products.AddVersionAsync(id, request, cancellationToken));

    /// <summary>Утверждение версии условий: ставка выше РВСР требует отдельного решения КУАП.</summary>
    [HttpPost("versions/{versionId:guid}/approve")]
    [Authorize(Roles = "Head")]
    public async Task<ActionResult<ProductVersionDto>> Approve(Guid versionId, [FromQuery] bool force, CancellationToken cancellationToken) =>
        Ok(await _products.ApproveVersionAsync(versionId, force, cancellationToken));

    [HttpGet("versions/{versionId:guid}/rvsr")]
    public async Task<ActionResult<RvsrCheckDto>> Rvsr(Guid versionId, CancellationToken cancellationToken) =>
        Ok(await _products.CheckRvsrAsync(versionId, cancellationToken));
}
