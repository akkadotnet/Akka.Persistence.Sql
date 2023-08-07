# Akka.Persistence.Sql

A Cross-SQL-DB Engine Akka.Persistence plugin with broad database compatibility thanks to [Linq2Db](https://linq2db.github.io/).

This is a port of the amazing [akka-persistence-jdbc](https://github.com/akka/akka-persistence-jdbc) package from Scala, with a few improvements based on C# as well as our choice of data library.

Please read the documentation carefully. Some features may be specific to use case and have trade-offs (namely, compatibility modes)

> ### This Is Still a Beta
>
> Please note this is still considered 'work in progress' and only used if one understands the risks. While the TCK Specs pass you should still test in a 'safe' non-production environment carefully before deciding to fully deploy.

> ### Suitable For Greenfield Projects Only
>
>Until backward compatibility is properly tested and documented, it is recommended to use this plugin only on new greenfield projects that does not rely on existing persisted data.

# Table Of Content
- [Akka.Persistence.Sql](#akkapersistencesql)
- [Getting Started](#getting-started)
  * [The Easy Way, Using `Akka.Hosting`](#the-easy-way-using-akkahosting)
  * [The Classic Way, Using HOCON](#the-classic-way-using-hocon)
  * [Supported Database Providers](#supported-database-providers)
    + [Tested Database Providers](#tested-database-providers)
    + [Supported By Linq2Db But Untested In Akka.Persistence ](#supported-by-linq2db-but-untested-in-akkapersistence)
- [Sql.Common Compatibility modes](#sqlcommon-compatibility-modes)
- [Migration Guide](./docs/articles/migration.md)
  * [Migrating Using Compatibility Mode](./docs/articles/migration.md#migrating-using-compatibility-mode)
    + [Akka.Hosting Migration](./docs/articles/migration.md#akkahosting-migration)
    + [HOCON Migration](./docs/articles/migration.md#hocon-migration)
  * [Upgrading to Tag Table (Optional)](./docs/articles/migration.md#upgrading-to-tag-table-optional)
  * [Enable WriterUuid Anti-Corruption Layer Feature (Recommended)](./docs/articles/migration.md#enable-writeruuid-anti-corruption-layer-feature-recommended)
- [Migration Tutorial](./docs/articles/migration-walkthrough.md)
- [Features/Architecture](./docs/articles/features.md)
  * [Currently Implemented](./docs/articles/features.md#currently-implemented)
  * [Incomplete](./docs/articles/features.md#incomplete)
- [Performance Benchmarks](./docs/articles/benchmarks.md)
- [Configuration](./docs/articles/configuration.md)
  * [Journal](./docs/articles/configuration.md#journal)
  * [Snapshot Store](./docs/articles/configuration.md#snapshot-store)
- [Building this solution](#building-this-solution)
  + [Conventions](#conventions)
  + [Release Notes, Version Numbers, Etc](#release-notes-version-numbers-etc)

# Getting Started

## The Easy Way, Using `Akka.Hosting`

Assuming a MS SQL Server 2019 setup:
```csharp
var host = new HostBuilder()
    .ConfigureServices((context, services) => {
        services.AddAkka("my-system-name", (builder, provider) =>
        {
            builder.WithSqlPersistence(
                connectionString: _myConnectionString,
                providerName: ProviderName.SqlServer2019)
        });
    })
```

## The Classic Way, Using HOCON

These are the minimum HOCON configuration you need to start using Akka.Persistence.Sql:
```hocon
akka.persistence {
    journal {
        plugin = "akka.persistence.journal.sql"
        sql {
            connection-string = "{database-connection-string}"
            provider-name = "{provider-name}"
        }
    }
    query.journal.sql {
        connection-string = "{database-connection-string}"
        provider-name = "{provider-name}"
    }
    snapshot-store {
        plugin = "akka.persistence.snapshot-store.sql"
        sql {
            connection-string = "{database-connection-string}"
            provider-name = "{provider-name}"
        }
    }
}
```

* **database-connection-string**: The proper connection string to your database of choice.
* **provider-name**: A string constant defining the database type to connect to, valid values are defined inside `LinqToDB.ProviderName` static class. Refer to the Members of [`LinqToDb.ProviderName`](https://linq2db.github.io/api/LinqToDB.ProviderName.html) for included providers.

**Note**: For best performance, one should use the most specific provider name possible. i.e. `LinqToDB.ProviderName.SqlServer2012` instead of `LinqToDB.ProviderName.SqlServer`. Otherwise certain provider detections have to run more frequently which may impair performance slightly.

## Supported Database Providers

### Tested Database Providers
- Microsoft SQL Server
- MS SQLite
- System.Data.SQLite
- PostgreSQL using binary payload
- MySql

### Supported By Linq2Db But Untested In Akka.Persistence
- Firebird
- Microsoft Access OleDB
- Microsoft Access ODBC
- IBM DB2
- Informix
- Oracle
- Sybase
- SAP HANA
- ClickHouse

# Sql.Common Compatibility modes

- Delete Compatibility mode is available.
  - This mode will utilize a `journal_metadata` table containing the last sequence number
  - The main table delete is done the same way regardless of delete compatibility mode

**Delete Compatibility mode is expensive.**

- Normal Deletes involve first marking the deleted records as deleted, and then deleting them
  - Table compatibility mode adds an additional InsertOrUpdate and Delete
- **This all happens in a transaction**
  - In SQL Server this can cause issues because of page locks/etc.

# Building this solution

To run the build script associated with this solution, execute the following:

**Windows**
```
c:\> build.cmd all
```

**Linux / OS X**
```bash
c:\> build.sh all
```

If you need any information on the supported commands, please execute the `build.[cmd|sh] help` command.

This build script is powered by [FAKE](https://fake.build/); please see their API documentation should you need to make any changes to the [`build.fsx`](build.fsx) file.

### Conventions

The attached build script will automatically do the following based on the conventions of the project names added to this project:

* Any project name ending with `.Tests` will automatically be treated as a [XUnit2](https://xunit.github.io/) project and will be included during the test stages of this build script;
* Any project name ending with `.Tests` will automatically be treated as a [NBench](https://github.com/petabridge/NBench) project and will be included during the test stages of this build script; and
* Any project meeting neither of these conventions will be treated as a NuGet packaging target and its `.nupkg` file will automatically be placed in the `bin\nuget` folder upon running the `build.[cmd|sh] all` command.

### Release Notes, Version Numbers, Etc

This project will automatically populate its release notes in all of its modules via the entries written inside [`RELEASE_NOTES.md`](RELEASE_NOTES.md) and will automatically update the versions of all assemblies and NuGet packages via the metadata included inside [`Directory.Build.props`](src/Directory.Build.props).
