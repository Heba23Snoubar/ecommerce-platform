package com.ecommerce.warehouse.repository;

import com.ecommerce.warehouse.entity.Stock;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Modifying;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import java.util.Optional;

public interface StockRepository extends JpaRepository<Stock, String> {

    /**
     * قراءة الكمية مباشرة من قاعدة البيانات (استعلام scalar، لا يمر بذاكرة JPA المؤقتة).
     * فارغ = المنتج غير موجود أصلاً، وهذا يختلف عن "موجود بكمية صفر".
     */
    @Query("select s.quantity from Stock s where s.productId = :productId")
    Optional<Integer> findQuantity(@Param("productId") String productId);

    /**
     * خصم ذري: الشرط quantity >= :qty داخل نفس جملة الـ UPDATE.
     * هذا ما يمنع الـ Race Condition (المتطلب الثاني): عند طلبين متزامنين على آخر قطعة
     * يقفل Postgres الصف، وينجح واحد فقط (يرجع 1) والثاني يرجع 0.
     */
    @Modifying
    @Query("update Stock s set s.quantity = s.quantity - :qty where s.productId = :productId and s.quantity >= :qty")
    int decrementIfAvailable(@Param("productId") String productId, @Param("qty") int qty);

    /** إرجاع كمية للمخزون (عند Release). */
    @Modifying
    @Query("update Stock s set s.quantity = s.quantity + :qty where s.productId = :productId")
    int increment(@Param("productId") String productId, @Param("qty") int qty);

    /** SetStock كـ Upsert ذري (أفضل من find ثم insert/update الذي كان في نسخة C#). */
    @Modifying
    @Query(value = """
            INSERT INTO stocks (product_id, quantity) VALUES (:productId, :quantity)
            ON CONFLICT (product_id) DO UPDATE SET quantity = EXCLUDED.quantity
            """, nativeQuery = true)
    int upsert(@Param("productId") String productId, @Param("quantity") int quantity);
}
