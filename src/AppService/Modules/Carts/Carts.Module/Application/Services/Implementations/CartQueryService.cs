using Carts.Contract.Cart.DTOs;
using Carts.Contract.Cart.Interfaces;
using Catalog.Module.Application.Abstraction.Interfaces;
using Catalog.Module.Application.Abstraction.Repositories;
 



namespace Carts.Module.Application.Services.Implementations
{
     
     
    internal sealed class CartQueryService : ICartQueryService
    {
        private readonly ICartRepository _cartRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CartQueryService(ICartRepository cartRepository, IUnitOfWork unitOfWork)
        {
            _cartRepository = cartRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<CartIntegrationDto?> GetCartByUserIdAsync(
            Guid userId,
            CancellationToken ct = default)
        {
            var cart = await _cartRepository.GetByUserIdAsync(userId, ct);

            if (cart is null || cart.Items.Count == 0)
            {
                return null;
            }

            var items = cart.Items
                .Select(i => new CartItemIntegrationDto(i.ProductId, i.Quantity))
                .ToList();

            return new CartIntegrationDto(cart.Id, cart.UserId, items);
        }

        public async Task ClearCartAsync(Guid userId, CancellationToken ct = default)
        {
            var cart = await _cartRepository.GetByUserIdAsync(userId, ct);

            if (cart is null || cart.Items.Count == 0)
            {
                return;
            }

            cart.Clear();
            await _unitOfWork.SaveChangesAsync(ct);
        }
    }
}
