-- =========================================
-- Create database roles for each module
-- =========================================
--كلمات المرور هون لازم تكون نفسا بenv

CREATE ROLE users_role LOGIN PASSWORD 'users_pw';
CREATE ROLE products_role LOGIN PASSWORD 'products_pw';
CREATE ROLE orders_role LOGIN PASSWORD 'orders_pw';
CREATE ROLE notifications_role LOGIN PASSWORD 'notifications_pw';


-- =========================================
-- Allow module roles to connect to appdb
-- =========================================

GRANT CONNECT ON DATABASE appdb TO
    users_role,
    products_role,
    orders_role,
    notifications_role;


-- =========================================
-- Create isolated schemas
-- =========================================

CREATE SCHEMA users AUTHORIZATION users_role;
CREATE SCHEMA products AUTHORIZATION products_role;
CREATE SCHEMA orders AUTHORIZATION orders_role;
CREATE SCHEMA notifications AUTHORIZATION notifications_role;


-- =========================================
-- Restrict access to the default public schema
-- =========================================

REVOKE ALL ON SCHEMA public FROM PUBLIC;