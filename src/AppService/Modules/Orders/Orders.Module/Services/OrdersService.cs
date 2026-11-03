using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orders.Contracts;
using Orders.Module.Data;
using Orders.Module.Ports;
using Shared.Errors;

namespace Orders.Module.Services;

internal class OrdersService : IOrdersApi
{
    private const int MaxLines = 50;
    private const int MaxQuantityPerLine = 1000;

    private static readonly Dictionary<OrderStatus, OrderStatus[]> Transitions = new()
    {
        [OrderStatus.Pending]   = new[] { OrderStatus.Confirmed, OrderStatus.Cancelled },
        [OrderStatus.Confirmed] = new[] { OrderStatus.Shipped, OrderStatus.Cancelled },
        [OrderStatus.Shipped]   = new[] { OrderStatus.Delivered },
        [OrderStatus.Delivered] = Array.Empty<OrderStatus>(),
        [OrderStatus.Cancelled] = Array.Empty<OrderStatus>(),
    };

    private readonly OrdersDbContext _db;
    private readonly IProductCatalog _catalog;
    private readonly IStockReservation _stock;
    private readonly ILogger<OrdersService> _log;

    public OrdersService(OrdersDbContext db, IProductCatalog catalog,
        IStockReservation stock, ILogger<OrdersService> log)
    {
        _db = db;
        _catalog = catalog;
        _stock = stock;
        _log = log;
    }

    public async Task<OrderDto> CreateAsync(Guid userId, CreateOrderRequest request, CancellationToken ct)
    {
        // 1) تحقق من المدخلات ودمج المنتجات المكررة
        var lines = Validate(request);

        // 2) تأكد أن المنتجات موجودة واحصل على الاسم والسعر
        var products = (await _catalog.GetByIdsAsync(lines.Select(l => l.ProductId), ct))
            .ToDictionary(p => p.Id);
        var missing = lines.Where(l => !products.ContainsKey(l.ProductId))
            .Select(l => l.ProductId).ToList();
        if (missing.Count > 0)
            throw new AppException(422, "product_not_found", "Some products do not exist.",
                new { productIds = missing });

        // 3) المعرّف يولَّد أولاً، وهو نفسه reservation_id في المستودع
        var orderId = Guid.NewGuid();

        // 4) حجز الكمية (كلها أو لا شيء)
        var reserve = await _stock.ReserveAsync(orderId,
            lines.Select(l => (l.ProductId, l.Quantity)).ToList(), ct);

        if (reserve.Outcome == ReserveOutcome.InsufficientStock)
            throw new AppException(409, "insufficient_stock",
                reserve.Message ?? "Insufficient stock.", reserve.Details);
        if (reserve.Outcome == ReserveOutcome.ProductNotFound)
            throw new AppException(422, "product_not_found",
                reserve.Message ?? "Product not found in warehouse.", reserve.Details);

        // 5) حفظ الطلب، وعند الفشل نُرجع الكمية المحجوزة
        var order = new Order { Id = orderId, UserId = userId, Status = OrderStatus.Pending };
        foreach (var l in lines)
        {
            var p = products[l.ProductId];
            order.Items.Add(new OrderItem
            {
                Id = Guid.NewGuid(),
                OrderId = orderId,
                ProductId = p.Id,
                ProductName = p.Name,
                UnitPrice = p.Price,   // نحفظ سعر لحظة الطلب
                Quantity = l.Quantity
            });
        }
        order.Total = order.Items.Sum(i => i.UnitPrice * i.Quantity);

        _db.Orders.Add(order);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Saving order {OrderId} failed; releasing reservation", orderId);
            await SafeReleaseAsync(orderId);
            throw;
        }

