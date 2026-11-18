-- =============================================================================
-- Extensions. Requires PostgreSQL 18+ (uuidv7), with PostGIS and pg_partman
-- installed at the OS level.
-- =============================================================================

-- companies.location and proximity search.
CREATE EXTENSION IF NOT EXISTS postgis;

-- EXCLUDE constraints that mix = on scalars with && on ranges.
CREATE EXTENSION IF NOT EXISTS btree_gist;

-- Case-insensitive email columns.
CREATE EXTENSION IF NOT EXISTS citext;

-- Trigram indexes on the ..._normalized search columns.
CREATE EXTENSION IF NOT EXISTS pg_trgm;

-- fold() calls public.unaccent by its full name.
CREATE EXTENSION IF NOT EXISTS unaccent SCHEMA public;

-- audit_logs partitions.
CREATE SCHEMA IF NOT EXISTS partman;
CREATE EXTENSION IF NOT EXISTS pg_partman SCHEMA partman;
