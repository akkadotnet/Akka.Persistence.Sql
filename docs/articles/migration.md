---
uid: sql-migration-guide
title: Migration Guide
---

# Migration Guide

This guide are only needed if you intend to migrate from legacy `Akka.Persistence` plugins to `Akka.Persistence.Sql`, this documentation **does not** apply if you are using `Akka.Persistence.Sql` in a greenfield project. 

> ### WARNING
> 
> Some of the steps in this guide might change the database schema of your persistent
> database, making it really hard to revert any changes.
> 
> Always do a database backup before attempting to do any of these migration steps.
> 
> Always test the result of any migration on a development environment

# Table Of Content 
* [Migrating Using Compatibility Mode](#migrating-using-compatibility-mode)
   + [Akka.Hosting Migration](#akkahosting-migration)
   + [HOCON Migration](#hocon-migration)
* [Tag Table Based Tag Query Migration (Optional)](#tag-table-based-tag-query-migration-optional)
* [Enable WriterUuid Anti-Corruption Layer Feature (Optional)](#enable-writeruuid-anti-corruption-layer-feature-optional)

# Migrating Using Compatibility Mode

`Akka.Persistence.Sql` provides a compatibility mode that works seamlessly side-by-side with existing legacy `Akka.Persistence.Sql.Common` implementation out of the box. Supported `Akka.Persistence.Sql.Common` compatibility targets:

| Plugin                        | Server               |
|-------------------------------|----------------------|
| `Akka.Persistence.SqlServer`  | Microsoft SQL Server |
| `Akka.Persistence.PostgreSql` | PostgreSql           |
| `Akka.Persistence.MySql`      | MySql                |
| `Akka.Persistence.Sqlite`     | SqLite               |

## Akka.Hosting Migration

To migrate to `Akka.Persistence.Sql.Hosting` from supported `Akka.Persistence.Sql.Common` plugins, supply the required parameters:

```csharp
builder
    .WithSqlPersistence(
        connectionString: "my-connection-string",
        providerName: ProviderName.{database-provider},
        databaseMapping: {database-mapping},
        tagStorageMode: TagMode.Csv,
        deleteCompatibilityMode: true,
        autoInitialize: false
    );
```

### Parameter Descriptions
* `connectionString` - **Required**, the proper connection string to your database of choice.
* `providerName` - **Required**, a string constant defining the database type to connect to, valid values are defined inside [`LinqToDB.ProviderName`](https://linq2db.github.io/api/LinqToDB.ProviderName.html) static class. Refer to the [LinqToDb API documentation](https://linq2db.github.io/api/LinqToDB.ProviderName.html) for valid values.
* `databaseMapping` - **Required**, set this parameter according to this table:
   
   | Plugin                      | databaseMapping              |
   |-----------------------------|------------------------------|
   | Akka.Persistence.SqlServer  | `DatabaseMapping.SqlServer`  |
   | Akka.Persistence.PostgreSql | `DatabaseMapping.PostgreSql` |
   | Akka.Persistence.MySql      | `DatabaseMapping.MySql`      |
   | Akka.Persistence.Sqlite     | `DatabaseMapping.SqLite`     |

* `tagStoreMode` - **Required**, always set this parameter to `TagMode.Csv`.
* `deleteCompatibilityMode` - **Required**, always set this parameter to `true`.
* `autoInitialize` - **Optional** but highly recommended, set this to `false` to prevent any schema modification.

## HOCON Migration

To migrate to `Akka.Persistence.Sql` from supported `Akka.Persistence.Sql.Common` plugins, modify your HOCON configuration to match these values:

```hocon
akka.persistence {
    journal {
        plugin = "akka.persistence.journal.sql"
        sql {
            # Required
            connection-string = "{database-connection-string}"
            
            # Required
            # Refer to LinqToDb.ProviderName for values.
            #
            # Always use a specific version if possible
            # to avoid provider detection performance penalty.
            #
            # Don't worry if your DB is newer than what is listed;
            # Just pick the newest one (if yours is still newer)
            provider-name = "{provider-name}"
            
            # Optional but highly recommended, do not change existing schema
            auto-initialize = false
            
            # Required for migration
            # Set {table-mapping} to "sqlite", "sql-server", "mysql", 
            # or "postgresql" depending on the plugin you're migrating from
            table-mapping = {table-mapping}
            
            # Required for migration
            tag-write-mode = Csv
            
            # Required for migration
            delete-compatibility-mode = true
        }
    }
    query.journal.sql {
        # Required
        connection-string = "{database-connection-string}"
        
        # Required
        provider-name = "{provider-name}"
        
        # Required for migration
        # Set {table-mapping} to "sqlite", "sql-server", "mysql", 
        # or "postgresql" depending on the plugin you're migrating from
        table-mapping = {table-mapping}
        
        # Required for migration
        tag-read-mode = Csv
    }
    snapshot-store {
        plugin = "akka.persistence.snapshot-store.sql"
        sql {
            # Required
            connection-string = "{database-connection-string}"
            
            # Required
            provider-name = "{provider-name}"
            
            # Optional but highly recommended, do not change existing schema
            auto-initialize = false
            
            # Required for migration
            # Set {table-mapping} to "sqlite", "sql-server", "mysql", 
            # or "postgresql" depending on the plugin you're migrating from
            table-mapping = {table-mapping}
        }
    }
}
```

## Upgrading to Tag Table (Optional)

This feature is an `Akka.Persistence.Sql` specific feature that leverages a separate table for event tags to speed up any tag based CQRS queries against the persistence backend.

This feature **is not** required for legacy `Akka.Persistence.Sql.Common` compatibility mode, only perform these migration steps if you require the CQRS performance boost that this feature entails.

> ### WARNING
> 
> This migration **WILL** change the database schema of your persistent database.
>
> Always do a database backup before attempting to do any of these migration steps.
> 
> **This migration path is not available for `Akka.Persistence.SqLite`.**

To migrate your database to use the new tag table based tag query feature, follow these steps:

1. Make sure that your system is [running as intended in compatibility mode](#migrating-using-compatibility-mode)
2. Download the migration SQL scripts for your particular database type from the ["Sql Scripts" folder](https://github.com/akkadotnet/Akka.Persistence.Sql/tree/dev/scripts) in `Akka.Persistence.Sql` repository.

   These SQL scripts are designed to be idempotent and can be run on the same database multiple times without creating any side effects.
3. Down your cluster.
4. Do a database backup and save the backup file somewhere safe.
5. Execute SQL script "1_Migration_Setup.sql" against your database.
6. Execute SQL script "2_Migration.sql" against your database.
7. (Optional) Execute SQL script "3_Post_Migration_Cleanup.sql" against your database.
8. Modify the configuration to enable the feature:

   **Using Akka.Hosting**
   ```csharp
   builder
       .WithSqlPersistence(
           connectionString: "my-connection-string",
           // ...
           tagStorageMode: TagMode.TagTable, // Change this line
       );
   ```
   **Using HOCON**
   ```hocon
   akka.persistence.journal.sql.tag-write-mode = TagTable # change this line
   akka.persistence.query.journal.sql.tag-read-mode = TagTable # change this line
   ```

# Enable WriterUuid Anti-Corruption Layer Feature (Recommended)

The WriterUuid is an anti-corruption feature added to SQL plugins. It **is not** required for legacy `Akka.Persistence.Sql.Common` compatibility mode, but it is highly recommended.

> ### WARNING
>
> This guide **WILL** change the database schema of your persistent database.
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
5. Bring the cluster back up.
