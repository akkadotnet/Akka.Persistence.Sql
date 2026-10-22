-- Drop only the configured FK constraint. This does not restore tag rows
-- deleted by ON DELETE CASCADE. Identifier variables match 02_add_foreign_key.sql.
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
    THROW 50200, 'Identifiers must be nonempty and at most 128 Unicode code units.', 1;
IF QUOTENAME(@schema_name) IS NULL OR QUOTENAME(@journal_table) IS NULL
    OR QUOTENAME(@journal_ordering_column) IS NULL OR QUOTENAME(@tag_table) IS NULL
    OR QUOTENAME(@tag_ordering_id_column) IS NULL OR QUOTENAME(@constraint_name) IS NULL
    THROW 50205, 'A configured identifier cannot be quoted safely.', 1;

DECLARE @journal_object_id int = OBJECT_ID(QUOTENAME(@schema_name) + N'.' + QUOTENAME(@journal_table), N'U');
DECLARE @tag_object_id int = OBJECT_ID(QUOTENAME(@schema_name) + N'.' + QUOTENAME(@tag_table), N'U');
DECLARE @journal_column_id int;
DECLARE @tag_column_id int;
IF @journal_object_id IS NULL OR @tag_object_id IS NULL
    THROW 50201, 'Configured journal or tags table does not exist.', 1;
SELECT @journal_column_id = column_id FROM sys.columns
 WHERE object_id = @journal_object_id AND name = @journal_ordering_column;
SELECT @tag_column_id = column_id FROM sys.columns
 WHERE object_id = @tag_object_id AND name = @tag_ordering_id_column;
IF @journal_column_id IS NULL OR @tag_column_id IS NULL
    THROW 50202, 'Configured journal ordering or tag ordering-id column does not exist.', 1;
IF EXISTS (
    SELECT 1 FROM sys.indexes i JOIN sys.data_spaces ds ON ds.data_space_id = i.data_space_id
     WHERE i.object_id IN (@journal_object_id, @tag_object_id) AND ds.type = 'PS'
)
    THROW 50204, 'This migration supports non-partitioned SQL Server tables only.', 1;

DECLARE @existing_fk_count int;
DECLARE @existing_column_count int;
DECLARE @exact_definition_count int;
DECLARE @sql nvarchar(max);
SELECT @existing_fk_count = COUNT(*)
  FROM sys.foreign_keys
 WHERE parent_object_id = @tag_object_id AND name = @constraint_name;

IF @existing_fk_count = 0
BEGIN
    PRINT 'The configured FK is absent; nothing to drop.';
    RETURN;
END;

SELECT @existing_column_count = COUNT(*),
       @exact_definition_count = SUM(CASE
           WHEN fk.referenced_object_id = @journal_object_id
            AND fk.delete_referential_action = 1
            AND fk.update_referential_action = 0
            AND fk.is_not_for_replication = 0
            AND fkc.parent_column_id = @tag_column_id
            AND fkc.referenced_column_id = @journal_column_id
           THEN 1 ELSE 0 END)
  FROM sys.foreign_keys fk
  JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
 WHERE fk.parent_object_id = @tag_object_id AND fk.name = @constraint_name;
IF @existing_fk_count <> 1 OR @existing_column_count <> 1 OR @exact_definition_count <> 1
    THROW 50203, 'The configured constraint name has a different FK definition; refusing to drop it.', 1;

SET @sql = N'ALTER TABLE ' + QUOTENAME(@schema_name) + N'.' + QUOTENAME(@tag_table)
    + N' DROP CONSTRAINT ' + QUOTENAME(@constraint_name) + N';';
EXEC (@sql);
