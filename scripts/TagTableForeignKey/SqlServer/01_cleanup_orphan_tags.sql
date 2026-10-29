-- Remove tag rows whose journal event no longer exists.
--
-- Default behavior is a read-only report. Set @execute_cleanup to 1 to delete
-- at most @batch_size rows per invocation. Re-run the script until it reports
-- zero orphan rows. Run without an explicit surrounding transaction so each
-- invocation commits its one bounded batch. Do not use READPAST: a reported
-- nonzero count must remain visible until it is actually cleaned.
--
-- Pause journal writers/deleters while running cleanup and applying the FK so
-- no out-of-band deletion can create a new orphan between the final report and
-- constraint creation. Normal plugin writes/deletes are transactional.
--
-- Defaults are the modern table mapping. For compat mapping, change
-- @journal_table to N'EventJournal' and @journal_ordering_column to N'Ordering'.
-- Edit the sysname values for custom mappings; QUOTENAME safely quotes each
-- identifier rather than concatenating unquoted user-supplied SQL.
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @schema_name nvarchar(4000) = N'dbo';
DECLARE @journal_table nvarchar(4000) = N'journal';
DECLARE @journal_ordering_column nvarchar(4000) = N'ordering';
DECLARE @tag_table nvarchar(4000) = N'tags';
DECLARE @tag_ordering_id_column nvarchar(4000) = N'ordering_id';
DECLARE @execute_cleanup bit = 0;
DECLARE @batch_size int = 1000;
DECLARE @sample_limit int = 20;

IF @batch_size < 1 OR @batch_size > 100000
    THROW 50001, 'batch_size must be from 1 through 100000.', 1;
IF @sample_limit < 1 OR @sample_limit > 100
    THROW 50002, 'sample_limit must be from 1 through 100.', 1;
IF @execute_cleanup IS NULL
    THROW 50004, 'execute_cleanup must be 0 or 1.', 1;
IF @schema_name IS NULL OR DATALENGTH(@schema_name) = 0 OR DATALENGTH(@schema_name) > 256
    OR @journal_table IS NULL OR DATALENGTH(@journal_table) = 0 OR DATALENGTH(@journal_table) > 256
    OR @journal_ordering_column IS NULL OR DATALENGTH(@journal_ordering_column) = 0 OR DATALENGTH(@journal_ordering_column) > 256
    OR @tag_table IS NULL OR DATALENGTH(@tag_table) = 0 OR DATALENGTH(@tag_table) > 256
    OR @tag_ordering_id_column IS NULL OR DATALENGTH(@tag_ordering_id_column) = 0 OR DATALENGTH(@tag_ordering_id_column) > 256
    OR QUOTENAME(@schema_name) IS NULL OR QUOTENAME(@journal_table) IS NULL
    OR QUOTENAME(@journal_ordering_column) IS NULL OR QUOTENAME(@tag_table) IS NULL
    OR QUOTENAME(@tag_ordering_id_column) IS NULL
    THROW 50003, 'A schema, table, or column identifier is too long to quote safely.', 1;

DECLARE @journal_object nvarchar(517) = QUOTENAME(@schema_name) + N'.' + QUOTENAME(@journal_table);
DECLARE @tag_object nvarchar(517) = QUOTENAME(@schema_name) + N'.' + QUOTENAME(@tag_table);
DECLARE @sql nvarchar(max);
DECLARE @orphan_count bigint;
DECLARE @deleted_count int = 0;
DECLARE @remaining_count bigint;

SET @sql = N'SELECT @count_out = COUNT_BIG(*) FROM ' + @tag_object + N' AS t WHERE NOT EXISTS (SELECT 1 FROM '
    + @journal_object + N' AS j WHERE j.' + QUOTENAME(@journal_ordering_column) + N' = t.'
    + QUOTENAME(@tag_ordering_id_column) + N');';
EXEC sys.sp_executesql @sql, N'@count_out bigint OUTPUT', @count_out = @orphan_count OUTPUT;

SELECT @orphan_count AS orphan_tag_row_count;

SET @sql = N'SELECT TOP (@limit) t.' + QUOTENAME(@tag_ordering_id_column) + N' AS orphan_ordering_id FROM '
    + @tag_object + N' AS t WHERE NOT EXISTS (SELECT 1 FROM ' + @journal_object + N' AS j WHERE j.'
    + QUOTENAME(@journal_ordering_column) + N' = t.' + QUOTENAME(@tag_ordering_id_column)
    + N') GROUP BY t.' + QUOTENAME(@tag_ordering_id_column) + N' ORDER BY t.'
    + QUOTENAME(@tag_ordering_id_column) + N';';
EXEC sys.sp_executesql @sql, N'@limit int', @limit = @sample_limit;

IF @execute_cleanup = 0
BEGIN
    PRINT 'Dry run only. Set @execute_cleanup to 1 to delete one bounded batch.';
    RETURN;
END;

SET @sql = N'DELETE TOP (@limit) t FROM ' + @tag_object + N' AS t WHERE NOT EXISTS (SELECT 1 FROM '
    + @journal_object + N' AS j WHERE j.' + QUOTENAME(@journal_ordering_column) + N' = t.'
    + QUOTENAME(@tag_ordering_id_column) + N'); SELECT @deleted_out = @@ROWCOUNT;';
EXEC sys.sp_executesql @sql, N'@limit int, @deleted_out int OUTPUT',
    @limit = @batch_size, @deleted_out = @deleted_count OUTPUT;

SET @sql = N'SELECT @count_out = COUNT_BIG(*) FROM ' + @tag_object + N' AS t WHERE NOT EXISTS (SELECT 1 FROM '
    + @journal_object + N' AS j WHERE j.' + QUOTENAME(@journal_ordering_column) + N' = t.'
    + QUOTENAME(@tag_ordering_id_column) + N');';
EXEC sys.sp_executesql @sql, N'@count_out bigint OUTPUT', @count_out = @remaining_count OUTPUT;

SELECT @deleted_count AS deleted_tag_rows_this_batch, @remaining_count AS orphan_tag_rows_remaining;
PRINT 'Re-run until orphan_tag_rows_remaining is zero, then apply the FK migration.';
