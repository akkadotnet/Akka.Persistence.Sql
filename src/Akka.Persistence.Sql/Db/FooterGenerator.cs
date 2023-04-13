// -----------------------------------------------------------------------
//  <copyright file="FooterGenerator.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Config;
using LinqToDB;

#nullable enable
namespace Akka.Persistence.Sql.Db
{
    public static class FooterGenerator
    {
        public static string? GenerateJournalFooter(this JournalConfig config)
        {
            var tableName = config.TableConfig.EventJournalTable.Name;
            var columns = config.TableConfig.EventJournalTable.ColumnNames;
            var journalFullTableName = string.IsNullOrEmpty(config.TableConfig.SchemaName) 
                ? tableName : $"{config.TableConfig.SchemaName}.{tableName}";
			
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
ALTER TABLE {journalFullTableName} ADD CONSTRAINT UQ_{tableName} UNIQUE ({columns.PersistenceId}, {columns.SequenceNumber});

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE
        object_id = OBJECT_ID('{journalFullTableName}') AND
        name = 'IX_{tableName}_{columns.SequenceNumber}'
)
CREATE INDEX IX_{tableName}_{columns.SequenceNumber} ON {journalFullTableName}({columns.SequenceNumber});

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE 
        object_id = OBJECT_ID('{journalFullTableName}') AND
        name = 'IX_{tableName}_{columns.Created}'
)
CREATE INDEX IX_{tableName}_{columns.Created} ON {journalFullTableName}({columns.Created});";
            #endregion
			
            #region MySql
            if (config.ProviderName.StartsWith(ProviderName.MySql))
                return $@";
DROP PROCEDURE IF EXISTS AKKA_Journal_Setup;

DELIMITER ??
CREATE PROCEDURE AKKA_Journal_Setup()
BEGIN
	DECLARE UniqueExists TINYINT UNSIGNED DEFAULT 0;
	DECLARE CreatedIndexExists TINYINT UNSIGNED DEFAULT 0;
	DECLARE SequenceIndexExists TINYINT UNSIGNED DEFAULT 0;

	SELECT 1 INTO UniqueExists
	FROM information_schema.TABLE_CONSTRAINTS
	WHERE
        CONSTRAINT_SCHEMA = DATABASE() AND
        TABLE_NAME = '{tableName}' AND
        CONSTRAINT_NAME   = '{columns.PersistenceId}' AND
        CONSTRAINT_TYPE   = 'UNIQUE';

	SELECT 1 INTO CreatedIndexExists
	FROM information_schema.INNODB_INDEXES
	WHERE NAME = '{tableName}_{columns.Created}_idx';

	SELECT 1 INTO SequenceIndexExists
	FROM information_schema.INNODB_INDEXES
	WHERE NAME = '{tableName}_{columns.SequenceNumber}_idx';

	IF (UniqueExists = 0) THEN
		ALTER TABLE {journalFullTableName} ADD UNIQUE ({columns.PersistenceId}, {columns.SequenceNumber});
	END IF;

	IF (CreatedIndexExists = 0) THEN
		CREATE INDEX {tableName}_{columns.Created}_idx ON {journalFullTableName} ({columns.Created});
	END IF;

	IF (SequenceIndexExists = 0) THEN
		CREATE INDEX {tableName}_{columns.SequenceNumber}_idx ON {journalFullTableName} ({columns.SequenceNumber});
	END IF;
END??
DELIMITER ;

CALL AKKA_Journal_Setup();

DROP PROCEDURE IF EXISTS AKKA_Journal_Setup;";
            #endregion
			
            #region PostgreSql
            // These SQL syntax should be supported from PostgreSql 9.0
            if (config.ProviderName.StartsWith(ProviderName.PostgreSQL))
            {
	            journalFullTableName = string.IsNullOrEmpty(config.TableConfig.SchemaName) 
		            ? tableName : $"\"{config.TableConfig.SchemaName}\".\"{tableName}\"";
	            
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
		create index {tableName}_{columns.PersistenceId}_idx on {journalFullTableName} ({columns.PersistenceId});
	exception
		when duplicate_table
		then raise notice 'index ""{tableName}_{columns.PersistenceId}_idx"" on {journalFullTableName} already exists, skipping';
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
CREATE INDEX IF NOT EXISTS {tableName}_{columns.PersistenceId}_idx ON {journalFullTableName} ({columns.PersistenceId});
CREATE INDEX IF NOT EXISTS {tableName}_{columns.SequenceNumber}_idx ON {journalFullTableName} ({columns.SequenceNumber});";
            #endregion
			
            return null;
        }

        public static string? GenerateTagFooter(this JournalConfig config)
        {
	        var tableName = config.TableConfig.TagTable.Name;
	        var columns = config.TableConfig.TagTable.ColumnNames;
	        var tagFullTableName = string.IsNullOrEmpty(config.TableConfig.SchemaName)
		        ? tableName : $"{config.TableConfig.SchemaName}.{tableName}";
			
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
CREATE INDEX IX_{tableName}_{columns.PersistenceId}_{columns.SequenceNumber} ON {tagFullTableName} ({columns.PersistenceId}, {columns.SequenceNumber});

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE 
        object_id = OBJECT_ID('{tagFullTableName}') AND
        name = 'IX_{tableName}_{columns.Tag}'
)
CREATE INDEX IX_{tableName}_{columns.Tag} ON {tagFullTableName} ({columns.Tag});
";
	        #endregion
			
