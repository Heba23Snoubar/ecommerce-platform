BEGIN;

SET search_path TO public;

CREATE TEMP TABLE seed_users AS
SELECT "Id" AS user_id
FROM "users"."users";

CREATE TEMP TABLE seed_products AS
SELECT
    "Id" AS product_id,
    "Name" AS product_name,
    "Price" AS product_price
FROM "products"."Products"
WHERE "IsAvailable" = true;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM seed_users) THEN
        RAISE EXCEPTION 'No users found. Seed users before orders.';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM seed_products) THEN
        RAISE EXCEPTION 'No available products found. Seed products before orders.';
    END IF;
END $$;

CREATE TEMP TABLE seed_order_ids (
    order_id uuid PRIMARY KEY,
    user_id uuid NOT NULL,
    status text NOT NULL,
    created_at timestamptz NOT NULL
);

INSERT INTO seed_order_ids (
    order_id,
    user_id,
    status,
    created_at
)
SELECT
    gen_random_uuid(),
    users.user_id,
    (ARRAY[
        'Pending',
        'Confirmed',
        'Shipped',
        'Delivered',
        'Cancelled'
    ])[1 + floor(random() * 5)::int],
    now() - (random() * interval '365 days')
FROM generate_series(1, 500000)
CROSS JOIN LATERAL (
    SELECT user_id
    FROM seed_users
    OFFSET floor(random() * (SELECT count(*) FROM seed_users))
    LIMIT 1
) users;

INSERT INTO "orders"."Orders" (
    "Id",
    "UserId",
    "Status",
    "Total",
    "CreatedAt"
)
SELECT
    order_id,
    user_id,
    status,
    0,
    created_at
FROM seed_order_ids;

INSERT INTO "orders"."OrderItems" (
    "Id",
    "OrderId",
    "ProductId",
    "ProductName",
    "UnitPrice",
    "Quantity"
)
SELECT
    gen_random_uuid(),
    orders.order_id,
    products.product_id,
    products.product_name,
    products.product_price,
    quantity
FROM seed_order_ids orders
CROSS JOIN LATERAL generate_series(
    1,
    1 + floor(random() * 3)::int
) item_number
CROSS JOIN LATERAL (
    SELECT product_id, product_name, product_price
    FROM seed_products
    OFFSET floor(random() * (SELECT count(*) FROM seed_products))
    LIMIT 1
) products
CROSS JOIN LATERAL (
    SELECT 1 + floor(random() * 5)::int AS quantity
) quantities;

UPDATE "orders"."Orders" orders
SET "Total" = totals.total
FROM (
    SELECT
        "OrderId",
        SUM("UnitPrice" * "Quantity") AS total
    FROM "orders"."OrderItems"
    GROUP BY "OrderId"
) totals
WHERE orders."Id" = totals."OrderId";

CREATE INDEX IF NOT EXISTS "IX_Orders_CreatedAt"
ON "orders"."Orders" ("CreatedAt" DESC);

CREATE INDEX IF NOT EXISTS "IX_Orders_UserId_CreatedAt"
ON "orders"."Orders" ("UserId", "CreatedAt" DESC);

ANALYZE "orders"."Orders";
ANALYZE "orders"."OrderItems";

COMMIT;