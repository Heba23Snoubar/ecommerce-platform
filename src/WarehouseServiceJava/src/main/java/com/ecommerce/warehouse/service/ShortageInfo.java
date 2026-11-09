package com.ecommerce.warehouse.service;

public record ShortageInfo(String productId, int requested, int available) {
}
