using Catalog.Module.Application.Abstraction.Interfaces;
using Catalog.Module.Application.Abstraction.Repositories;
using Catalog.Module.Application.Services.Interfaces;
using Products.Module.Application.DTOs.Cart;
using Shared.Errors;


namespace Carts.Module.Application.Services.Implementations
{
     

     
    internal sealed class CartService : ICartService
    {
        private readonly ICartRepository _cartRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CartService(ICartRepository cartRepository, IUnitOfWork unitOfWork)
        {
            _cartRepository = cartRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<CartResponse> GetCartAsync(Guid userId, CancellationToken ct = default)
        {
            var cart = await _cartRepository.GetByUserIdAsync(userId, ct);

            if (cart is null)
            {
                cart = new E.Domain.Entites.Cart(userId);
                await _cartRepository.AddAsync(cart, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            }

            return MapToResponse(cart);
        }

        public async Task<CartResponse> AddItemAsync(
            Guid userId,
            AddItemRequest request,
            CancellationToken ct = default)
        {
            var cart = await _cartRepository.GetByUserIdAsync(userId, ct);

            if (cart is null)
            {
                cart = new E.Domain.Entites.Cart(userId);
                await _cartRepository.AddAsync(cart, ct);
            }

            try
            {
                cart.AddItem(request.ProductId, request.Quantity);
                await _unitOfWork.SaveChangesAsync(ct);
            }
            catch (ArgumentException ex)
            {
                throw new AppException(422, "VALIDATION_ERROR", ex.Message);
            }

            return MapToResponse(cart);
        }

        public async Task<CartResponse> IncreaseItemQuantityAsync(
            Guid userId,
            Guid productId,
            int amount,
            CancellationToken ct = default)
        {
            var cart = await GetExistingCartOrThrowAsync(userId, ct);

            try
            {
                cart.IncreaseItemQuantity(productId, amount);
                await _unitOfWork.SaveChangesAsync(ct);
            }
            catch (ArgumentException ex)
            {
                throw new AppException(422, "VALIDATION_ERROR", ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                throw new AppException(404, "CART_ITEM_NOT_FOUND", ex.Message);
            }

            return MapToResponse(cart);
        }

        public async Task<CartResponse> DecreaseItemQuantityAsync(
            Guid userId,
            Guid productId,
            int amount,
            CancellationToken ct = default)
        {
            var cart = await GetExistingCartOrThrowAsync(userId, ct);

            try
            {
                cart.DecreaseItemQuantity(productId, amount);
                await _unitOfWork.SaveChangesAsync(ct);
            }
            catch (ArgumentException ex)
            {
                throw new AppException(422, "VALIDATION_ERROR", ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                // يشمل حالة عدم وجود المنتج أو محاولة إنقاص الكمية إلى صفر أو أقل
                throw new AppException(400, "INVALID_OPERATION", ex.Message);
            }

            return MapToResponse(cart);
        }

        public async Task<CartResponse> RemoveItemAsync(
            Guid userId,
            Guid productId,
            CancellationToken ct = default)
        {
            var cart = await GetExistingCartOrThrowAsync(userId, ct);

            try
            {
                cart.RemoveItem(productId);
                await _unitOfWork.SaveChangesAsync(ct);
            }
            catch (ArgumentException ex)
            {
                throw new AppException(422, "VALIDATION_ERROR", ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                throw new AppException(404, "CART_ITEM_NOT_FOUND", ex.Message);
            }

            return MapToResponse(cart);
        }

        private async Task<E.Domain.Entites.Cart> GetExistingCartOrThrowAsync(Guid userId, CancellationToken ct)
        {
            var cart = await _cartRepository.GetByUserIdAsync(userId, ct);

            if (cart is null)
            {
                throw new AppException(404, "CART_NOT_FOUND", "Cart not found for this user.");
            }

            return cart;
        }

        private static CartResponse MapToResponse(E.Domain.Entites.Cart cart)
        {
            var items = cart.Items
                .Select(i => new CartItemResponse(i.Id, i.ProductId, i.Quantity))
                .ToList();

            return new CartResponse(
                cart.Id,
                cart.UserId,
                items,
                items.Sum(i => i.Quantity)
            );
        }
    }
}
