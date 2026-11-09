using Asp.Versioning;
using Catalog.Module.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Products.Module.Application.DTOs.Cart;
using Shared.Errors;
using System.Security.Claims;

namespace Carts.Module.Endpoints.Controller
{
     
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/cart")]
    [Authorize]
    public class CartController : ControllerBase
    {
        private readonly ICartService _cartService;

        public CartController(ICartService cartService)
        {
            _cartService = cartService;
        }

        [HttpGet]
        public async Task<ActionResult<CartResponse>> GetCart(CancellationToken ct)
        {
            var result = await _cartService.GetCartAsync(CurrentUserId, ct);
            return Ok(result);
        }

        [HttpPost("items")]
        public async Task<ActionResult<CartResponse>> AddItem(
            [FromBody] AddItemRequest request,
            CancellationToken ct)
        {
            var result = await _cartService.AddItemAsync(CurrentUserId, request, ct);
            return Ok(result);
        }

        [HttpPatch("items/{productId:guid}/increase")]
        public async Task<ActionResult<CartResponse>> IncreaseQuantity(
            Guid productId,
            [FromBody] ChangeQuantityRequest request,
            CancellationToken ct)
        {
            var result = await _cartService.IncreaseItemQuantityAsync(CurrentUserId, productId, request.Amount, ct);
            return Ok(result);
        }

        [HttpPatch("items/{productId:guid}/decrease")]
        public async Task<ActionResult<CartResponse>> DecreaseQuantity(
            Guid productId,
            [FromBody] ChangeQuantityRequest request,
            CancellationToken ct)
        {
            var result = await _cartService.DecreaseItemQuantityAsync(CurrentUserId, productId, request.Amount, ct);
            return Ok(result);
        }

        [HttpDelete("items/{productId:guid}")]
        public async Task<ActionResult<CartResponse>> RemoveItem(
            Guid productId,
            CancellationToken ct)
        {
            var result = await _cartService.RemoveItemAsync(CurrentUserId, productId, ct);
            return Ok(result);
        }

        private Guid CurrentUserId
        {
            get
            {
                var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
                return Guid.TryParse(raw, out var id)
                    ? id
                    : throw new AppException(401, "INVALID_TOKEN", "User ID claim is missing or invalid.");
            }
        }
    }
}