        return ToDto(order);
    }

    public async Task<OrderDto> GetAsync(Guid id, Guid requesterId, bool isAdmin, CancellationToken ct)
    {
        // منع IDOR: الشرط داخل الاستعلام نفسه
        var order = await _db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id && (isAdmin || o.UserId == requesterId), ct);

        if (order is null)
            throw new AppException(404, "order_not_found", "Order not found.");

        return ToDto(order);
    }

    public async Task<OrderPageDto> ListAsync(Guid requesterId, bool isAdmin, OrderStatus? status,
        int page, int pageSize, CancellationToken ct)
    {
        if (page < 1) throw Invalid("page must be 1 or greater.");
        if (pageSize < 1 || pageSize > 50) throw Invalid("pageSize must be between 1 and 50.");

        // الزبون يرى طلباته فقط: الشرط داخل الاستعلام، لا بعد جلب البيانات
        var query = isAdmin
            ? _db.Orders.AsNoTracking()
            : _db.Orders.AsNoTracking().Where(o => o.UserId == requesterId);

        if (status.HasValue)
        {
            var wanted = status.Value;
            query = query.Where(o => o.Status == wanted);
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(o => o.CreatedAt).ThenBy(o => o.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(o => new OrderSummaryDto(o.Id, o.UserId, o.Status, o.Total, o.CreatedAt, o.Items.Count))
            .ToListAsync(ct);

        return new OrderPageDto(items, page, pageSize, total);
    }

    public async Task<OrderDto> UpdateStatusAsync(Guid id, OrderStatus newStatus, CancellationToken ct)
    {
        var current = await _db.Orders.AsNoTracking()
            .Where(o => o.Id == id)
            .Select(o => (OrderStatus?)o.Status)
            .FirstOrDefaultAsync(ct);

        if (current is null)
            throw new AppException(404, "order_not_found", "Order not found.");

        var oldStatus = current.Value;
        var allowed = Transitions[oldStatus];
        if (!allowed.Contains(newStatus))
            throw new AppException(422, "invalid_status_transition",
                $"Cannot change status from {oldStatus} to {newStatus}.",
                new
                {
                    current = oldStatus.ToString(),
                    requested = newStatus.ToString(),
                    allowed = allowed.Select(a => a.ToString()).ToList()
                });

        // تحديث ذرّي مشروط بالحالة القديمة: يمنع تعارض مسؤولَين يغيّران الحالة معاً
        var affected = await _db.Orders
            .Where(o => o.Id == id && o.Status == oldStatus)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, newStatus), ct);

        if (affected == 0)
            throw new AppException(409, "status_conflict",
                "The order status was changed by someone else. Reload and try again.");

        if (newStatus == OrderStatus.Cancelled)
            await SafeReleaseAsync(id);   // إرجاع الكمية المحجوزة

        return await GetAsync(id, Guid.Empty, true, ct);
    }

    private async Task SafeReleaseAsync(Guid orderId)
    {
        try { await _stock.ReleaseAsync(orderId, CancellationToken.None); }
        catch (Exception ex) { _log.LogError(ex, "Release failed for reservation {OrderId}", orderId); }
    }

    private static List<OrderItemRequest> Validate(CreateOrderRequest? request)
    {
        if (request?.Items is null || request.Items.Count == 0)
            throw Invalid("Order must contain at least one item.");
        if (request.Items.Any(i => i.ProductId == Guid.Empty))
            throw Invalid("productId is required for every item.");
        if (request.Items.Any(i => i.Quantity <= 0 || i.Quantity > MaxQuantityPerLine))
            throw Invalid($"quantity must be between 1 and {MaxQuantityPerLine}.");

        var merged = request.Items.GroupBy(i => i.ProductId)
            .Select(g => new OrderItemRequest(g.Key, g.Sum(x => x.Quantity))).ToList();

        if (merged.Count > MaxLines)
            throw Invalid($"An order can contain at most {MaxLines} different products.");
        if (merged.Any(m => m.Quantity > MaxQuantityPerLine))
            throw Invalid($"Total quantity per product must not exceed {MaxQuantityPerLine}.");

        return merged;
    }

    private static AppException Invalid(string message) => new(422, "validation_failed", message);

    private static OrderDto ToDto(Order o) => new(o.Id, o.UserId, o.Status, o.Total, o.CreatedAt,
        o.Items.Select(i => new OrderItemDto(i.ProductId, i.ProductName, i.UnitPrice, i.Quantity)).ToList());
}