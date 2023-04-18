---
uid: sql-migration-guide
title: Migration Guide
---

# Migration Guide

> [!Warning]
> Some of the steps in this guide might change the database schema of your persistent
> database, making it really hard to revert any changes.
> 
> Always do a database backup before attempting to do any of these migration steps.
> 
> Always test the result of any migration on a development environment

## Migrating Using Compatibility Mode

Supported `Akka.Persistence.Sql.Common` targets

| Plugin                        | Server               |
|-------------------------------|----------------------|
| `Akka.Persistence.SqlServer`  | Microsoft SQL Server |
| `Akka.Persistence.PostgreSql` | PostgreSql           |
| `Akka.Persistence.MySql`      | MySql                |
| `Akka.Persistence.Sqlite`     | SqLite               |

### Akka.Hosting

To migrate to `Akka.Persistence.Sql` from supported `Akka.Persistence.Sql.Common` plugins, supply the required parameters:

```csharp
builder
    .WithSqlPersistence(
        connectionString: "my-connection-string",
        providerName: ProviderName.SqlServer2019,
        databaseMapping: DatabaseMapping.SqlServer,
        tagStorageMode: TagMode.Csv,
        deleteCompatibilityMode: true,
        useWriterUuidColumn: false,
        autoInitialize: false
    );
```

* `databaseMapping` - Set this parameter according to this table:
   
   | Plugin     | databaseMapping              |
   |------------|------------------------------|
   | SqlServer  | `DatabaseMapping.SqlServer`  |
   | PostgreSql | `DatabaseMapping.PostgreSql` |
   | MySql      | `DatabaseMapping.MySql`      |
   | Sqlite     | `DatabaseMapping.SqLite`     |

* `tagStoreMode` 
  * Set to `TagMode.Csv` if you do not or have not migrated your database to use tag table.
  * Set to `TagMode.TagTable` if you migrated your database to use tag table.
* `deleteCompatibilityMode` - always set this parameter to `true`
* `useWriterUuidColumn`
  * Set to `false` if you do not or have not migrated your database to use `WriterUuid` feature
  * Set to `true` if you migrated your database to use `WriterUuid`.
* `autoInitialize` - always set this to `false` to prevent any schema modification.

### HOCON

```hocon
akka.persistence {
    journal {
        plugin = "akka.persistence.journal.sql"
        sql {
            connection-string = "{database-connection-string}"
            provider-name = "{provider-name}"
            
            # Required for migration, do not change existing schema
            auto-initialize = false
            
            # Required for migration
            # Set to "sqlite", "sql-server", "mysql", or "postgresql"
            # depending on the plugin you're migrating from
            table-mapping = sql-server
            
            # Required if you did not migrate your database to tag table mode
            tag-write-mode = Csv
            
            # Required for migration
            delete-compatibility-mode = true
            
            # Required if you did not migrate your database to use WriterUuid
            {table-mapping-name}.journal.use-writer-uuid-column = false
        }
    }
    query.journal.sql {
        connection-string = "{database-connection-string}"
        provider-name = "{provider-name}"
            
        # Required if you did not migrate your database to tag table mode
        tag-write-mode = Csv
    }
    snapshot-store {
        plugin = "akka.persistence.snapshot-store.sql"
        sql {
            connection-string = "{database-connection-string}"
            provider-name = "{provider-name}"
            
            # Required for migration, do not change existing schema
            auto-initialize = false
            
            # Required for migration
            # Set to "sqlite", "sql-server", "mysql", or "postgresql"
            # depending on the plugin you're migrating from
            table-mapping = sql-server
        }
    }
}
```

## Migrating To Tag Table Based Tag Query

> [!Warning]
> This guide WILL change the database schema of your persistent database.
>
> Always do a database backup before attempting to do any of these migration steps.

To migrate your database to use the new tag table based tag query feature, follow these steps:

1. Download the migration SQL scripts for your particular database type from the "Sql Scrips" folder in `Akka.Persistence.Sql` repository.
2. Down your cluster.
3. Do a database backup and save the backup file somewhere safe.
4. Execute SQL script "1_Migration_Setup.sql" against your database.
5. Execute SQL script "2_Migration.sql" against your database.
6. (Optional) Execute SQL script "3_Post_Migration_Cleanup.sql" against your database.
7. Apply migration steps in [Migrating Using Compatibility Mode](#migrating-using-compatibility-mode) section.
8. Bring the cluster back up.

These SQL scripts are designed to be idempotent and can be run on the same database without creating any side effects.

## Migrating To Enable WriterUuid Anti-Corruption Layer Feature

> [!Warning]
> This guide WILL change the database schema of your persistent database.
>
> Always do a database backup before attempting to do any of these migration steps.

To migrate your database to use the new WriterUuid feature, follow these steps:

1. Do a database backup and save the backup file somewhere safe.
2. Down your cluster.
3. Execute this SQL script against your database:
    ```sql
    ALTER TABLE [journal_table_name] ADD writer_uuid VARCHAR(128);
    ```
4. Modify the database mapping configuration to enable the feature:

   **Using Akka.Hosting**

   ```csharp
   builder
       .WithSqlPersistence(
           connectionString: "my-connection-string",
           // ...
           useWriterUuidColumn: true // Use this setting
       );
   ```
   
   **Using HOCON**

   ```HOCON
   # replace {mapping-name} with "sqlite", "sql-server", "mysql", or "postgresql"
   akka.persistence.journal.sql.{mapping-name}.journal.use-writer-uuid-column = true
   ```
5. Apply migration steps in [Migrating Using Compatibility Mode](#migrating-using-compatibility-mode) section.   
6. Bring the cluster back up.
