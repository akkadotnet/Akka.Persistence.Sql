// -----------------------------------------------------------------------
//  <copyright file="FooterGenerator.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

#nullable enable
using Akka.Persistence.Sql.Config;
using LinqToDB;

namespace Akka.Persistence.Sql.Db
{
    public static class FooterGenerator
    {
        public static string? GenerateJournalFooter(this JournalConfig config)
        {
            var tableName = config.TableConfig.EventJournalTable.Name;
            var columns = config.TableConfig.EventJournalTable.ColumnNames;
            var journalFullTableName = string.IsNullOrEmpty(config.TableConfig.SchemaName)
                ? tableName
                : $"{config.TableConfig.SchemaName}.{tableName}";

            #region SqlServer
            if (config.ProviderName.StartsWith(ProviderName.SqlServer))
                return @$";

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE
        object_id = OBJECT_ID('{journalFullTableName}') AND
        name = 'UQ_{tableName}'
)
BEGIN TRY
    ALTER TABLE {journalFullTableName} ADD CONSTRAINT UQ_{tableName} UNIQUE ({columns.PersistenceId}, {columns.SequenceNumber});
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 2714 -- Error code for 'constraint already exists'
    BEGIN
        PRINT 'Constraint UQ_{tableName} already exists, skipping.';
    END
    ELSE
    BEGIN
        THROW;
    END
END CATCH;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE
        object_id = OBJECT_ID('{journalFullTableName}') AND
        name = 'IX_{tableName}_{columns.SequenceNumber}'
)
BEGIN TRY
    CREATE INDEX IX_{tableName}_{columns.SequenceNumber} ON {journalFullTableName}({columns.SequenceNumber});
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 1913 -- Error code for 'index already exists'
    BEGIN
        PRINT 'Index IX_{tableName}_{columns.Created} already exists, skipping.';
    END
    ELSE
    BEGIN
        THROW;
    END
END CATCH;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE 
        object_id = OBJECT_ID('{journalFullTableName}') AND
        name = 'IX_{tableName}_{columns.Created}'
)
BEGIN TRY
    CREATE INDEX IX_{tableName}_{columns.Created} ON {journalFullTableName}({columns.Created});
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 1913 -- Error code for 'index already exists'
    BEGIN
        PRINT 'Index IX_{tableName}_{columns.Created} already exists, skipping.';
    END
    ELSE
    BEGIN
        THROW;
    END
END CATCH;
";
            #endregion

            #region MySql
            if (config.ProviderName.StartsWith(ProviderName.MySql))
                return $@";

SET @akka_journal_setup = IF(
	EXISTS (
		SELECT 1
		FROM information_schema.TABLE_CONSTRAINTS
		WHERE
        	CONSTRAINT_SCHEMA = DATABASE() AND
        	TABLE_NAME = '{tableName}' AND
        	CONSTRAINT_NAME   = '{columns.PersistenceId}' AND
        	CONSTRAINT_TYPE   = 'UNIQUE'
	), 
	'SELECT ''Unique constraint already exist. Skipping.'';',
	'ALTER TABLE {journalFullTableName} ADD UNIQUE ({columns.PersistenceId}, {columns.SequenceNumber})'
);
PREPARE akka_statement_journal_setup FROM @akka_journal_setup;
EXECUTE akka_statement_journal_setup;
DEALLOCATE PREPARE akka_statement_journal_setup;

SET @akka_journal_setup = IF(
	EXISTS (
		SELECT 1
		FROM information_schema.INNODB_INDEXES
		WHERE NAME = '{tableName}_{columns.Created}_idx'
	), 
	'SELECT ''Index {tableName}_{columns.Created}_idx already exist. Skipping.'';',
	'CREATE INDEX {tableName}_{columns.Created}_idx ON {journalFullTableName} ({columns.Created})'
);
PREPARE akka_statement_journal_setup FROM @akka_journal_setup;
EXECUTE akka_statement_journal_setup;
DEALLOCATE PREPARE akka_statement_journal_setup;

