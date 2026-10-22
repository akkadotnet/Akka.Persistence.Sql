-- Add and validate tags(ordering_id) -> journal(ordering) ON DELETE CASCADE.
--
-- Run after 01_cleanup_orphan_tags.sql reports zero orphans. Pause journal
-- writers and deleters while applying this migration. WITH CHECK validates all
-- existing rows while SQL Server holds the locks required to add the FK, so a
-- concurrent write cannot slip between the orphan preflight and enforcement.
-- Lock waits are bounded to five seconds; validation itself can scan the tag
-- table and may hold schema/data locks for the duration. Run in a maintenance
-- window appropriate for the table size.
--
-- Identifier variables match 01_cleanup_orphan_tags.sql. Defaults are the
-- modern mapping. For compat mapping use EventJournal and Ordering. Edit each
-- sysname for a custom mapping; QUOTENAME quotes identifiers in dynamic SQL.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 5000;

DECLARE @schema_name nvarchar(4000) = N'dbo';
DECLARE @journal_table nvarchar(4000) = N'journal';
DECLARE @journal_ordering_column nvarchar(4000) = N'ordering';
DECLARE @tag_table nvarchar(4000) = N'tags';
DECLARE @tag_ordering_id_column nvarchar(4000) = N'ordering_id';
DECLARE @constraint_name nvarchar(4000) = N'tags_ordering_id_journal_ordering_fk';

IF @schema_name = N'' OR @journal_table = N'' OR @journal_ordering_column = N''
    OR @tag_table = N'' OR @tag_ordering_id_column = N'' OR @constraint_name = N''
    OR DATALENGTH(@schema_name) > 256 OR DATALENGTH(@journal_table) > 256
    OR DATALENGTH(@journal_ordering_column) > 256 OR DATALENGTH(@tag_table) > 256
    OR DATALENGTH(@tag_ordering_id_column) > 256 OR DATALENGTH(@constraint_name) > 256
    THROW 50100, 'Identifiers must be nonempty and at most 128 Unicode code units.', 1;
IF QUOTENAME(@schema_name) IS NULL OR QUOTENAME(@journal_table) IS NULL
    OR QUOTENAME(@journal_ordering_column) IS NULL OR QUOTENAME(@tag_table) IS NULL
    OR QUOTENAME(@tag_ordering_id_column) IS NULL OR QUOTENAME(@constraint_name) IS NULL
    THROW 50107, 'A configured identifier cannot be quoted safely.', 1;

DECLARE @journal_object_id int = OBJECT_ID(QUOTENAME(@schema_name) + N'.' + QUOTENAME(@journal_table), N'U');
DECLARE @tag_object_id int = OBJECT_ID(QUOTENAME(@schema_name) + N'.' + QUOTENAME(@tag_table), N'U');
DECLARE @journal_column_id int;
DECLARE @tag_column_id int;
IF @journal_object_id IS NULL OR @tag_object_id IS NULL
    THROW 50101, 'Configured journal or tags table does not exist.', 1;

SELECT @journal_column_id = column_id FROM sys.columns
 WHERE object_id = @journal_object_id AND name = @journal_ordering_column;
SELECT @tag_column_id = column_id FROM sys.columns
 WHERE object_id = @tag_object_id AND name = @tag_ordering_id_column;
IF @journal_column_id IS NULL OR @tag_column_id IS NULL
    THROW 50102, 'Configured journal ordering or tag ordering-id column does not exist.', 1;
IF EXISTS (
    SELECT 1 FROM sys.indexes i JOIN sys.data_spaces ds ON ds.data_space_id = i.data_space_id
     WHERE i.object_id IN (@journal_object_id, @tag_object_id) AND ds.type = 'PS'
)
    THROW 50106, 'This migration supports non-partitioned SQL Server tables only.', 1;

