# Akka.Persistence.Sql

A Cross-SQL-DB Engine Akka.Persistence plugin with broad database compatibility thanks to [Linq2Db](https://linq2db.github.io/).

This is a port of the amazing [akka-persistence-jdbc](https://github.com/akka/akka-persistence-jdbc) package from Scala, with a few improvements based on C# as well as our choice of data library.

Please read the documentation carefully. Some features may have specific use case and have trade-offs (namely, compatibility modes).

If you're migrating from legacy `Akka.Persistence.Sql.Common` based plugins, you can read the [migration guide documentation](https://github.com/akkadotnet/Akka.Persistence.Sql/blob/dev/docs/articles/migration.md), the [migration tutorial](https://github.com/akkadotnet/Akka.Persistence.Sql/blob/dev/docs/articles/migration-walkthrough.md), and watch the [migration tutorial video](https://youtu.be/gSmqUrVHPq8).

# Table Of Content
- [Akka.Persistence.Sql](#akkapersistencesql)
- [Getting Started](#getting-started)
  * [The Easy Way, Using `Akka.Hosting`](#the-easy-way-using-akkahosting)
    + [Health Checks](#health-checks)
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
                providerName: ProviderName.SqlServer2019);
        });
    });
```

You can also provide your own [`LinqToDb.DataOptions`](https://linq2db.github.io/api/linq2db/LinqToDB.DataOptions.html) object for a more advanced configuration.
Assuming a setup with a custom `NpgsqlDataSource`:
```csharp
NpgsqlDataSource dataSource = new NpgsqlDataSourceBuilder(_myConnectionString).Build();

DataOptions dataOptions = new DataOptions()
    .UseDataProvider(DataConnection.GetDataProvider(ProviderName.PostgreSQL, dataSource.ConnectionString) ?? throw new Exception("Could not get data provider"))
    .UseProvider(ProviderName.PostgreSQL)
    .UseConnectionFactory((opt) => dataSource.CreateConnection());
    
var host = new HostBuilder()
    .ConfigureServices((context, services) => {
        services.AddAkka("my-system-name", (builder, provider) =>
        {
            builder.WithSqlPersistence(dataOptions);
        });
    });
```
If `dataOptions` are provided, you must supply enough information for linq2db to connect to the database.
This includes setting the connection string and provider name again, if necessary for your use case.
Please consult the Linq2Db documentation for more details on configuring a valid DataOptions object.
Note that `MappingSchema` and `RetryPolicy` will always be overridden by Akka.Persistence.Sql.

### Health Checks

Starting with Akka.Persistence.Sql v1.5.51 or later, you can add health checks for your persistence plugins to verify that journals and snapshot stores are properly initialized and accessible. These health checks integrate with `Microsoft.Extensions.Diagnostics.HealthChecks` and can be used with ASP.NET Core health check endpoints.

To configure health checks, use the `journalBuilder` and `snapshotBuilder` parameters with the `.WithHealthCheck()` method:

```csharp
var host = new HostBuilder()
    .ConfigureServices((context, services) => {
        services.AddAkka("my-system-name", (builder, provider) =>
        {
            builder.WithSqlPersistence(
                connectionString: _myConnectionString,
                providerName: ProviderName.SqlServer2019,
                journalBuilder: journal => journal.WithHealthCheck(HealthStatus.Degraded),
                snapshotBuilder: snapshot => snapshot.WithHealthCheck(HealthStatus.Degraded));
        });
    });
```

The health checks will automatically:
- Verify connectivity to the underlying SQL database
- Test database responsiveness with a lightweight "SELECT 1" query
- Report `Healthy` when the database is accessible
- Report `Degraded` or `Unhealthy` (configurable) when the database is unreachable or unresponsive

Health checks are tagged with `akka`, `persistence`, and either `journal` or `snapshot-store` for filtering and organization purposes.

#### Exposing Health Checks via ASP.NET Core

For ASP.NET Core applications, you can expose these health checks via an endpoint:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add health checks service
builder.Services.AddHealthChecks();

builder.Services.AddAkka("my-system-name", (configBuilder, provider) =>
{
    configBuilder.WithSqlPersistence(
        connectionString: _myConnectionString,
        providerName: ProviderName.SqlServer2019,
        journalBuilder: journal => journal.WithHealthCheck(),
        snapshotBuilder: snapshot => snapshot.WithHealthCheck());
});

var app = builder.Build();

// Map health check endpoint
app.MapHealthChecks("/healthz");

app.Run();
```

#### Customizing Health Check Tags

You can customize the tags applied to health checks by providing an `IEnumerable<string>` to the `WithHealthCheck()` method:

```csharp
journalBuilder: j => j.WithHealthCheck(
    unHealthyStatus: HealthStatus.Degraded,
    name: "sql-journal",
    tags: new[] { "backend", "database", "sql" }),
snapshotBuilder: s => s.WithHealthCheck(
    unHealthyStatus: HealthStatus.Degraded,
    name: "sql-snapshot",
    tags: new[] { "backend", "database", "sql" })
```

When tags are not specified, the default tags are used: `["akka", "persistence", "journal"]` for journals and `["akka", "persistence", "snapshot-store"]` for snapshot stores.

## The Classic Way, Using HOCON

These are the minimum HOCON configuration you need to start using Akka.Persistence.Sql:
```hocon
akka.persistence {
    journal {
        plugin = "akka.persistence.journal.sql"
        sql {
            class = "Akka.Persistence.Sql.Journal.SqlWriteJournal, Akka.Persistence.Sql"
            connection-string = "{database-connection-string}"
            provider-name = "{provider-name}"
        }
    }
    query.journal.sql {
        class = "Akka.Persistence.Sql.Query.SqlReadJournalProvider, Akka.Persistence.Sql"
        connection-string = "{database-connection-string}"
        provider-name = "{provider-name}"
    }
    snapshot-store {
        plugin = "akka.persistence.snapshot-store.sql"
        sql {
            class = "Akka.Persistence.Sql.Snapshot.SqlSnapshotStore, Akka.Persistence.Sql"
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
