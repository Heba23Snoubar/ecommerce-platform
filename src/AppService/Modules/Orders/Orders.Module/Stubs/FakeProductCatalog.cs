using Orders.Module.Ports;

namespace Orders.Module.Stubs;

internal class FakeProductCatalog : IProductCatalog
{
    private static readonly Dictionary<Guid, ProductInfo> Items = new()
    {
        [Guid.Parse("00000000-0000-0000-0000-000000000001")] =
            new ProductInfo(Guid.Parse("00000000-0000-0000-0000-000000000001"), "Laptop", 1200m),
        [Guid.Parse("00000000-0000-0000-0000-000000000002")] =
            new ProductInfo(Guid.Parse("00000000-0000-0000-0000-000000000002"), "Mouse", 25m),
    };

    public Task<IReadOnlyList<ProductInfo>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<ProductInfo>>(
            ids.Where(Items.ContainsKey).Select(i => Items[i]).ToList());
}