SET @akka_journal_setup = IF(
	EXISTS (
		SELECT 1
		FROM information_schema.INNODB_INDEXES
		WHERE NAME = '{tableName}_{columns.SequenceNumber}_idx'
	), 
	'SELECT ''Index {tableName}_{columns.SequenceNumber}_idx already exist. Skipping.'';',
	'CREATE INDEX {tableName}_{columns.SequenceNumber}_idx ON {journalFullTableName} ({columns.SequenceNumber})'
);
PREPARE akka_statement_journal_setup FROM @akka_journal_setup;
EXECUTE akka_statement_journal_setup;
DEALLOCATE PREPARE akka_statement_journal_setup;";
            #endregion

            #region PostgreSql
            // These SQL syntax should be supported from PostgreSql 9.0
            if (config.ProviderName.StartsWith(ProviderName.PostgreSQL))
            {
                journalFullTableName = string.IsNullOrEmpty(config.TableConfig.SchemaName)
                    ? tableName
                    : $"\"{config.TableConfig.SchemaName}\".\"{tableName}\"";

                return $@";
do $BLOCK$
begin
	begin
		alter table {journalFullTableName} add constraint {tableName}_uq unique ({columns.PersistenceId}, {columns.SequenceNumber});
	exception
		when duplicate_table
		then raise notice 'unique constraint ""{tableName}_uq"" on {journalFullTableName} already exists, skipping';
	end;

	begin
		create index {tableName}_{columns.Created}_idx on {journalFullTableName} ({columns.Created});
	exception
		when duplicate_table
		then raise notice 'index ""{tableName}_{columns.Created}_idx"" on {journalFullTableName} already exists, skipping';
	end;

	begin
		create index {tableName}_{columns.SequenceNumber}_idx on {journalFullTableName} ({columns.SequenceNumber});
	exception
		when duplicate_table
		then raise notice 'index ""{tableName}_{columns.SequenceNumber}_idx"" on {journalFullTableName} already exists, skipping';
	end;
end;
$BLOCK$
";
            }
            #endregion

            #region Sqlite
            if (config.ProviderName.StartsWith(ProviderName.SQLite))
                return $@";
CREATE UNIQUE INDEX IF NOT EXISTS {tableName}_uq ON {journalFullTableName} ({columns.PersistenceId}, {columns.SequenceNumber});
CREATE INDEX IF NOT EXISTS {tableName}_{columns.Created}_idx ON {journalFullTableName} ({columns.Created});
CREATE INDEX IF NOT EXISTS {tableName}_{columns.SequenceNumber}_idx ON {journalFullTableName} ({columns.SequenceNumber});";
            #endregion

            return null;
        }

        public static string? GenerateTagFooter(this JournalConfig config)
        {
            var tableName = config.TableConfig.TagTable.Name;
            var columns = config.TableConfig.TagTable.ColumnNames;
            var tagFullTableName = string.IsNullOrEmpty(config.TableConfig.SchemaName)
                ? tableName
                : $"{config.TableConfig.SchemaName}.{tableName}";

            #region SqlServer
            if (config.ProviderName.StartsWith(ProviderName.SqlServer))
                return $@";

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE 
        object_id = OBJECT_ID('{tagFullTableName}') AND
        name = 'IX_{tableName}_{columns.PersistenceId}_{columns.SequenceNumber}'
)
BEGIN TRY
    CREATE INDEX IX_{tableName}_{columns.PersistenceId}_{columns.SequenceNumber} ON {tagFullTableName} ({columns.PersistenceId}, {columns.SequenceNumber});
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 1913 -- Error code for 'index already exists'
    BEGIN
        PRINT 'Index IX_{tableName}_{columns.PersistenceId}_{columns.SequenceNumber} already exists, skipping.';
    END
    ELSE
    BEGIN
        THROW;
    END
