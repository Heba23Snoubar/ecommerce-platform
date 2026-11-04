using Contracts.Warehouse;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Warehouse.Api.Data;
using Warehouse.Api.Entities;

namespace Warehouse.Api.Services;

public class WarehouseGrpcService : WarehouseService.WarehouseServiceBase
{
    private readonly WarehouseDbContext _db;
    private readonly ILogger<WarehouseGrpcService> _logger;

    public WarehouseGrpcService(WarehouseDbContext db, ILogger<WarehouseGrpcService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public override async Task<GetStockResponse> GetStock(GetStockRequest request, ServerCallContext context)
    {
        var stock = await _db.Stocks
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ProductId == request.ProductId, context.CancellationToken);

        return new GetStockResponse
        {
            ProductId = request.ProductId,
            Available = stock?.Quantity ?? 0
        };
    }

    public override async Task<SetStockResponse> SetStock(SetStockRequest request, ServerCallContext context)
    {
        var stock = await _db.Stocks.FirstOrDefaultAsync(s => s.ProductId == request.ProductId, context.CancellationToken);

        if (stock == null)
        {
            _db.Stocks.Add(new Stock
            {
                ProductId = request.ProductId,
                Quantity = request.Quantity
            });
        }
        else
        {
            stock.Quantity = request.Quantity;
        }

        await _db.SaveChangesAsync(context.CancellationToken);

        return new SetStockResponse { Ok = true };
    }

    public override async Task<ReserveResponse> Reserve(ReserveRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.ReservationId) || request.Items.Count == 0)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "ReservationId and Items are required."));
        }

        // 1. فحص الـ Idempotency: إذا كان الحجز مسجلاً مسبقاً نرجع RESERVED فوراً
        var existingReservation = await _db.Reservations
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.ReservationId == request.ReservationId, context.CancellationToken);

        if (existingReservation != null)
        {
            return new ReserveResponse { Status = ReserveStatus.Reserved };
        }

        // 2. منع الـ Deadlock: ترتيب العناصر حسب ProductId
        var orderedItems = request.Items.OrderBy(i => i.ProductId).ToList();

        // 3. فتح Transaction
        await using var transaction = await _db.Database.BeginTransactionAsync(context.CancellationToken);

        try
        {
            var shortages = new List<Shortage>();

            foreach (var item in orderedItems)
            {
                // تحقق أولاً هل المنتج موجود أساساً في النظام
                var currentStock = await _db.Stocks
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.ProductId == item.ProductId, context.CancellationToken);

                if (currentStock == null)
                {
                    await transaction.RollbackAsync(context.CancellationToken);
                    return new ReserveResponse
                    {
                        Status = ReserveStatus.ProductNotFound,
                        Shortages =
                        {
                            new Shortage
                            {
                                ProductId = item.ProductId,
                                Requested = item.Quantity,
                                Available = 0
                            }
                        }
                    };
                }

                // تنفيذ التحديث الذري لمنع Race Condition
                var rowsAffected = await _db.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE \"Stocks\" SET \"Quantity\" = \"Quantity\" - {item.Quantity} WHERE \"ProductId\" = {item.ProductId} AND \"Quantity\" >= {item.Quantity}",
                    context.CancellationToken);

                if (rowsAffected == 0)
                {
                    // الكمية غير كافية
                    shortages.Add(new Shortage
                    {
                        ProductId = item.ProductId,
                        Requested = item.Quantity,
                        Available = currentStock.Quantity
                    });
                }
            }

            if (shortages.Count > 0)
            {
                await transaction.RollbackAsync(context.CancellationToken);
                var response = new ReserveResponse { Status = ReserveStatus.InsufficientStock };
                response.Shortages.AddRange(shortages);
                return response;
            }

            // 4. تسجيل بيانات الحجز لضمان خاصية عدم التكرار (Idempotency) وإمكانية الـ Release
            var reservation = new Reservation
            {
                ReservationId = request.ReservationId,
                CreatedAt = DateTime.UtcNow,
                IsReleased = false,
                Items = orderedItems.Select(i => new ReservationItem
                {
                    ProductId = i.ProductId,
                    Quantity = i.Quantity
                }).ToList()
            };

            _db.Reservations.Add(reservation);
            await _db.SaveChangesAsync(context.CancellationToken);

            // 5. تثبيت المعاملة
            await transaction.CommitAsync(context.CancellationToken);

            return new ReserveResponse { Status = ReserveStatus.Reserved };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during reservation {ReservationId}", request.ReservationId);
            await transaction.RollbackAsync(context.CancellationToken);
            throw new RpcException(new Status(StatusCode.Internal, "Internal database error during stock reservation."));
        }
    }

    public override async Task<ReleaseResponse> Release(ReleaseRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.ReservationId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "ReservationId is required."));
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(context.CancellationToken);

        try
        {
            var reservation = await _db.Reservations
                .Include(r => r.Items)
                .FirstOrDefaultAsync(r => r.ReservationId == request.ReservationId, context.CancellationToken);

            // إذا لم يكن هناك حجز أو تم إلغاؤه مسبقاً، نرجع true بسلام (Idempotent)
            if (reservation == null || reservation.IsReleased)
            {
                return new ReleaseResponse { Released = true };
            }

            // إعادة الكميات المحجوزة للمخزون بشكل ذري
            foreach (var item in reservation.Items)
            {
                await _db.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE \"Stocks\" SET \"Quantity\" = \"Quantity\" + {item.Quantity} WHERE \"ProductId\" = {item.ProductId}",
                    context.CancellationToken);
            }

            reservation.IsReleased = true;
            await _db.SaveChangesAsync(context.CancellationToken);

            await transaction.CommitAsync(context.CancellationToken);

            return new ReleaseResponse { Released = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during release for reservation {ReservationId}", request.ReservationId);
            await transaction.RollbackAsync(context.CancellationToken);
            throw new RpcException(new Status(StatusCode.Internal, "Internal database error during stock release."));
        }
    }
}
