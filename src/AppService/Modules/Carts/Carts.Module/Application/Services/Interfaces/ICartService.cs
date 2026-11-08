
using Products.Module.Application.DTOs.Cart;




namespace Catalog.Module.Application.Services.Interfaces
{



     
    public interface ICartService
    {
        Task<CartResponse> GetCartAsync(Guid userId, CancellationToken ct = default);
        Task<CartResponse> AddItemAsync(Guid userId, AddItemRequest request, CancellationToken ct = default);
        Task<CartResponse> IncreaseItemQuantityAsync(Guid userId, Guid productId, int amount, CancellationToken ct = default);
        Task<CartResponse> DecreaseItemQuantityAsync(Guid userId, Guid productId, int amount, CancellationToken ct = default);
        Task<CartResponse> RemoveItemAsync(Guid userId, Guid productId, CancellationToken ct = default);
    }
}
