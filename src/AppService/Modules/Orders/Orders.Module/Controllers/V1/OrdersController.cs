using System.Reflection;
using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orders.Contracts;
using Shared.Errors;

namespace Orders.Module.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/orders")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IOrdersApi _orders;

    public OrdersController(IOrdersApi orders) => _orders = orders;

    [HttpGet("ping")]
    [AllowAnonymous]
    public IActionResult Ping() => Ok(new { module = "orders", status = "alive" });

    [HttpPost]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> Create(CreateOrderRequest request, CancellationToken ct)
    {
        var order = await _orders.CreateAsync(CurrentUserId, request, ct);
        return Created($"{Request.Path}/{order.Id}", order);
    }

    /// ينشئ طلباً من سلة المستخدم الحالي (بدون body) ثم يفرّغ السلة بعد نجاح الحفظ
    [HttpPost("checkout")]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> Checkout(CancellationToken ct)
    {
        var order = await _orders.CreateFromCartAsync(CurrentUserId, ct);

        // الرابط /api/v1/orders/{id}: نحذف آخر جزء "checkout" من المسار الحالي
        var path = Request.Path.Value!.TrimEnd('/');
        var ordersPath = path[..path.LastIndexOf('/')];
        return Created($"{ordersPath}/{order.Id}", order);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => Ok(await _orders.GetAsync(id, CurrentUserId, User.IsInRole("Admin"), ct));

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] OrderStatus? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
        => Ok(await _orders.ListAsync(CurrentUserId, User.IsInRole("Admin"), status, page, pageSize, ct));

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateStatus(Guid id, UpdateOrderStatusRequest request, CancellationToken ct)
        => Ok(await _orders.UpdateStatusAsync(id, request.Status, ct));

    private Guid CurrentUserId
    {
        get
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            return Guid.TryParse(raw, out var id)
                ? id
                : throw new AppException(401, "invalid_token", "User id claim is missing or invalid.");
        }
    }
}