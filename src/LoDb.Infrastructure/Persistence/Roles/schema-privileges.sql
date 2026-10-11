-- Owners and grants of the schema, applied by `migrate` after the migrations, in the
-- transaction that keeps the roles in place (DatabaseRoles.cs), as the superuser it
-- connects as. Every run derives the same state again, so the objects a new migration
-- creates get theirs at the end of that run.
--
--   lodb_owner       owns the objects of the schema. Nobody logs in as it.
--   lodb_partitions  owns analytics_event, its daily partitions and the two functions that
--                    create and drop them, the only DDL the API runs. Nobody logs in as it.
--   lodb_app         the API: reads and writes rows, runs those two functions, nothing else.
--
-- The superuser, as which the legacy stack still connects, keeps every right.

-- Only the owners create objects in the schema, and nobody gets temporary tables: a
-- database created before PostgreSQL 15 still let anyone create in `public`.
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
GRANT USAGE, CREATE ON SCHEMA public TO lodb_owner, lodb_partitions;
GRANT USAGE ON SCHEMA public TO lodb_app;

DO $database$
BEGIN
    EXECUTE format('REVOKE TEMPORARY ON DATABASE %I FROM PUBLIC', current_database());
    EXECUTE format('GRANT CONNECT ON DATABASE %I TO lodb_app', current_database());
END
$database$;

-- Owners: analytics_event and its partitions to lodb_partitions, everything else to
-- lodb_owner. An extension keeps its own objects; the sequence of an identity or serial
-- column follows its table, as do the indexes.
DO $owners$
DECLARE
    analytics regclass := to_regclass('public.analytics_event');
    item record;
BEGIN
    FOR item IN
        SELECT c.oid::regclass AS name,
            pg_get_userbyid(c.relowner) AS current_owner,
            CASE c.relkind
                WHEN 'S' THEN 'SEQUENCE'
                WHEN 'v' THEN 'VIEW'
                WHEN 'm' THEN 'MATERIALIZED VIEW'
                WHEN 'f' THEN 'FOREIGN TABLE'
                ELSE 'TABLE'
            END AS kind,
            CASE
                WHEN c.oid = analytics
                    OR c.oid IN (SELECT i.inhrelid FROM pg_inherits i WHERE i.inhparent = analytics)
                THEN 'lodb_partitions'
                ELSE 'lodb_owner'
            END AS owner
        FROM pg_class c
        WHERE c.relnamespace = 'public'::regnamespace
            AND c.relkind IN ('r', 'p', 'v', 'm', 'f', 'S')
            AND NOT EXISTS (
                SELECT FROM pg_depend d
                WHERE d.classid = 'pg_class'::regclass
                    AND d.objid = c.oid
                    AND (d.deptype = 'e' OR (c.relkind = 'S' AND d.deptype IN ('a', 'i'))))
    LOOP
        IF item.current_owner <> item.owner THEN
            EXECUTE format('ALTER %s %s OWNER TO %I', item.kind, item.name, item.owner);
        END IF;
    END LOOP;
END
$owners$;

-- The API's door to the partitions of analytics_event: a day's partition, created ahead of
-- its first view, then dropped once out of retention. SECURITY DEFINER runs them as their
-- owner; the name and the bounds come from a date alone, and the search path holds no
-- schema anyone may create in.
CREATE OR REPLACE FUNCTION public.lodb_create_analytics_partition(day date)
    RETURNS boolean
    LANGUAGE plpgsql
    STRICT
    SECURITY DEFINER
    SET search_path = pg_catalog, pg_temp
    SET TimeZone = 'UTC'
AS $function$
DECLARE
    partition text := 'analytics_event_' || to_char(day, 'YYYYMMDD');
BEGIN
    IF to_regclass(format('public.%I', partition)) IS NOT NULL THEN
        RETURN false;
    END IF;
    EXECUTE format(
        'CREATE TABLE public.%I PARTITION OF public.analytics_event'
            || ' FOR VALUES FROM (%L) TO (%L)',
        partition,
        day::timestamptz,
        (day + 1)::timestamptz);
    RETURN true;
