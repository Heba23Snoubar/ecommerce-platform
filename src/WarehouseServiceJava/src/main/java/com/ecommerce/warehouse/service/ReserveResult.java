package com.ecommerce.warehouse.service;

import java.util.List;

public record ReserveResult(Status status, List<ShortageInfo> shortages) {

    public enum Status { RESERVED, INSUFFICIENT_STOCK, PRODUCT_NOT_FOUND }

    public static ReserveResult reserved() {
        return new ReserveResult(Status.RESERVED, List.of());
    }

    public static ReserveResult insufficient(List<ShortageInfo> shortages) {
        return new ReserveResult(Status.INSUFFICIENT_STOCK, List.copyOf(shortages));
    }

    public static ReserveResult productNotFound(ShortageInfo shortage) {
        return new ReserveResult(Status.PRODUCT_NOT_FOUND, List.of(shortage));
    }
}
