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
}
