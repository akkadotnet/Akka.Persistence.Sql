-- Remove only the configured FK constraint. This does not restore tag rows
-- deleted by ON DELETE CASCADE. Identifier variables match 02_add_foreign_key.sql.
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

DO $tag_fk_drop$
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
    existing pg_constraint%ROWTYPE;
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
    SELECT attnum INTO journal_attnum FROM pg_attribute
     WHERE attrelid = journal_oid AND attname = journal_ordering_column
       AND attnum > 0 AND NOT attisdropped;
    SELECT attnum INTO tag_attnum FROM pg_attribute
     WHERE attrelid = tag_oid AND attname = tag_ordering_id_column
       AND attnum > 0 AND NOT attisdropped;
    IF journal_attnum IS NULL OR tag_attnum IS NULL THEN
        RAISE EXCEPTION 'Configured journal ordering or tag ordering-id column does not exist';
    END IF;

    EXECUTE format('LOCK TABLE %I.%I, %I.%I IN SHARE ROW EXCLUSIVE MODE',
                   schema_name, tag_table, schema_name, journal_table);

    SELECT * INTO existing FROM pg_constraint
     WHERE conrelid = tag_oid AND conname = constraint_name;
    IF NOT FOUND THEN
        RAISE NOTICE 'Constraint % is absent; nothing to drop', constraint_name;
        RETURN;
    END IF;
    IF existing.contype <> 'f'
       OR existing.confrelid <> journal_oid
       OR existing.conkey <> ARRAY[tag_attnum]::smallint[]
       OR existing.confkey <> ARRAY[journal_attnum]::smallint[]
       OR existing.confdeltype <> 'c'
       OR existing.confupdtype <> 'a'
       OR existing.confmatchtype <> 's'
       OR existing.condeferrable
       OR existing.condeferred THEN
        RAISE EXCEPTION 'Constraint % has a different definition; refusing to drop it', constraint_name;
    END IF;

    EXECUTE format('ALTER TABLE %I.%I DROP CONSTRAINT %I', schema_name, tag_table, constraint_name);
END;
$tag_fk_drop$;
COMMIT;
