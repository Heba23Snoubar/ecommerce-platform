-- المخزون: صف واحد لكل منتج
CREATE TABLE stocks (
    product_id VARCHAR(64) PRIMARY KEY,
    quantity   INTEGER     NOT NULL CHECK (quantity >= 0)   -- شبكة أمان: لا يمكن أن يصبح المخزون سالباً أبداً
);

-- الحجوزات (لضمان الـ Idempotency وإمكانية الـ Release)
CREATE TABLE reservations (
    reservation_id VARCHAR(64) PRIMARY KEY,
    created_at     TIMESTAMPTZ NOT NULL,
    is_released    BOOLEAN     NOT NULL DEFAULT FALSE
);

-- عناصر كل حجز
CREATE TABLE reservation_items (
    id             BIGSERIAL   PRIMARY KEY,
    reservation_id VARCHAR(64) NOT NULL REFERENCES reservations (reservation_id) ON DELETE CASCADE,
    product_id     VARCHAR(64) NOT NULL,
    quantity       INTEGER     NOT NULL CHECK (quantity > 0)
);

CREATE INDEX idx_reservation_items_reservation_id ON reservation_items (reservation_id);