END CATCH;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE 
        object_id = OBJECT_ID('{tagFullTableName}') AND
        name = 'IX_{tableName}_{columns.Tag}'
)
BEGIN TRY
    CREATE INDEX IX_{tableName}_{columns.Tag} ON {tagFullTableName} ({columns.Tag});
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 1913 -- Error code for 'index already exists'
    BEGIN
        PRINT 'Index IX_{tableName}_{columns.Tag} already exists, skipping.';
    END
    ELSE
    BEGIN
        THROW;
    END
END CATCH;
";
            #endregion

            #region MySql
            if (config.ProviderName.StartsWith(ProviderName.MySql))
                return $@";

SET @akka_journal_setup = IF(
	EXISTS (
		SELECT 1
		FROM information_schema.INNODB_INDEXES
		WHERE NAME = '{tableName}_{columns.PersistenceId}_{columns.SequenceNumber}_idx'
	), 
	'SELECT ''Index {tableName}_{columns.PersistenceId}_{columns.SequenceNumber}_idx already exist. Skipping.'';',
	'CREATE INDEX {tableName}_{columns.PersistenceId}_{columns.SequenceNumber}_idx ON {tagFullTableName} ({columns.PersistenceId}, {columns.SequenceNumber})'
);
PREPARE akka_statement_journal_setup FROM @akka_journal_setup;
EXECUTE akka_statement_journal_setup;
DEALLOCATE PREPARE akka_statement_journal_setup;

SET @akka_journal_setup = IF(
	EXISTS (
		SELECT 1
		FROM information_schema.INNODB_INDEXES
		WHERE NAME = '{tableName}_{columns.Tag}_idx'
	), 
	'SELECT ''Index {tableName}_{columns.Tag}_idx already exist. Skipping.'';',
	'CREATE INDEX {tableName}_{columns.Tag}_idx ON {tagFullTableName} ({columns.Tag})'
);
PREPARE akka_statement_journal_setup FROM @akka_journal_setup;
EXECUTE akka_statement_journal_setup;
DEALLOCATE PREPARE akka_statement_journal_setup;";
            #endregion

            #region PostgreSql
            // These SQL syntax should be supported from PostgreSql 9.0
            if (config.ProviderName.StartsWith(ProviderName.PostgreSQL))
            {
                tagFullTableName = string.IsNullOrEmpty(config.TableConfig.SchemaName)
                    ? tableName
                    : $"\"{config.TableConfig.SchemaName}\".\"{tableName}\"";

                return $@";
do $BLOCK$
begin
	begin
		create index {tableName}_{columns.PersistenceId}_{columns.SequenceNumber}_idx on {tagFullTableName} ({columns.PersistenceId}, {columns.SequenceNumber});
	exception
		when duplicate_table
		then raise notice 'index ""{tableName}_{columns.PersistenceId}_{columns.SequenceNumber}_idx"" on {tagFullTableName} already exists, skipping';
	end;

	begin
		create index {tableName}_{columns.Tag}_idx on {tagFullTableName} ({columns.Tag});
	exception
		when duplicate_table
		then raise notice 'index ""{tableName}_{columns.Tag}_idx"" on {tagFullTableName} already exists, skipping';
	end;
end;
$BLOCK$
";
            }
            #endregion

            #region Sqlite
            if (config.ProviderName.StartsWith(ProviderName.SQLite))
                return $@";
CREATE INDEX IF NOT EXISTS {tableName}_{columns.PersistenceId}_{columns.SequenceNumber}_idx ON {tagFullTableName} ({columns.PersistenceId}, {columns.SequenceNumber});
CREATE INDEX IF NOT EXISTS {tableName}_{columns.Tag}_idx ON {tagFullTableName} ({columns.Tag});";
            #endregion

            return null;
        }

        public static string? GenerateSnapshotFooter(this SnapshotConfig config)
        {
            var tableName = config.TableConfig.SnapshotTable.Name;
            var columns = config.TableConfig.SnapshotTable.ColumnNames;
            var fullTableName = string.IsNullOrEmpty(config.TableConfig.SchemaName)
                ? tableName
                : $"{config.TableConfig.SchemaName}.{tableName}";

            #region SqlServer
            if (config.ProviderName.StartsWith(ProviderName.SqlServer))
                return @$";

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE
        object_id = OBJECT_ID('{fullTableName}') AND
        name = 'IX_{tableName}_{columns.SequenceNumber}'
)
BEGIN TRY
    CREATE INDEX IX_{tableName}_{columns.SequenceNumber} ON {fullTableName}({columns.SequenceNumber});
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 1913 -- Error code for 'index already exists'
    BEGIN
        PRINT 'Index IX_{tableName}_{columns.SequenceNumber} already exists, skipping.';
    END
    ELSE
    BEGIN
        THROW;
    END
