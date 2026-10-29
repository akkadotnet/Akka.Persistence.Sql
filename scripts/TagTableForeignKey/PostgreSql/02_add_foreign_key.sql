-- Add and validate tags(ordering_id) -> journal(ordering) ON DELETE CASCADE.
--
-- Run with psql and ON_ERROR_STOP enabled, after 01_cleanup_orphan_tags.sql
-- reports zero orphans (and cleanup is complete). Pause journal writers and
-- deleters for this migration. The transaction takes SHARE ROW EXCLUSIVE locks
-- on both tables and holds them through validation, preventing a write/delete
-- from racing the orphan check or FK installation. It fails quickly if those
-- locks are unavailable and bounds validation to 30 minutes; adjust the values
-- below for the maintenance window and database size.
--
-- Identifier variables below match 01_cleanup_orphan_tags.sql. Edit them for
-- custom mappings. For built-in compat mapping, set journal_table to
-- event_journal. The schema is shared by both tables.
\set ON_ERROR_STOP on
\set schema_name 'public'
\set journal_table 'journal'
\set journal_ordering_column 'ordering'
\set tag_table 'tags'
\set tag_ordering_id_column 'ordering_id'
\set constraint_name 'tags_ordering_id_journal_ordering_fk'

BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '30min';

SELECT set_config('akka.tag_fk.schema_name', :'schema_name', true);
SELECT set_config('akka.tag_fk.journal_table', :'journal_table', true);
SELECT set_config('akka.tag_fk.journal_ordering_column', :'journal_ordering_column', true);
SELECT set_config('akka.tag_fk.tag_table', :'tag_table', true);
SELECT set_config('akka.tag_fk.tag_ordering_id_column', :'tag_ordering_id_column', true);
SELECT set_config('akka.tag_fk.constraint_name', :'constraint_name', true);

DO $tag_fk$
DECLARE
    schema_name text := current_setting('akka.tag_fk.schema_name');
    journal_table text := current_setting('akka.tag_fk.journal_table');
    journal_ordering_column text := current_setting('akka.tag_fk.journal_ordering_column');
    tag_table text := current_setting('akka.tag_fk.tag_table');
    tag_ordering_id_column text := current_setting('akka.tag_fk.tag_ordering_id_column');
    constraint_name text := current_setting('akka.tag_fk.constraint_name');
    journal_oid oid;
    tag_oid oid;
    journal_attnum smallint;
    tag_attnum smallint;
    orphan_count bigint;
    existing pg_constraint%ROWTYPE;
    journal_kind "char";
    tag_kind "char";
    identifier text;