DECLARE @journal_object nvarchar(517) = QUOTENAME(@schema_name) + N'.' + QUOTENAME(@journal_table);
DECLARE @tag_object nvarchar(517) = QUOTENAME(@schema_name) + N'.' + QUOTENAME(@tag_table);
DECLARE @sql nvarchar(max);
DECLARE @orphan_count bigint;

BEGIN TRY
    BEGIN TRANSACTION;

    SET @sql = N'SELECT @count_out = COUNT_BIG(*) FROM ' + @tag_object + N' AS t LEFT JOIN '
        + @journal_object + N' AS j ON j.' + QUOTENAME(@journal_ordering_column) + N' = t.'
        + QUOTENAME(@tag_ordering_id_column) + N' WHERE j.'
        + QUOTENAME(@journal_ordering_column) + N' IS NULL;';
    EXEC sys.sp_executesql @sql, N'@count_out bigint OUTPUT', @count_out = @orphan_count OUTPUT;
    IF @orphan_count > 0
    BEGIN
        DECLARE @orphan_error nvarchar(2048) = CONCAT(
            N'Cannot add FK: ', @orphan_count,
            N' orphan tag rows remain; run 01_cleanup_orphan_tags.sql and retry.');
        THROW 50103, @orphan_error, 1;
    END;

    DECLARE @existing_fk_count int;
    DECLARE @existing_column_count int;
    DECLARE @exact_definition_count int;
    DECLARE @is_disabled bit;
    DECLARE @is_not_trusted bit;

    SELECT @existing_fk_count = COUNT(*)
      FROM sys.foreign_keys
     WHERE parent_object_id = @tag_object_id AND name = @constraint_name;

    IF @existing_fk_count > 0
    BEGIN
        SELECT @existing_column_count = COUNT(*),
               @exact_definition_count = SUM(CASE
                   WHEN fk.referenced_object_id = @journal_object_id
                    AND fk.delete_referential_action = 1
                    AND fk.update_referential_action = 0
                    AND fk.is_not_for_replication = 0
                    AND fkc.parent_column_id = @tag_column_id
                    AND fkc.referenced_column_id = @journal_column_id
                   THEN 1 ELSE 0 END),
               @is_disabled = MAX(CONVERT(int, fk.is_disabled)),
               @is_not_trusted = MAX(CONVERT(int, fk.is_not_trusted))
          FROM sys.foreign_keys fk
          JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
         WHERE fk.parent_object_id = @tag_object_id AND fk.name = @constraint_name;

        IF @existing_fk_count <> 1 OR @existing_column_count <> 1 OR @exact_definition_count <> 1
            THROW 50104, 'The configured constraint name already exists with a different FK definition.', 1;

        IF @is_disabled = 1 OR @is_not_trusted = 1
        BEGIN
            SET @sql = N'ALTER TABLE ' + @tag_object + N' WITH CHECK CHECK CONSTRAINT '
                + QUOTENAME(@constraint_name) + N';';
            EXEC (@sql);
        END;
        ELSE
            PRINT 'The configured FK already exists, is enabled, and is trusted.';
    END
    ELSE
    BEGIN
        IF EXISTS (
            SELECT 1 FROM sys.foreign_key_columns fkc
             WHERE fkc.parent_object_id = @tag_object_id
               AND fkc.parent_column_id = @tag_column_id
        )
            THROW 50105, 'A different FK already uses the configured tag ordering-id column; inspect it before applying this migration.', 1;

        SET @sql = N'ALTER TABLE ' + @tag_object + N' WITH CHECK ADD CONSTRAINT '
            + QUOTENAME(@constraint_name) + N' FOREIGN KEY (' + QUOTENAME(@tag_ordering_id_column)
            + N') REFERENCES ' + @journal_object + N' (' + QUOTENAME(@journal_ordering_column)
            + N') ON DELETE CASCADE; ALTER TABLE ' + @tag_object + N' CHECK CONSTRAINT '
            + QUOTENAME(@constraint_name) + N';';
        EXEC (@sql);
        PRINT 'The FK was added and validated.';
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
