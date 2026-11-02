-- Delete journal events older than a retention period, together with their tag rows.
--
-- Default behavior is a read-only report. Set @execute_delete to 1 to delete
-- at most @batch_size events (and their tag rows) per invocation, in one
-- transaction, tags first. Schedule the script (for example as a SQL Server
-- Agent job step) or re-run it until it reports zero eligible events. Run
-- without an explicit surrounding transaction so each invocation commits its
-- one bounded batch.
--
-- An event is eligible when:
--   * its created value is older than @retention_days, and
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
-- Defaults are the modern table mapping. For compat mapping, change
-- @journal_table to N'EventJournal', @journal_ordering_column to N'Ordering',
-- @journal_persistence_id_column to N'PersistenceId',
-- @journal_sequence_number_column to N'SequenceNr' and
-- @journal_created_column to N'Timestamp'.
-- Edit the sysname values for custom mappings; QUOTENAME safely quotes each
-- identifier rather than concatenating unquoted user-supplied SQL.
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @schema_name nvarchar(4000) = N'dbo';
DECLARE @journal_table nvarchar(4000) = N'journal';
DECLARE @journal_ordering_column nvarchar(4000) = N'ordering';
DECLARE @journal_persistence_id_column nvarchar(4000) = N'persistence_id';
DECLARE @journal_sequence_number_column nvarchar(4000) = N'sequence_number';
DECLARE @journal_created_column nvarchar(4000) = N'created';
DECLARE @tag_table nvarchar(4000) = N'tags';
DECLARE @tag_ordering_id_column nvarchar(4000) = N'ordering_id';
DECLARE @retention_days int = 90;
DECLARE @execute_delete bit = 0;
DECLARE @batch_size int = 1000;

IF @retention_days IS NULL OR @retention_days < 1 OR @retention_days > 999999
    THROW 50101, 'retention_days must be from 1 through 999999.', 1;
IF @batch_size IS NULL OR @batch_size < 1 OR @batch_size > 100000
    THROW 50102, 'batch_size must be from 1 through 100000.', 1;
IF @execute_delete IS NULL
    THROW 50103, 'execute_delete must be 0 or 1.', 1;
IF @schema_name IS NULL OR DATALENGTH(@schema_name) = 0 OR DATALENGTH(@schema_name) > 256
    OR @journal_table IS NULL OR DATALENGTH(@journal_table) = 0 OR DATALENGTH(@journal_table) > 256
    OR @journal_ordering_column IS NULL OR DATALENGTH(@journal_ordering_column) = 0 OR DATALENGTH(@journal_ordering_column) > 256
    OR @journal_persistence_id_column IS NULL OR DATALENGTH(@journal_persistence_id_column) = 0 OR DATALENGTH(@journal_persistence_id_column) > 256
    OR @journal_sequence_number_column IS NULL OR DATALENGTH(@journal_sequence_number_column) = 0 OR DATALENGTH(@journal_sequence_number_column) > 256
    OR @journal_created_column IS NULL OR DATALENGTH(@journal_created_column) = 0 OR DATALENGTH(@journal_created_column) > 256
    OR @tag_table IS NULL OR DATALENGTH(@tag_table) = 0 OR DATALENGTH(@tag_table) > 256
    OR @tag_ordering_id_column IS NULL OR DATALENGTH(@tag_ordering_id_column) = 0 OR DATALENGTH(@tag_ordering_id_column) > 256
    THROW 50104, 'A schema, table, or column identifier is too long to quote safely.', 1;

DECLARE @journal_object nvarchar(517) = QUOTENAME(@schema_name) + N'.' + QUOTENAME(@journal_table);
DECLARE @tag_object nvarchar(517) = QUOTENAME(@schema_name) + N'.' + QUOTENAME(@tag_table);
DECLARE @ordering sysname = QUOTENAME(@journal_ordering_column);
DECLARE @persistence_id sysname = QUOTENAME(@journal_persistence_id_column);
DECLARE @sequence_number sysname = QUOTENAME(@journal_sequence_number_column);
DECLARE @created sysname = QUOTENAME(@journal_created_column);
DECLARE @tag_ordering_id sysname = QUOTENAME(@tag_ordering_id_column);

IF OBJECT_ID(@journal_object, N'U') IS NULL
    THROW 50105, 'The configured journal table does not exist.', 1;
IF OBJECT_ID(@tag_object, N'U') IS NULL
    THROW 50106, 'The configured tag table does not exist.', 1;

-- created holds .NET DateTime ticks (100 ns intervals since 0001-01-01 UTC).
DECLARE @cutoff_ticks bigint =
    DATEDIFF_BIG(MILLISECOND, '1970-01-01', DATEADD(DAY, -@retention_days, SYSUTCDATETIME())) * 10000
    + 621355968000000000;

DECLARE @eligible_predicate nvarchar(max) =
    N'j.' + @created + N' < @cutoff_ticks AND j.' + @sequence_number
    + N' < (SELECT MAX(m.' + @sequence_number + N') FROM ' + @journal_object
    + N' m WHERE m.' + @persistence_id + N' = j.' + @persistence_id + N')';

DECLARE @eligible_count bigint;
DECLARE @sql nvarchar(max) =
    N'SELECT @eligible_count = COUNT_BIG(*) FROM ' + @journal_object + N' j WHERE ' + @eligible_predicate + N';';
EXEC sys.sp_executesql @sql,
    N'@cutoff_ticks bigint, @eligible_count bigint OUTPUT',
    @cutoff_ticks = @cutoff_ticks, @eligible_count = @eligible_count OUTPUT;

PRINT CONCAT(N'Events older than ', @retention_days, N' days eligible for deletion: ', @eligible_count);

IF @execute_delete = 0
BEGIN
    PRINT CONCAT(N'Report only. Set @execute_delete to 1 to delete at most ', @batch_size, N' events per run.');
    RETURN;
END;

DECLARE @deleted_tags bigint;
DECLARE @deleted_events bigint;

SET @sql =
    N'DECLARE @batch TABLE (ordering bigint PRIMARY KEY);
    INSERT INTO @batch (ordering)
    SELECT TOP (@batch_size) j.' + @ordering + N' FROM ' + @journal_object + N' j WHERE ' + @eligible_predicate + N'
    ORDER BY j.' + @ordering + N';

    BEGIN TRANSACTION;
    DELETE t FROM ' + @tag_object + N' t JOIN @batch b ON t.' + @tag_ordering_id + N' = b.ordering;
    SET @deleted_tags = @@ROWCOUNT;
    DELETE j FROM ' + @journal_object + N' j JOIN @batch b ON j.' + @ordering + N' = b.ordering;
    SET @deleted_events = @@ROWCOUNT;
    COMMIT TRANSACTION;';

EXEC sys.sp_executesql @sql,
    N'@cutoff_ticks bigint, @batch_size int, @deleted_tags bigint OUTPUT, @deleted_events bigint OUTPUT',
    @cutoff_ticks = @cutoff_ticks, @batch_size = @batch_size,
    @deleted_tags = @deleted_tags OUTPUT, @deleted_events = @deleted_events OUTPUT;

PRINT CONCAT(N'Deleted ', @deleted_events, N' events and ', @deleted_tags,
    N' tag rows. Remaining eligible events: ', @eligible_count - @deleted_events);
