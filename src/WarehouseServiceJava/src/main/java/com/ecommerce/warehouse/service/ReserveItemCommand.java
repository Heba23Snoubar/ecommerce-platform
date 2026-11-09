package com.ecommerce.warehouse.service;

public record ReserveItemCommand(String productId, int quantity) {
}
