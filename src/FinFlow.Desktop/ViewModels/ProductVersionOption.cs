using System;

namespace FinFlow.Desktop.ViewModels;

/// <summary>Версия условий продукта для выбора при открытии счёта или вклада.</summary>
public class ProductVersionOption
{
    public Guid ProductId { get; init; }
    public Guid VersionId { get; init; }
    public string Kind { get; init; }
    public string Currency { get; init; }
    public decimal RatePercent { get; init; }
    public int TermMonths { get; init; }
    public decimal MinAmount { get; init; }
    public string Label { get; init; }

    public bool IsCurrentAccount => string.Equals(Kind, "CurrentAccount", StringComparison.OrdinalIgnoreCase);

    public static ProductVersionOption Create(FinFlow.Contracts.ProductDto product, FinFlow.Contracts.ProductVersionDto version)
    {
        var term = version.TermMonths > 0 ? $"{version.TermMonths} мес." : "без срока";
        return new ProductVersionOption
        {
            ProductId = product.Id,
            VersionId = version.Id,
            Kind = product.Kind,
            Currency = product.Currency,
            RatePercent = version.RatePercent,
            TermMonths = version.TermMonths,
            MinAmount = version.MinAmount,
            Label = $"{product.Name} · {version.RatePercent:0.00} % · {term} · {product.Currency}"
        };
    }
}