END
$function$;

CREATE OR REPLACE FUNCTION public.lodb_drop_analytics_partition(day date)
    RETURNS boolean
    LANGUAGE plpgsql
    STRICT
    SECURITY DEFINER
    SET search_path = pg_catalog, pg_temp
AS $function$
DECLARE
    partition regclass := to_regclass(
        format('public.%I', 'analytics_event_' || to_char(day, 'YYYYMMDD')));
BEGIN
    IF partition IS NULL OR NOT EXISTS (
        SELECT FROM pg_inherits i
        WHERE i.inhrelid = partition
            AND i.inhparent = to_regclass('public.analytics_event'))
    THEN
        RETURN false;
    END IF;
    EXECUTE format('DROP TABLE %s', partition);
    RETURN true;
END
$function$;

ALTER FUNCTION public.lodb_create_analytics_partition(date) OWNER TO lodb_partitions;
ALTER FUNCTION public.lodb_drop_analytics_partition(date) OWNER TO lodb_partitions;
REVOKE ALL ON FUNCTION
    public.lodb_create_analytics_partition(date),
    public.lodb_drop_analytics_partition(date)
    FROM PUBLIC;
GRANT EXECUTE ON FUNCTION
    public.lodb_create_analytics_partition(date),
    public.lodb_drop_analytics_partition(date)
    TO lodb_app;

-- The API's rows: read and write on every table and view, read only on the two migration
-- histories, the sequences of the identity columns. Revoked first: a privilege granted by
-- hand does not outlive the next run. An extension's objects keep their own grants.
DO $grants$
DECLARE
    item record;
    kind text;
BEGIN
    FOR item IN
        SELECT c.oid::regclass AS name, c.relkind, c.relname
        FROM pg_class c
        WHERE c.relnamespace = 'public'::regnamespace
            AND c.relkind IN ('r', 'p', 'v', 'm', 'f', 'S')
            AND NOT EXISTS (
                SELECT FROM pg_depend d
                WHERE d.classid = 'pg_class'::regclass
                    AND d.objid = c.oid
                    AND d.deptype = 'e')
    LOOP
        kind := CASE WHEN item.relkind = 'S' THEN 'SEQUENCE' ELSE 'TABLE' END;
        EXECUTE format('REVOKE ALL ON %s %s FROM lodb_app', kind, item.name);
        EXECUTE format(
            'GRANT %s ON %s %s TO lodb_app',
            CASE
                WHEN item.relkind = 'S' THEN 'USAGE, SELECT'
                WHEN item.relname IN ('__EFMigrationsHistory', 'doctrine_migration_versions')
                    THEN 'SELECT'
                ELSE 'SELECT, INSERT, UPDATE, DELETE'
            END,
            kind,
            item.name);
    END LOOP;
END
$grants$;

-- What a migration creates is the API's as soon as it exists, before the end of the run
-- hands it to lodb_owner: the migrating role's defaults, and the owners' for the
-- partitions and for a migration that would create as lodb_owner.
ALTER DEFAULT PRIVILEGES FOR ROLE lodb_owner, lodb_partitions IN SCHEMA public
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO lodb_app;
ALTER DEFAULT PRIVILEGES FOR ROLE lodb_owner IN SCHEMA public
    GRANT USAGE, SELECT ON SEQUENCES TO lodb_app;

DO $defaults$
BEGIN
    EXECUTE format(
        'ALTER DEFAULT PRIVILEGES FOR ROLE %I IN SCHEMA public'
            || ' GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO lodb_app',
        current_user);
    EXECUTE format(
        'ALTER DEFAULT PRIVILEGES FOR ROLE %I IN SCHEMA public'
            || ' GRANT USAGE, SELECT ON SEQUENCES TO lodb_app',
        current_user);
END
$defaults$;
