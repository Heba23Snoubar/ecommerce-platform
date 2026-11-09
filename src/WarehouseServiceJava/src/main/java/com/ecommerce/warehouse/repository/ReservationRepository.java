package com.ecommerce.warehouse.repository;

import com.ecommerce.warehouse.entity.Reservation;
import org.springframework.data.jpa.repository.EntityGraph;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Modifying;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import java.util.Optional;

public interface ReservationRepository extends JpaRepository<Reservation, String> {

    /** نجلب العناصر مع الحجز بنفس الاستعلام لتجنب مشكلة N+1 (والمتطلب الثالث يسأل عنها). */
    @EntityGraph(attributePaths = "items")
    Optional<Reservation> findWithItemsByReservationId(String reservationId);

    /**
     * "استلام" الحجز بشكل ذري: يرجع 1 لطلب واحد فقط، حتى لو وصل طلبا Release متزامنان.
     * يرجع 0 إذا كان الحجز غير موجود أو مُلغى سابقاً.
     */
    @Modifying
    @Query("update Reservation r set r.released = true where r.reservationId = :id and r.released = false")
    int markReleased(@Param("id") String reservationId);
}
