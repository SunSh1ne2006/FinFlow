using FinFlow.Application.Abstractions;
using FinFlow.Application.Common;
using FinFlow.Application.Mapping;
using FinFlow.Contracts;
using FinFlow.Domain.Entities;
using FinFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Application.Services;

/// <summary>
/// Продуктовый конструктор вкладов и счетов (требование 2): версии условий,
/// утверждение с контролем РВСР, неизменяемость утверждённых версий.
/// </summary>
public class ProductService
{
    private readonly IBankDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditWriter _audit;
    private readonly ICurrentUser _current;
    private readonly ParameterService _parameters;

    public ProductService(IBankDbContext db, IClock clock, IAuditWriter audit, ICurrentUser current, ParameterService parameters)
    {
        _db = db;
        _clock = clock;
        _audit = audit;
        _current = current;
        _parameters = parameters;
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

    public async Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
        {
            throw new AppException("Укажите код и наименование продукта.");
        }

        var code = request.Code.Trim().ToUpperInvariant();
        if (await _db.Products.AnyAsync(p => p.Code == code, cancellationToken))
        {
            throw new ConflictException($"Продукт с кодом {code} уже существует.");
        }

        var kind = Parsing.Enum<ProductKind>(request.Kind, "вид продукта", ProductKind.Deposit);
        var product = new Product
        {
            Code = code,
            Name = request.Name.Trim(),
            Kind = kind,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "BYN" : request.Currency.Trim().ToUpperInvariant(),
            BalanceAccount = string.IsNullOrWhiteSpace(request.BalanceAccount)
                ? (kind == ProductKind.Deposit ? "3014" : "3012")
                : request.BalanceAccount.Trim(),
            Description = request.Description,
            IsActive = true,
            CreatedBy = _current.UserId
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("ProductCreated", nameof(Product), product.Id.ToString(), null,
            new { product.Code, product.Name, Kind = kind.ToString() }, cancellationToken);

        return await GetAsync(product.Id, cancellationToken);
    }

    public async Task<ProductVersionDto> AddVersionAsync(Guid productId, CreateProductVersionRequest request, CancellationToken cancellationToken = default)
    {
        var product = await _db.Products
            .Include(p => p.Versions)
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);

        if (product == null)
        {
            throw new NotFoundException("Продукт не найден.");
        }

        if (request == null)
        {
            throw new AppException("Не переданы условия новой версии.");
        }

        if (request.RatePercent < 0m || request.RatePercent > 100m)
        {
            throw new AppException("Процентная ставка должна находиться в диапазоне от 0 до 100 %.");
        }

        if (request.MinBalance < 0m || request.MinAmount < 0m)
        {
            throw new AppException("Минимальная сумма и неснижаемый остаток не могут быть отрицательными.");
        }

        var version = new ProductVersion
        {
            ProductId = product.Id,
            Version = (product.Versions?.Count ?? 0) + 1,
            RatePercent = request.RatePercent,
            RateType = Parsing.Enum<RateType>(request.RateType, "тип ставки", RateType.Fixed),
            SpreadPercent = request.SpreadPercent,
            DepositType = Parsing.Enum<DepositType>(request.DepositType, "вид вклада", DepositType.None),
            IsRevocable = request.IsRevocable,
            AllowsProlongation = request.AllowsProlongation,
            TermMonths = request.TermMonths < 0 ? 0 : request.TermMonths,
            Capitalization = Parsing.Enum<CapitalizationMode>(request.Capitalization, "капитализация", CapitalizationMode.None),
            InterestPayout = Parsing.Enum<InterestPayoutMode>(request.InterestPayout, "порядок выплаты процентов", InterestPayoutMode.AtEnd),
            MinAmount = request.MinAmount,
            MinBalance = request.MinBalance,
            DayBase = request.DayBase is 365 or 366 ? request.DayBase : 0,
            ValidFrom = request.ValidFrom ?? _clock.Today,
            IsApproved = false,
            Notes = request.Notes,
            CreatedBy = _current.UserId
        };

        _db.ProductVersions.Add(version);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("ProductVersionCreated", nameof(ProductVersion), version.Id.ToString(), null,
            new { version.Version, version.RatePercent, version.ValidFrom }, cancellationToken);

        return version.ToDto(_clock.Today);
    }

    /// <summary>Утверждение версии условий. Ставка выше РВСР требует отдельного решения КУАП (риск R-08).</summary>
    public async Task<ProductVersionDto> ApproveVersionAsync(Guid versionId, bool force, CancellationToken cancellationToken = default)
    {
        var version = await _db.ProductVersions
            .Include(v => v.Product)
            .FirstOrDefaultAsync(v => v.Id == versionId, cancellationToken);

        if (version == null)
        {
            throw new NotFoundException("Версия условий не найдена.");
        }

        if (version.IsApproved)
        {
            throw new ConflictException("Версия условий уже утверждена: изменение создаёт новую версию (бизнес-правило 3).");
        }

        var check = await CheckRvsrAsync(version, cancellationToken);
        if (check.IsExceeded && !force)
        {
            throw new AppException(
                $"Ставка {version.RatePercent:0.00} % превышает расчётную величину стандартного риска {check.RvsrLimit:0.00} % " +
                "и не может быть утверждена без отдельного решения КУАП.", 409);
        }

        version.IsApproved = true;
        version.ApprovedBy = _current.UserId;
        version.ApprovedAt = _clock.UtcNow;
        version.RvsrLimit = check.RvsrLimit;

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("ProductVersionApproved", nameof(ProductVersion), version.Id.ToString(),
            new { Approved = false }, new { Approved = true, Rvsr = check.RvsrLimit, Forced = force }, cancellationToken);

        return version.ToDto(_clock.Today);
    }

    public async Task<RvsrCheckDto> CheckRvsrAsync(Guid versionId, CancellationToken cancellationToken = default)
    {
        var version = await _db.ProductVersions
            .Include(v => v.Product)
            .FirstOrDefaultAsync(v => v.Id == versionId, cancellationToken);

        if (version == null)
        {
            throw new NotFoundException("Версия условий не найдена.");
        }

        return await CheckRvsrAsync(version, cancellationToken);
    }

    private async Task<RvsrCheckDto> CheckRvsrAsync(ProductVersion version, CancellationToken cancellationToken)
    {
        var currency = version.Product?.Currency ?? "BYN";
        var limit = 0m;

        if (string.Equals(currency, "BYN", StringComparison.OrdinalIgnoreCase))
        {
            limit = version.TermMonths <= 36
                ? await _parameters.GetValueAsync(ParameterService.RvsrByn1To3, _clock.Today, 12.17m, cancellationToken)
                : await _parameters.GetValueAsync(ParameterService.RvsrBynOver3, _clock.Today, 14.33m, cancellationToken);
        }

        var isExceeded = limit > 0m && version.RatePercent > limit;
        return new RvsrCheckDto
        {
            VersionId = version.Id,
            RatePercent = version.RatePercent,
            RvsrLimit = limit,
            IsExceeded = isExceeded,
            Message = limit <= 0m
                ? "Контроль РВСР для вкладов в иностранной валюте не применяется."
                : isExceeded
                    ? $"Ставка {version.RatePercent:0.00} % превышает РВСР {limit:0.00} % годовых."
                    : $"Ставка {version.RatePercent:0.00} % не превышает РВСР {limit:0.00} % годовых."
        };
    }
}
