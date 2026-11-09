package com.ecommerce.warehouse.entity;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.Id;
import jakarta.persistence.Table;

@Entity
@Table(name = "stocks")
public class Stock {

    @Id
    @Column(name = "product_id", length = 64)
    private String productId;

    @Column(nullable = false)
    private int quantity;

    protected Stock() {
        // مطلوب لـ JPA
    }

    public Stock(String productId, int quantity) {
        this.productId = productId;
        this.quantity = quantity;
    }

    public String getProductId() {
        return productId;
    }

    public int getQuantity() {
        return quantity;
    }
}
