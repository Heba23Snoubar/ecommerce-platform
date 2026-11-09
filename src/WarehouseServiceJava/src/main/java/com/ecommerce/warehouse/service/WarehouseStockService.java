package com.ecommerce.warehouse.service;

import com.ecommerce.warehouse.entity.Reservation;
import com.ecommerce.warehouse.entity.ReservationItem;
import com.ecommerce.warehouse.repository.ReservationRepository;
import com.ecommerce.warehouse.repository.StockRepository;
import org.springframework.dao.DataIntegrityViolationException;
import org.springframework.stereotype.Service;
import org.springframework.transaction.PlatformTransactionManager;
import org.springframework.transaction.TransactionStatus;
import org.springframework.transaction.support.TransactionTemplate;

import java.util.ArrayList;
import java.util.Comparator;
import java.util.List;
import java.util.Map;
import java.util.Objects;
import java.util.Optional;
import java.util.TreeMap;

/**
 * منطق المستودع. نستعمل TransactionTemplate بدل @Transactional لسببين:
 * 1) نحتاج setRollbackOnly() ثم إرجاع نتيجة عادية (INSUFFICIENT_STOCK) بدون رمي استثناء.
 * 2) حدود الـ Transaction واضحة في الكود: لا يُرسَل أي رد لعميل gRPC إلا بعد الـ commit.
 */
@Service
public class WarehouseStockService {

    private final StockRepository stocks;
    private final ReservationRepository reservations;
    private final TransactionTemplate tx;

    public WarehouseStockService(StockRepository stocks,
                                 ReservationRepository reservations,
                                 PlatformTransactionManager transactionManager) {
        this.stocks = stocks;
        this.reservations = reservations;
        this.tx = new TransactionTemplate(transactionManager);
    }

    // ------------------------------------------------------------------ GetStock

    public int getStock(String productId) {
        // منتج غير موجود => 0 (كما في نسخة C#)
        return stocks.findQuantity(productId).orElse(0);
    }

    // ------------------------------------------------------------------ SetStock

    public void setStock(String productId, int quantity) {
        if (productId == null || productId.isBlank()) {
            throw new IllegalArgumentException("ProductId is required.");
        }
        if (quantity < 0) {
            throw new IllegalArgumentException("Quantity must not be negative.");
        }
        tx.executeWithoutResult(status -> stocks.upsert(productId, quantity));
    }

    // ------------------------------------------------------------------ Reserve

    public ReserveResult reserve(String reservationId, List<ReserveItemCommand> rawItems) {
        if (reservationId == null || reservationId.isBlank() || rawItems.isEmpty()) {
            throw new IllegalArgumentException("ReservationId and Items are required.");
        }

        // دمج العناصر المكررة لنفس المنتج + ترتيبها حسب ProductId (منع الـ Deadlock).
        // TreeMap يرتّب المفاتيح تلقائياً، وكل الطلبات تقفل الصفوف بنفس الترتيب.
        Map<String, Integer> items = new TreeMap<>();
        for (ReserveItemCommand item : rawItems) {
            if (item.productId() == null || item.productId().isBlank() || item.quantity() <= 0) {
                throw new IllegalArgumentException("Each item requires a product_id and a positive quantity.");
            }
            items.merge(item.productId(), item.quantity(), Math::addExact);
        }

        // 1. فحص الـ Idempotency: الحجز مسجّل مسبقاً => RESERVED فوراً
        if (reservations.existsById(reservationId)) {
            return ReserveResult.reserved();
        }

        try {
            return Objects.requireNonNull(tx.execute(status -> doReserve(status, reservationId, items)));
        } catch (DataIntegrityViolationException e) {
            // طلب متزامن بنفس reservation_id سبقنا. تراجعت معاملتنا كلها (فلا خصم مزدوج)،
            // والطلب الآخر نجح، فالنتيجة الصحيحة RESERVED.
            if (reservations.existsById(reservationId)) {
                return ReserveResult.reserved();
            }
            throw e;
        }
    }

    private ReserveResult doReserve(TransactionStatus status, String reservationId, Map<String, Integer> items) {
        List<ShortageInfo> shortages = new ArrayList<>();

        for (Map.Entry<String, Integer> entry : items.entrySet()) {
            String productId = entry.getKey();
            int requested = entry.getValue();

            Optional<Integer> current = stocks.findQuantity(productId);
            if (current.isEmpty()) {
                status.setRollbackOnly();
                return ReserveResult.productNotFound(new ShortageInfo(productId, requested, 0));
            }

            // الخصم الذري: يرجع 0 إذا لم تكفِ الكمية
            if (stocks.decrementIfAvailable(productId, requested) == 0) {
                shortages.add(new ShortageInfo(productId, requested, current.get()));
            }
        }

        if (!shortages.isEmpty()) {
            status.setRollbackOnly(); // يُلغي كل الخصومات التي نجحت قبل أول نقص
            return ReserveResult.insufficient(shortages);
        }

        // تسجيل الحجز (للـ Idempotency وإمكانية الـ Release)
        Reservation reservation = new Reservation(reservationId);
        items.forEach(reservation::addItem);
        reservations.saveAndFlush(reservation); // flush هنا ليظهر خطأ تكرار المفتاح داخل الـ Transaction
        return ReserveResult.reserved();
    }

    // ------------------------------------------------------------------ Release

    public void release(String reservationId) {
        if (reservationId == null || reservationId.isBlank()) {
            throw new IllegalArgumentException("ReservationId is required.");
        }

        tx.executeWithoutResult(status -> {
            // "استلام" ذري للحجز: طلب واحد فقط يفوز، فلا تُرجَع الكميات مرتين أبداً.
            // غير موجود أو مُلغى سابقاً => لا شيء نفعله (Idempotent) والرد released = true
            if (reservations.markReleased(reservationId) == 0) {
                return;
            }

            Reservation reservation = reservations.findWithItemsByReservationId(reservationId).orElseThrow();
            reservation.getItems().stream()
                    .sorted(Comparator.comparing(ReservationItem::getProductId)) // نفس ترتيب الـ Reserve
                    .forEach(item -> stocks.increment(item.getProductId(), item.getQuantity()));
        });
    }
}