	        #region MySql
	        if (config.ProviderName.StartsWith(ProviderName.MySql))
		        return $@";
DROP PROCEDURE IF EXISTS AKKA_Tag_Setup;

DELIMITER ??
CREATE PROCEDURE AKKA_Tag_Setup()
BEGIN
	DECLARE DeleteIndexExists TINYINT UNSIGNED DEFAULT 0;
	DECLARE TagIndexExists TINYINT UNSIGNED DEFAULT 0;

	SELECT 1 INTO DeleteIndexExists
	FROM information_schema.INNODB_INDEXES
	WHERE NAME = '{tableName}_{columns.PersistenceId}_{columns.SequenceNumber}_idx';

	SELECT 1 INTO TagIndexExists
	FROM information_schema.INNODB_INDEXES
	WHERE NAME = '{tableName}_{columns.Tag}_idx';

	IF (DeleteIndexExists = 0) THEN
		CREATE INDEX {tableName}_{columns.PersistenceId}_{columns.SequenceNumber}_idx ON {tagFullTableName} ({columns.PersistenceId}, {columns.SequenceNumber});
	END IF;

	IF (TagIndexExists = 0) THEN
		CREATE INDEX {tableName}_{columns.Tag}_idx ON {tagFullTableName} ({columns.Tag});
	END IF;
END??
DELIMITER ;

CALL AKKA_Journal_Setup();

DROP PROCEDURE IF EXISTS AKKA_Tag_Setup;";
	        #endregion
			
	        #region PostgreSql
	        // These SQL syntax should be supported from PostgreSql 9.0
	        if (config.ProviderName.StartsWith(ProviderName.PostgreSQL))
	        {
		        tagFullTableName = string.IsNullOrEmpty(config.TableConfig.SchemaName) 
			        ? tableName : $"\"{config.TableConfig.SchemaName}\".\"{tableName}\"";
	            
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
		        ? tableName : $"{config.TableConfig.SchemaName}.{tableName}";

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
CREATE INDEX IX_{tableName}_{columns.SequenceNumber} ON {fullTableName}({columns.SequenceNumber});

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE 
        object_id = OBJECT_ID('{fullTableName}') AND
        name = 'IX_{tableName}_{columns.Created}'
)
CREATE INDEX IX_{tableName}_{columns.Created} ON {fullTableName}({columns.Created});";
	        #endregion

            #region MySql
            if (config.ProviderName.StartsWith(ProviderName.MySql))
                return $@";
DROP PROCEDURE IF EXISTS AKKA_Journal_Setup;

DELIMITER ??
CREATE PROCEDURE AKKA_Journal_Setup()
BEGIN
	DECLARE CreatedIndexExists TINYINT UNSIGNED DEFAULT 0;
	DECLARE SequenceIndexExists TINYINT UNSIGNED DEFAULT 0;

	SELECT 1 INTO CreatedIndexExists
	FROM information_schema.INNODB_INDEXES
	WHERE NAME = '{tableName}_{columns.Created}_idx';

	SELECT 1 INTO SequenceIndexExists
	FROM information_schema.INNODB_INDEXES
	WHERE NAME = '{tableName}_{columns.SequenceNumber}_idx';

	IF (CreatedIndexExists = 0) THEN
		CREATE INDEX {tableName}_{columns.Created}_idx ON {fullTableName} ({columns.Created});
	END IF;

	IF (SequenceIndexExists = 0) THEN
		CREATE INDEX {tableName}_{columns.SequenceNumber}_idx ON {fullTableName} ({columns.SequenceNumber});
	END IF;
END??
DELIMITER ;

CALL AKKA_Journal_Setup();

DROP PROCEDURE IF EXISTS AKKA_Journal_Setup;";
            #endregion
			
            #region PostgreSql
            // These SQL syntax should be supported from PostgreSql 9.0
            if (config.ProviderName.StartsWith(ProviderName.PostgreSQL))
            {
	            fullTableName = string.IsNullOrEmpty(config.TableConfig.SchemaName) 
		            ? tableName : $"\"{config.TableConfig.SchemaName}\".\"{tableName}\"";
	            
	            return $@";
do $BLOCK$
begin
	begin
		create index {tableName}_{columns.PersistenceId}_idx on {fullTableName} ({columns.PersistenceId});
	exception
		when duplicate_table
		then raise notice 'index ""{tableName}_{columns.PersistenceId}_idx"" on {fullTableName} already exists, skipping';
	end;

	begin
		create index {tableName}_{columns.SequenceNumber}_idx on {fullTableName} ({columns.SequenceNumber});
	exception
		when duplicate_table
		then raise notice 'index ""{tableName}_{columns.SequenceNumber}_idx"" on {fullTableName} already exists, skipping';
	end;
end;
$BLOCK$
";
            }
            #endregion
			
            #region Sqlite
            if (config.ProviderName.StartsWith(ProviderName.SQLite))
	            return $@";
CREATE INDEX IF NOT EXISTS {tableName}_{columns.PersistenceId}_idx ON {fullTableName} ({columns.PersistenceId});
CREATE INDEX IF NOT EXISTS {tableName}_{columns.SequenceNumber}_idx ON {fullTableName} ({columns.SequenceNumber});";
            #endregion

            return null;
        }
    }
}
