-- Delete journal events older than a retention period, together with their tag rows.
--
-- Default behavior is a read-only report. Set execute_delete to true to delete
-- at most batch_size events (and their tag rows) per invocation, in one
-- transaction, tags first. Schedule the script (for example with pg_cron) or
-- re-run it until it reports zero eligible events. Run with psql in autocommit
-- mode and without wrapping this script in an explicit transaction so each
-- invocation commits its one bounded batch.
--
-- An event is eligible when:
--   * its created value is older than retention_days, and
--   * it is not the highest sequence number of its persistence ID. The plugin
--     reads that row to decide where the entity resumes writing, so it is
--     always kept.
--
-- This script does not check snapshots or projection offsets. Choose a
-- retention period that recovery and every EventsByTag consumer no longer
-- need; see ../README.md before scheduling it.
--
-- The tag delete runs whether or not the optional tag-table FK is installed.
--
-- Identifiers below are psql variables, then passed as values to format('%I')
-- for identifier quoting. Edit them for a custom schema/table/column mapping.
-- Defaults are the modern table mapping. For compat mapping, change
-- journal_table to event_journal, journal_sequence_number_column to
-- sequence_nr and journal_created_column to created_at.
\set ON_ERROR_STOP on
\set schema_name 'public'
\set journal_table 'journal'
\set journal_ordering_column 'ordering'
\set journal_persistence_id_column 'persistence_id'
\set journal_sequence_number_column 'sequence_number'
\set journal_created_column 'created'
\set tag_table 'tags'
\set tag_ordering_id_column 'ordering_id'
\set retention_days '90'
\set execute_delete 'false'
\set batch_size '1000'

-- Keep values as session settings because psql does not interpolate variables
-- inside the dollar-quoted DO body. Values remain data; identifiers below are
-- quoted with format('%I', ...).
SELECT set_config('akka_retention.schema_name', :'schema_name', false) AS ignored \gset
SELECT set_config('akka_retention.journal_table', :'journal_table', false) AS ignored \gset
SELECT set_config('akka_retention.journal_ordering_column', :'journal_ordering_column', false) AS ignored \gset
SELECT set_config('akka_retention.journal_persistence_id_column', :'journal_persistence_id_column', false) AS ignored \gset
SELECT set_config('akka_retention.journal_sequence_number_column', :'journal_sequence_number_column', false) AS ignored \gset
SELECT set_config('akka_retention.journal_created_column', :'journal_created_column', false) AS ignored \gset
SELECT set_config('akka_retention.tag_table', :'tag_table', false) AS ignored \gset
SELECT set_config('akka_retention.tag_ordering_id_column', :'tag_ordering_id_column', false) AS ignored \gset
SELECT set_config('akka_retention.retention_days', :'retention_days', false) AS ignored \gset
SELECT set_config('akka_retention.execute_delete', :'execute_delete', false) AS ignored \gset
SELECT set_config('akka_retention.batch_size', :'batch_size', false) AS ignored \gset

DO $retention$
DECLARE
    schema_name text := current_setting('akka_retention.schema_name');
    journal_table text := current_setting('akka_retention.journal_table');
    ordering_column text := current_setting('akka_retention.journal_ordering_column');
    persistence_id_column text := current_setting('akka_retention.journal_persistence_id_column');
    sequence_number_column text := current_setting('akka_retention.journal_sequence_number_column');
    created_column text := current_setting('akka_retention.journal_created_column');
    tag_table text := current_setting('akka_retention.tag_table');
    tag_ordering_id_column text := current_setting('akka_retention.tag_ordering_id_column');
    retention_days_text text := current_setting('akka_retention.retention_days');
    execute_delete_text text := current_setting('akka_retention.execute_delete');
    batch_size_text text := current_setting('akka_retention.batch_size');
    retention_days integer;
    execute_delete boolean;
    batch_size integer;
    cutoff_ticks bigint;
    eligible_predicate text;
    eligible_count bigint;
    deleted_tags bigint := 0;
    deleted_events bigint := 0;
