package com.ecommerce.warehouse.grpc;

import com.ecommerce.warehouse.service.ReserveItemCommand;
import com.ecommerce.warehouse.service.ReserveResult;
import com.ecommerce.warehouse.service.WarehouseStockService;
import io.grpc.Status;
import io.grpc.stub.StreamObserver;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.grpc.server.service.GrpcService;

import java.util.List;
import java.util.function.Supplier;

/**
 * طبقة gRPC رقيقة: تحوّل رسائل الـ proto إلى استدعاءات للخدمة وتحوّل النتيجة والأخطاء إلى gRPC.
 * يُرسَل الرد فقط بعد عودة الخدمة، أي بعد انتهاء الـ Transaction (commit أو rollback).
 */
@GrpcService
public class WarehouseGrpcService extends WarehouseServiceGrpc.WarehouseServiceImplBase {

    private static final Logger log = LoggerFactory.getLogger(WarehouseGrpcService.class);

    private final WarehouseStockService warehouse;

    public WarehouseGrpcService(WarehouseStockService warehouse) {
        this.warehouse = warehouse;
    }

    @Override
    public void getStock(GetStockRequest request, StreamObserver<GetStockResponse> observer) {
        handle(observer, "Internal error while reading stock.", () ->
                GetStockResponse.newBuilder()
                        .setProductId(request.getProductId())
                        .setAvailable(warehouse.getStock(request.getProductId()))
                        .build());
    }

    @Override
    public void setStock(SetStockRequest request, StreamObserver<SetStockResponse> observer) {
        handle(observer, "Internal error while setting stock.", () -> {
            warehouse.setStock(request.getProductId(), request.getQuantity());
            return SetStockResponse.newBuilder().setOk(true).build();
        });
    }

    @Override
    public void reserve(ReserveRequest request, StreamObserver<ReserveResponse> observer) {
        handle(observer, "Internal database error during stock reservation.", () -> {
            List<ReserveItemCommand> items = request.getItemsList().stream()
                    .map(i -> new ReserveItemCommand(i.getProductId(), i.getQuantity()))
                    .toList();

            ReserveResult result = warehouse.reserve(request.getReservationId(), items);

            ReserveResponse.Builder response = ReserveResponse.newBuilder()
                    .setStatus(switch (result.status()) {
                        case RESERVED -> ReserveStatus.RESERVED;
                        case INSUFFICIENT_STOCK -> ReserveStatus.INSUFFICIENT_STOCK;
                        case PRODUCT_NOT_FOUND -> ReserveStatus.PRODUCT_NOT_FOUND;
                    });
            result.shortages().forEach(s -> response.addShortages(
                    Shortage.newBuilder()
                            .setProductId(s.productId())
                            .setRequested(s.requested())
                            .setAvailable(s.available())
                            .build()));
            return response.build();
        });
    }

    @Override
    public void release(ReleaseRequest request, StreamObserver<ReleaseResponse> observer) {
        handle(observer, "Internal database error during stock release.", () -> {
            warehouse.release(request.getReservationId());
            return ReleaseResponse.newBuilder().setReleased(true).build();
        });
    }

    /** تنفيذ موحّد: نجاح => onNext + onCompleted، وأخطاء الإدخال => INVALID_ARGUMENT، وغيرها => INTERNAL. */
    private <T> void handle(StreamObserver<T> observer, String internalMessage, Supplier<T> action) {
        T response;
        try {
            response = action.get();
        } catch (IllegalArgumentException | ArithmeticException e) {
            observer.onError(Status.INVALID_ARGUMENT.withDescription(e.getMessage()).asRuntimeException());
            return;
        } catch (Exception e) {
            log.error(internalMessage, e);
            observer.onError(Status.INTERNAL.withDescription(internalMessage).asRuntimeException());
            return;
        }
        observer.onNext(response);
        observer.onCompleted();
    }
}
