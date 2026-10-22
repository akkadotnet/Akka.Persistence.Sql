-- Remove tag rows whose journal event no longer exists.
--
-- Default behavior is a read-only report. Set execute_cleanup to true to delete
-- at most batch_size rows per invocation. Re-run the script until it reports
-- zero orphan rows. Run with psql in autocommit mode and without wrapping this
-- script in an explicit transaction so each invocation commits its one bounded
-- batch. Do not use SKIP LOCKED: a reported nonzero count must remain visible
-- until it is actually cleaned.
--
-- Pause journal writers/deleters while running cleanup and applying the FK so
-- no out-of-band deletion can create a new orphan between the final report and
-- constraint creation. Normal plugin writes/deletes are transactional.
--
-- Identifiers below are psql variables, then passed as values to format('%I')
-- for identifier quoting. Edit them for a custom schema/table/column mapping.
-- Defaults are the modern table mapping. For compat mapping, change journal_table
-- to event_journal; ordering remains ordering and tag names remain unchanged.
\set ON_ERROR_STOP on
\set schema_name 'public'
\set journal_table 'journal'
\set journal_ordering_column 'ordering'
\set tag_table 'tags'
\set tag_ordering_id_column 'ordering_id'
\set execute_cleanup 'false'
\set batch_size '1000'
\set sample_limit '20'

-- Keep values as session settings because psql does not interpolate variables
-- inside the dollar-quoted DO body. Values remain data; identifiers below are
-- quoted with format('%I', ...).
SELECT set_config('akka_tag_fk.schema_name', :'schema_name', false) AS ignored \gset
SELECT set_config('akka_tag_fk.journal_table', :'journal_table', false) AS ignored \gset
SELECT set_config('akka_tag_fk.journal_ordering_column', :'journal_ordering_column', false) AS ignored \gset
SELECT set_config('akka_tag_fk.tag_table', :'tag_table', false) AS ignored \gset
SELECT set_config('akka_tag_fk.tag_ordering_id_column', :'tag_ordering_id_column', false) AS ignored \gset
SELECT set_config('akka_tag_fk.execute_cleanup', :'execute_cleanup', false) AS ignored \gset
SELECT set_config('akka_tag_fk.batch_size', :'batch_size', false) AS ignored \gset
SELECT set_config('akka_tag_fk.sample_limit', :'sample_limit', false) AS ignored \gset

DO $cleanup$
DECLARE
    schema_name text := current_setting('akka_tag_fk.schema_name');
    journal_table text := current_setting('akka_tag_fk.journal_table');
    journal_ordering_column text := current_setting('akka_tag_fk.journal_ordering_column');
    tag_table text := current_setting('akka_tag_fk.tag_table');
    tag_ordering_id_column text := current_setting('akka_tag_fk.tag_ordering_id_column');
    execute_cleanup text := current_setting('akka_tag_fk.execute_cleanup');
    batch_size text := current_setting('akka_tag_fk.batch_size');
    sample_limit text := current_setting('akka_tag_fk.sample_limit');
    orphan_count bigint;
    sample_orderings text;
    deleted_count bigint := 0;
    remaining_count bigint;
BEGIN
    IF schema_name IS NULL OR octet_length(schema_name) = 0 OR octet_length(schema_name) > 63
       OR journal_table IS NULL OR octet_length(journal_table) = 0 OR octet_length(journal_table) > 63
       OR journal_ordering_column IS NULL OR octet_length(journal_ordering_column) = 0 OR octet_length(journal_ordering_column) > 63
       OR tag_table IS NULL OR octet_length(tag_table) = 0 OR octet_length(tag_table) > 63
       OR tag_ordering_id_column IS NULL OR octet_length(tag_ordering_id_column) = 0 OR octet_length(tag_ordering_id_column) > 63 THEN
        RAISE EXCEPTION 'Identifiers must be nonempty and at most 63 bytes';
    END IF;
    IF execute_cleanup NOT IN ('true', 'false') THEN
        RAISE EXCEPTION 'execute_cleanup must be true or false';
    END IF;
    IF batch_size !~ '^[1-9][0-9]*$' OR batch_size::bigint > 100000 THEN
        RAISE EXCEPTION 'batch_size must be an integer from 1 through 100000';
    END IF;
    IF sample_limit !~ '^[1-9][0-9]*$' OR sample_limit::integer > 100 THEN
        RAISE EXCEPTION 'sample_limit must be an integer from 1 through 100';
    END IF;

    EXECUTE format(
        'SELECT count(*) FROM %I.%I t WHERE NOT EXISTS (SELECT 1 FROM %I.%I j WHERE j.%I = t.%I)',
        schema_name, tag_table, schema_name, journal_table,
        journal_ordering_column, tag_ordering_id_column)
    INTO orphan_count;

    EXECUTE format(
        'SELECT string_agg(ordering_value::text, '', '' ORDER BY ordering_value) FROM (SELECT DISTINCT t.%I AS ordering_value FROM %I.%I t WHERE NOT EXISTS (SELECT 1 FROM %I.%I j WHERE j.%I = t.%I) ORDER BY t.%I LIMIT %s) sample',
        tag_ordering_id_column, schema_name, tag_table, schema_name,
        journal_table, journal_ordering_column, tag_ordering_id_column,
        tag_ordering_id_column, sample_limit::integer)
    INTO sample_orderings;

    RAISE NOTICE 'Orphan tag rows: %. Sample ordering_id values: %', orphan_count,
        COALESCE(sample_orderings, '(none)');

    IF execute_cleanup = 'true' AND orphan_count > 0 THEN
        EXECUTE format(
            'WITH orphan_batch AS (SELECT t.tableoid, t.ctid FROM %I.%I t WHERE NOT EXISTS (SELECT 1 FROM %I.%I j WHERE j.%I = t.%I) LIMIT %s) DELETE FROM %I.%I t USING orphan_batch b WHERE t.tableoid = b.tableoid AND t.ctid = b.ctid',
            schema_name, tag_table, schema_name, journal_table,
            journal_ordering_column, tag_ordering_id_column,
            batch_size::bigint, schema_name, tag_table);
        GET DIAGNOSTICS deleted_count = ROW_COUNT;

        EXECUTE format(
            'SELECT count(*) FROM %I.%I t WHERE NOT EXISTS (SELECT 1 FROM %I.%I j WHERE j.%I = t.%I)',
            schema_name, tag_table, schema_name, journal_table,
            journal_ordering_column, tag_ordering_id_column)
        INTO remaining_count;

        RAISE NOTICE 'Deleted this batch: %. Orphan tag rows remaining: %. Re-run until zero.',
            deleted_count, remaining_count;
    ELSIF execute_cleanup = 'false' THEN
        RAISE NOTICE 'Dry run only. Set execute_cleanup to true to delete one bounded batch.';
    END IF;
END;
$cleanup$;