BEGIN
    IF schema_name IS NULL OR octet_length(schema_name) = 0 OR octet_length(schema_name) > 63
       OR journal_table IS NULL OR octet_length(journal_table) = 0 OR octet_length(journal_table) > 63
       OR ordering_column IS NULL OR octet_length(ordering_column) = 0 OR octet_length(ordering_column) > 63
       OR persistence_id_column IS NULL OR octet_length(persistence_id_column) = 0 OR octet_length(persistence_id_column) > 63
       OR sequence_number_column IS NULL OR octet_length(sequence_number_column) = 0 OR octet_length(sequence_number_column) > 63
       OR created_column IS NULL OR octet_length(created_column) = 0 OR octet_length(created_column) > 63
       OR tag_table IS NULL OR octet_length(tag_table) = 0 OR octet_length(tag_table) > 63
       OR tag_ordering_id_column IS NULL OR octet_length(tag_ordering_id_column) = 0 OR octet_length(tag_ordering_id_column) > 63 THEN
        RAISE EXCEPTION 'Identifiers must be nonempty and at most 63 bytes';
    END IF;

    IF retention_days_text !~ '^[0-9]{1,6}$' OR retention_days_text::integer < 1 THEN
        RAISE EXCEPTION 'retention_days must be a whole number from 1 through 999999';
    END IF;
    IF batch_size_text !~ '^[0-9]{1,6}$' OR batch_size_text::integer NOT BETWEEN 1 AND 100000 THEN
        RAISE EXCEPTION 'batch_size must be from 1 through 100000';
    END IF;
    IF lower(execute_delete_text) NOT IN ('true', 'false') THEN
        RAISE EXCEPTION 'execute_delete must be true or false';
    END IF;

    retention_days := retention_days_text::integer;
    batch_size := batch_size_text::integer;
    execute_delete := lower(execute_delete_text) = 'true';

    -- created holds .NET DateTime ticks (100 ns intervals since 0001-01-01 UTC).
    cutoff_ticks := (EXTRACT(EPOCH FROM now() - make_interval(days => retention_days)) * 10000000)::bigint
                    + 621355968000000000;

    eligible_predicate := format(
        'j.%1$I < $1 AND j.%2$I < (SELECT max(m.%2$I) FROM %3$I.%4$I m WHERE m.%5$I = j.%5$I)',
        created_column, sequence_number_column, schema_name, journal_table, persistence_id_column);

    EXECUTE format('SELECT count(*) FROM %I.%I j WHERE ', schema_name, journal_table) || eligible_predicate
        INTO eligible_count USING cutoff_ticks;

    RAISE NOTICE 'Events older than % days eligible for deletion: %', retention_days, eligible_count;

    IF NOT execute_delete THEN
        RAISE NOTICE 'Report only. Set execute_delete to true to delete at most % events per run.', batch_size;
        RETURN;
    END IF;

    EXECUTE format(
        'CREATE TEMP TABLE akka_retention_batch ON COMMIT DROP AS '
        'SELECT j.%I AS ordering FROM %I.%I j WHERE ', ordering_column, schema_name, journal_table)
        || eligible_predicate
        || format(' ORDER BY j.%I LIMIT $2', ordering_column)
        USING cutoff_ticks, batch_size;

    EXECUTE format(
        'DELETE FROM %I.%I t USING akka_retention_batch b WHERE t.%I = b.ordering',
        schema_name, tag_table, tag_ordering_id_column);
    GET DIAGNOSTICS deleted_tags = ROW_COUNT;

    EXECUTE format(
        'DELETE FROM %I.%I j USING akka_retention_batch b WHERE j.%I = b.ordering',
        schema_name, journal_table, ordering_column);
    GET DIAGNOSTICS deleted_events = ROW_COUNT;

    RAISE NOTICE 'Deleted % events and % tag rows. Remaining eligible events: %',
        deleted_events, deleted_tags, eligible_count - deleted_events;
END
$retention$;
