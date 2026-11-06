using Orders.Module.Ports;
using Products.Contract.Product.Interfaces;

namespace Orders.Module.Adapters;

internal class ProductCatalogAdapter : IProductCatalog
{
    private readonly IProductQueryService _products;

    public ProductCatalogAdapter(IProductQueryService products) => _products = products;

    public async Task<IReadOnlyList<ProductInfo>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var wanted = ids.Distinct().ToList();
        var found = await _products.GetByIdsAsync(wanted, ct);

        // المنتج غير المتاح للبيع يُعامل كأنه غير موجود
        return found
            .Where(p => p.IsAvailable)
            .Select(p => new ProductInfo(p.Id, p.Name, p.Price))
            .ToList();
    }
}