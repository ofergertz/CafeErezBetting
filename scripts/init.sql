-- Initial DB setup script (runs on first Postgres container start)
-- Actual schema is managed by EF Core migrations

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pg_trgm";  -- for full-text search on customer names
