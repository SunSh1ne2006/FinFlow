using FinFlow.Application.Abstractions;
using FinFlow.Application.Common;
using FinFlow.Application.Mapping;
using FinFlow.Contracts;
using FinFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Application.Services;

/// <summary>
/// Справочник продуктов банка (вклады и текущие счета). Условия хранятся версиями,
/// договор фиксирует версию, действовавшую на дату открытия.
/// </summary>
public class ProductService
{
    private readonly IBankDbContext _db;
    private readonly IClock _clock;

    public ProductService(IBankDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<IReadOnlyList<ProductDto>> ListAsync(string kind, CancellationToken cancellationToken = default)
    {
        var query = _db.Products.Include(p => p.Versions).AsQueryable();
        if (!string.IsNullOrWhiteSpace(kind))
        {
            var parsed = Parsing.Enum<ProductKind>(kind, "вид продукта");
            query = query.Where(p => p.Kind == parsed);
        }

        var products = await query.OrderBy(p => p.Code).ToListAsync(cancellationToken);
        return products.Select(p => p.ToDto(_clock.Today)).ToList();
    }

    public async Task<ProductDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _db.Products
            .Include(p => p.Versions)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product == null)
        {
            throw new NotFoundException("Продукт не найден.");
        }

        return product.ToDto(_clock.Today);
    }
}
