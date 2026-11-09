package com.ecommerce.warehouse.entity;

import jakarta.persistence.CascadeType;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.Id;
import jakarta.persistence.OneToMany;
import jakarta.persistence.PostLoad;
import jakarta.persistence.PostPersist;
import jakarta.persistence.Table;
import jakarta.persistence.Transient;
import org.springframework.data.domain.Persistable;

import java.time.Instant;
import java.util.ArrayList;
import java.util.List;

/**
 * مفتاح الحجز (reservation_id) يأتي من العميل وليس مولَّداً عندنا.
 * لذلك ننفذ Persistable: بدونها يظن Spring Data أن الكيان "قديم" ويستعمل merge
 * (الذي يحدّث الصف الموجود بصمت) بدل persist (الذي يفشل عند تكرار المفتاح).
 * نحتاج persist الحقيقي لنكشف طلبين متزامنين بنفس reservation_id.
 */
@Entity
@Table(name = "reservations")
public class Reservation implements Persistable<String> {

    @Id
    @Column(name = "reservation_id", length = 64)
    private String reservationId;

    @Column(name = "created_at", nullable = false)
    private Instant createdAt = Instant.now();

    @Column(name = "is_released", nullable = false)
    private boolean released = false;

    @OneToMany(mappedBy = "reservation", cascade = CascadeType.ALL, orphanRemoval = true)
    private List<ReservationItem> items = new ArrayList<>();

    @Transient
    private boolean newEntity = true;

    protected Reservation() {
        // مطلوب لـ JPA
    }

    public Reservation(String reservationId) {
        this.reservationId = reservationId;
    }

    public void addItem(String productId, int quantity) {
        items.add(new ReservationItem(this, productId, quantity));
    }

    public void markReleased() {
        this.released = true;
    }

    @Override
    public String getId() {
        return reservationId;
    }

    @Override
    public boolean isNew() {
        return newEntity;
    }

    @PostLoad
    @PostPersist
    void markNotNew() {
        this.newEntity = false;
    }

    public String getReservationId() {
        return reservationId;
    }

    public Instant getCreatedAt() {
        return createdAt;
    }

    public boolean isReleased() {
        return released;
    }

    public List<ReservationItem> getItems() {
        return items;
    }
}