BEGIN
    FOREACH identifier IN ARRAY ARRAY[
        schema_name, journal_table, journal_ordering_column,
        tag_table, tag_ordering_id_column, constraint_name
    ] LOOP
        IF identifier = '' OR octet_length(convert_to(identifier, 'UTF8')) > 63 THEN
            RAISE EXCEPTION 'PostgreSQL identifiers must be nonempty and at most 63 UTF-8 bytes';
        END IF;
    END LOOP;

    journal_oid := to_regclass(format('%I.%I', schema_name, journal_table));
    tag_oid := to_regclass(format('%I.%I', schema_name, tag_table));
    IF journal_oid IS NULL OR tag_oid IS NULL THEN
        RAISE EXCEPTION 'Configured journal or tags table does not exist in schema %', schema_name;
    END IF;
    SELECT relkind INTO journal_kind FROM pg_class WHERE oid = journal_oid;
    SELECT relkind INTO tag_kind FROM pg_class WHERE oid = tag_oid;
    IF journal_kind <> 'r' OR tag_kind <> 'r' THEN
        RAISE EXCEPTION 'This migration supports ordinary, non-partitioned PostgreSQL tables only';
    END IF;
    IF EXISTS (SELECT 1 FROM pg_inherits
                WHERE inhrelid IN (journal_oid, tag_oid) OR inhparent IN (journal_oid, tag_oid)) THEN
        RAISE EXCEPTION 'This migration does not support PostgreSQL inheritance hierarchies';
    END IF;

    SELECT attnum INTO journal_attnum
      FROM pg_attribute
     WHERE attrelid = journal_oid AND attname = journal_ordering_column
       AND attnum > 0 AND NOT attisdropped;
    SELECT attnum INTO tag_attnum
      FROM pg_attribute
     WHERE attrelid = tag_oid AND attname = tag_ordering_id_column
       AND attnum > 0 AND NOT attisdropped;
    IF journal_attnum IS NULL OR tag_attnum IS NULL THEN
        RAISE EXCEPTION 'Configured journal ordering or tag ordering-id column does not exist';
    END IF;

    EXECUTE format('LOCK TABLE %I.%I, %I.%I IN SHARE ROW EXCLUSIVE MODE',
                   schema_name, tag_table, schema_name, journal_table);

    EXECUTE format(
        'SELECT count(*) FROM %I.%I t LEFT JOIN %I.%I j ON j.%I = t.%I WHERE j.%I IS NULL',
        schema_name, tag_table, schema_name, journal_table,
        journal_ordering_column, tag_ordering_id_column, journal_ordering_column)
      INTO orphan_count;
    IF orphan_count > 0 THEN
        RAISE EXCEPTION 'Cannot add FK: % orphan tag rows remain; run 01_cleanup_orphan_tags.sql and retry', orphan_count;
    END IF;

    SELECT * INTO existing
      FROM pg_constraint
     WHERE conrelid = tag_oid AND conname = constraint_name;
    IF FOUND THEN
        IF existing.contype <> 'f'
           OR existing.confrelid <> journal_oid
           OR existing.conkey <> ARRAY[tag_attnum]::smallint[]
           OR existing.confkey <> ARRAY[journal_attnum]::smallint[]
           OR existing.confdeltype <> 'c'
           OR existing.confupdtype <> 'a'
           OR existing.confmatchtype <> 's'
           OR existing.condeferrable
           OR existing.condeferred THEN
            RAISE EXCEPTION 'Constraint % already exists on %.% with a different definition',
                constraint_name, schema_name, tag_table;
        END IF;
        IF existing.convalidated THEN
            IF NOT EXISTS (SELECT 1 FROM pg_trigger WHERE tgconstraint = existing.oid)
               OR EXISTS (SELECT 1 FROM pg_trigger
                           WHERE tgconstraint = existing.oid AND tgenabled NOT IN ('O', 'A')) THEN
                RAISE EXCEPTION 'Constraint % has disabled or replica-only enforcement triggers', constraint_name;
            END IF;
            RAISE NOTICE 'Constraint % already exists with the expected validated definition', constraint_name;
            RETURN;
        END IF;
    ELSE
        IF EXISTS (
            SELECT 1 FROM pg_constraint c
             WHERE c.conrelid = tag_oid AND c.contype = 'f'
               AND tag_attnum = ANY(c.conkey)
        ) THEN
            RAISE EXCEPTION 'A different foreign key already uses %.%.%; inspect it before applying this migration',
                schema_name, tag_table, tag_ordering_id_column;
        END IF;

        EXECUTE format(
            'ALTER TABLE %I.%I ADD CONSTRAINT %I FOREIGN KEY (%I) REFERENCES %I.%I (%I) ON DELETE CASCADE NOT VALID',
            schema_name, tag_table, constraint_name, tag_ordering_id_column,
            schema_name, journal_table, journal_ordering_column);
    END IF;

    SELECT * INTO STRICT existing FROM pg_constraint
     WHERE conrelid = tag_oid AND conname = constraint_name AND contype = 'f';
    IF NOT EXISTS (SELECT 1 FROM pg_trigger WHERE tgconstraint = existing.oid)
       OR EXISTS (SELECT 1 FROM pg_trigger
                   WHERE tgconstraint = existing.oid AND tgenabled NOT IN ('O', 'A')) THEN
        RAISE EXCEPTION 'Constraint % has disabled or replica-only enforcement triggers', constraint_name;
    END IF;

    EXECUTE format('ALTER TABLE %I.%I VALIDATE CONSTRAINT %I',
                   schema_name, tag_table, constraint_name);
    RAISE NOTICE 'Constraint % is installed and validated', constraint_name;
END;
$tag_fk$;

COMMIT;
