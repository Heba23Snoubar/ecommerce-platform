-- =========================================
-- 1. Create database roles for each module
-- =========================================
CREATE ROLE users_role LOGIN PASSWORD 'users_pw';
CREATE ROLE products_role LOGIN PASSWORD 'products_pw';
CREATE ROLE orders_role LOGIN PASSWORD 'orders_pw';
CREATE ROLE notifications_role LOGIN PASSWORD 'notifications_pw';
CREATE ROLE cart_role LOGIN PASSWORD 'cart_pw'; 

-- =========================================
-- 2. Allow module roles to connect to appdb
-- =========================================
GRANT CONNECT ON DATABASE appdb TO
    users_role,
    products_role,
    orders_role,
    notifications_role,
    cart_role; -- <-- منح صلاحية الاتصال

-- =========================================
-- 3. Create isolated schemas
-- =========================================
CREATE SCHEMA users AUTHORIZATION users_role;
CREATE SCHEMA products AUTHORIZATION products_role;
CREATE SCHEMA orders AUTHORIZATION orders_role;
CREATE SCHEMA notifications AUTHORIZATION notifications_role;
CREATE SCHEMA cart AUTHORIZATION cart_role; -- <-- إنشاء الـ Schema وربط ملكيتها بدور السلة

-- =========================================
-- 4. Set default search_path for each role (اختياري لكنه ممتاز جداً)
-- =============================s============
ALTER ROLE users_role SET search_path TO users;
ALTER ROLE products_role SET search_path TO products;
ALTER ROLE orders_role SET search_path TO orders;
ALTER ROLE notifications_role SET search_path TO notifications;
ALTER ROLE cart_role SET search_path TO cart;

-- =========================================
-- 5. Restrict access to the default public schema
-- =========================================
REVOKE ALL ON SCHEMA public FROM PUBLIC;


-- =========================================
-- Setup Replication for Read Replica
-- =========================================

-- 1. إنشاء مستخدم النسخ (Replication User)
CREATE ROLE replicator WITH REPLICATION LOGIN PASSWORD 'replicator_pw';

-- 2. إضافة صلاحية اتصال حاوية الـ Replica داخل ملف pg_hba.conf تلقائياً
COPY (SELECT 'host replication replicator 0.0.0.0/0 scram-sha-256') 
TO PROGRAM 'cat >> /var/lib/postgresql/data/pg_hba.conf';

-- 3. إعادة تحميل إعدادات PostgreSQL لتفعيل السطر الجديد فوراً
SELECT pg_reload_conf();