END CATCH;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE 
        object_id = OBJECT_ID('{fullTableName}') AND
        name = 'IX_{tableName}_{columns.Created}'
)
BEGIN TRY
    CREATE INDEX IX_{tableName}_{columns.Created} ON {fullTableName}({columns.Created});
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 1913 -- Error code for 'index already exists'
    BEGIN
        PRINT 'Index IX_{tableName}_{columns.Created} already exists, skipping.';
    END
    ELSE
    BEGIN
        THROW;
    END
END CATCH;
";
            #endregion

            #region MySql
            if (config.ProviderName.StartsWith(ProviderName.MySql))
                return $@";

SET @akka_journal_setup = IF(
	EXISTS (
		SELECT 1
		FROM information_schema.INNODB_INDEXES
		WHERE NAME = '{tableName}_{columns.SequenceNumber}_idx'
	), 
	'SELECT ''Index {tableName}_{columns.SequenceNumber}_idx already exist. Skipping.'';',
	'CREATE INDEX {tableName}_{columns.SequenceNumber}_idx ON {fullTableName} ({columns.SequenceNumber})'
);
PREPARE akka_statement_journal_setup FROM @akka_journal_setup;
EXECUTE akka_statement_journal_setup;
DEALLOCATE PREPARE akka_statement_journal_setup;

SET @akka_journal_setup = IF(
	EXISTS (
		SELECT 1
		FROM information_schema.INNODB_INDEXES
		WHERE NAME = '{tableName}_{columns.Created}_idx'
	), 
	'SELECT ''Index {tableName}_{columns.Created}_idx already exist. Skipping.'';',
	'CREATE INDEX {tableName}_{columns.Created}_idx ON {fullTableName} ({columns.Created})'
);
PREPARE akka_statement_journal_setup FROM @akka_journal_setup;
EXECUTE akka_statement_journal_setup;
DEALLOCATE PREPARE akka_statement_journal_setup;";
            #endregion

            #region PostgreSql
            // These SQL syntax should be supported from PostgreSql 9.0
            if (config.ProviderName.StartsWith(ProviderName.PostgreSQL))
            {
                fullTableName = string.IsNullOrEmpty(config.TableConfig.SchemaName)
                    ? tableName
                    : $"\"{config.TableConfig.SchemaName}\".\"{tableName}\"";

                return $@";
do $BLOCK$
begin
	begin
		create index {tableName}_{columns.SequenceNumber}_idx on {fullTableName} ({columns.SequenceNumber});
	exception
		when duplicate_table
		then raise notice 'index ""{tableName}_{columns.SequenceNumber}_idx"" on {fullTableName} already exists, skipping';
	end;

	begin
		create index {tableName}_{columns.Created}_idx on {fullTableName} ({columns.Created});
	exception
		when duplicate_table
		then raise notice 'index ""{tableName}_{columns.Created}_idx"" on {fullTableName} already exists, skipping';
	end;
end;
$BLOCK$
";
            }
            #endregion

            #region Sqlite
            if (config.ProviderName.StartsWith(ProviderName.SQLite))
                return $@";
CREATE INDEX IF NOT EXISTS {tableName}_{columns.SequenceNumber}_idx ON {fullTableName} ({columns.SequenceNumber});
CREATE INDEX IF NOT EXISTS {tableName}_{columns.Created}_idx ON {fullTableName} ({columns.Created});";
            #endregion

            return null;
        }
    }
}
