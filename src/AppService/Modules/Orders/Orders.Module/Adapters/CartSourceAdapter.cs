using Carts.Contract.Cart.Interfaces;
using Orders.Module.Ports;

namespace Orders.Module.Adapters;

internal class CartSourceAdapter : ICartSource
{
    private readonly ICartQueryService _carts;

    public CartSourceAdapter(ICartQueryService carts) => _carts = carts;

    public async Task<IReadOnlyList<CartLine>> GetItemsAsync(Guid userId, CancellationToken ct)
    {
        var cart = await _carts.GetCartByUserIdAsync(userId, ct);

        // الخدمة ترجع null عندما لا توجد سلة أو تكون فارغة
        if (cart is null)
            return Array.Empty<CartLine>();

        return cart.Items
            .Select(i => new CartLine(i.ProductId, i.Quantity))
            .ToList();
    }

    public Task ClearAsync(Guid userId, CancellationToken ct) => _carts.ClearCartAsync(userId, ct);
}
