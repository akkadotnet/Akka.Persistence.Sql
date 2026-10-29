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
- [DDL Scripts for Database Schema Management](#ddl-scripts-for-database-schema-management)
  * [Optional Tag-Table Foreign Keys](#optional-tag-table-foreign-keys)
- [Tuning Akka.Persistence.Query for Low-Latency Projections](#tuning-akkapersistencequery-for-low-latency-projections)
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

Starting with Akka.Persistence.Sql v1.5.51 or later, you can add health checks for your persistence plugins. Akka.Persistence.Sql supports two types of health checks that integrate with `Microsoft.Extensions.Diagnostics.HealthChecks` and can be used with ASP.NET Core health check endpoints.

#### Standard Health Checks

Standard health checks monitor the persistence plugins themselves and verify that journals and snapshot stores are properly initialized and accessible.

To configure standard health checks, use the `journalBuilder` and `snapshotBuilder` parameters with the `.WithHealthCheck()` method:

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

Standard health checks are tagged with `akka`, `persistence`, and either `journal` or `snapshot-store` for filtering and organization purposes.

#### Connectivity Health Checks (Akka.Persistence.Sql.Hosting v1.5.X+)

Connectivity health checks proactively verify database connectivity regardless of recent operation activity. This helps detect database outages during idle periods when the standard health checks might not catch issues.

**Using Akka.Hosting 1.5.55.1 or later (Simplified API):**

```csharp
services.AddAkka("my-system-name", (builder, provider) =>
{
    builder.WithSqlPersistence(
        connectionString: connectionString,
        providerName: ProviderName.SqlServer2022,
        journalBuilder: journal =>
        {
            journal.WithHealthCheck();           // Monitor plugin status
            journal.WithConnectivityCheck();     // Verify database connectivity
        },
        snapshotBuilder: snapshot =>
        {
            snapshot.WithHealthCheck();          // Monitor plugin status
            snapshot.WithConnectivityCheck();    // Verify database connectivity
        });
});
```

The connectivity checks will automatically:
- Verify connectivity to the underlying SQL database
- Test database responsiveness with a lightweight "SELECT 1" query
- Report `Healthy` when the database is accessible
- Report `Unhealthy` (configurable) when the database is unreachable or unresponsive

Connectivity checks are tagged with `akka`, `persistence`, `sql`, and either `journal` or `snapshot-store` for filtering and organization purposes.

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

#### Customizing Health Check Configuration

You can customize the names, tags, and unhealthy status for both standard and connectivity health checks:

```csharp
services.AddAkka("my-system-name", (builder, provider) =>
{
    builder.WithSqlPersistence(
        connectionString: connectionString,
        providerName: ProviderName.SqlServer2022,
        journalBuilder: journal =>
        {
            // Customize standard health check
            journal.WithHealthCheck(
                unHealthyStatus: HealthStatus.Degraded,
                name: "sql-journal",
                tags: new[] { "backend", "database", "sql" });

            // Customize connectivity health check
            journal.WithConnectivityCheck(
                unHealthyStatus: HealthStatus.Unhealthy,
                name: "sql-journal-connectivity",
                tags: new[] { "backend", "database", "sql", "connectivity" });
        },
        snapshotBuilder: snapshot =>
        {
            // Customize standard health check
            snapshot.WithHealthCheck(
                unHealthyStatus: HealthStatus.Degraded,
                name: "sql-snapshot",
                tags: new[] { "backend", "database", "sql" });

            // Customize connectivity health check
            snapshot.WithConnectivityCheck(
                unHealthyStatus: HealthStatus.Unhealthy,
                name: "sql-snapshot-connectivity",
                tags: new[] { "backend", "database", "sql", "connectivity" });
        });
});
```

**Default tags when not specified:**
- Standard health checks: `["akka", "persistence", "journal"]` or `["akka", "persistence", "snapshot-store"]`
- Connectivity health checks: `["akka", "persistence", "sql", "journal", "connectivity"]` or `["akka", "persistence", "sql", "snapshot-store", "connectivity"]`

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

# DDL Scripts for Database Schema Management

Pre-generated DDL scripts are available in [`docs/ddl/`](./docs/ddl/) for creating Akka.Persistence.Sql tables through your own migrations instead of relying on automatic table creation at runtime.

**Directory Structure:**

- [`docs/ddl/default/`](./docs/ddl/default/) - For new deployments using `table-mapping = default`
- [`docs/ddl/compat/`](./docs/ddl/compat/) - For migration from legacy plugins (uses provider-specific table mappings)

The initialization SQL files are checked into this repository:

| Provider | Default mapping | Compatibility mapping |
| --- | --- | --- |
| PostgreSQL | [Scripts](./docs/ddl/default/postgresql/) | [Scripts](./docs/ddl/compat/postgresql/) |
| SQL Server | [Scripts](./docs/ddl/default/sqlserver/) | [Scripts](./docs/ddl/compat/sqlserver/) |
| MySQL | [Scripts](./docs/ddl/default/mysql/) | [Scripts](./docs/ddl/compat/mysql/) |
| SQLite | [Scripts](./docs/ddl/default/sqlite/) | [Scripts](./docs/ddl/compat/sqlite/) |

Each directory contains SQL scripts for each supported provider (sqlserver, postgresql, mysql, sqlite):

- `journal.sql` - Event journal table
- `journal-tags.sql` - Tag table for `TagMode.TagTable`
- `snapshot.sql` - Snapshot store table
- `metadata.sql` - Metadata table for `delete-compatibility-mode`

**When to use DDL scripts:**

- When using Entity Framework Core or FluentMigrator for schema management
- When you need explicit control over database schema versioning
- When auto-initialization is disabled (`auto-initialize = false`)

See the [DDL documentation](./docs/ddl/README.md) for detailed usage instructions and examples.

## Optional Tag-Table Foreign Keys

The journal-to-tag foreign key is an **optional, operator-applied migration**. Runtime
auto-initialization and the base DDL scripts create tables without this FK.
For PostgreSQL and SQL Server, operators can explicitly install
`tags.ordering_id -> journal.ordering ON DELETE CASCADE` after creating the
tables. A physical journal-row deletion then removes its dependent tags.

For a new database, run the provider's `journal.sql` before `journal-tags.sql`,
plus `snapshot.sql` and `metadata.sql` where required. Use this base schema alone
without an FK, or opt in by configuring and running `02_add_foreign_key.sql` below after the
tables exist. For an existing database, audit and clean orphan tags with `01`
before applying `02`. Cleanup can also be used independently without adding an
FK. The application does not run these maintenance scripts at startup.

| Provider | Audit / bounded orphan cleanup | Install and validate FK | Remove FK |
| --- | --- | --- | --- |
| PostgreSQL | [01](./scripts/TagTableForeignKey/PostgreSql/01_cleanup_orphan_tags.sql) | [02](./scripts/TagTableForeignKey/PostgreSql/02_add_foreign_key.sql) | [03](./scripts/TagTableForeignKey/PostgreSql/03_drop_foreign_key.sql) |
| SQL Server | [01](./scripts/TagTableForeignKey/SqlServer/01_cleanup_orphan_tags.sql) | [02](./scripts/TagTableForeignKey/SqlServer/02_add_foreign_key.sql) | [03](./scripts/TagTableForeignKey/SqlServer/03_drop_foreign_key.sql) |

See the [maintenance instructions](./scripts/TagTableForeignKey/README.md) for
mapping headers, execution order, maintenance-window requirements, and rollback.
Orphan cleanup preserves tags whose parent journal row is soft-deleted but still
present. Removing the FK does not restore rows already deleted by cleanup or cascade.

### Measured FK overhead

The October 29, 2026 rerun covered 800 measurements, with five alternating
FK/no-FK pairs per case. The table shows the change in median event throughput
when adding the FK; negative values mean slower throughput. Inserts use batches
of 50, and deletes use four tags/event with a boundary of 100 (99 physical event
deletions per persistence ID).

| Branch | Database | Insert: 2 tags/event | Insert: 4 tags/event | Delete |
| --- | --- | ---: | ---: | ---: |
| v1.5 | PostgreSQL 16.15 | -19.8% | -30.9% | -40.6% |
| v1.5 | SQL Server 16.0.4185.3 | -1.9% | -10.7% | -24.4% |
| dev | PostgreSQL 16.15 | -30.9% | -31.6% | -38.0% |
| dev | SQL Server 16.0.4185.3 | +0.2% | +2.2% | -28.0% |

All four rows use `prefer-parameters-on-multirow-insert = true`; the dev rows
also enable `tagtable-asqueryable-insert-mode = parameterized` from #580. That
parent-insert setting is unavailable on v1.5. With parameterized parents but
literal tags, dev SQL Server's FK insert penalties were 44.5% and 56.4%, so
parent batching alone does not eliminate the cost.

The small positive SQL Server differences are within observed variation, not
evidence of a speedup. These are shared-host, small-payload measurements with
substantial contention. Stable and dev use different .NET/Akka versions, and
configuration runs occurred at different times; compare FK/no-FK within each
configuration rather than treating rows as a controlled branch comparison.
For all configurations, absolute rates, raw repetitions, and provenance, see
the [full benchmark report in PR #610](https://github.com/akkadotnet/Akka.Persistence.Sql/pull/610).
Measure the deployment's workload before opting into the FK.

# Tuning Akka.Persistence.Query for Low-Latency Projections

Default polling intervals cause 500-1200ms projection latency. For low-latency CQRS, tune **BOTH** settings together:

```hocon
akka.persistence.query.journal.sql {
    refresh-interval = 10ms          # Query stream polling
    max-buffer-size = 50
    journal-sequence-retrieval {
        query-delay = 10ms           # Sequence actor polling - CRITICAL!
    }
}
```

**Important**: Tuning only `refresh-interval` does NOT work. The sequence actor polls independently via `query-delay`.

## Performance Guide

| Configuration | Latency | Database Load |
|--------------|---------|---------------|
| Default (1s/1s) | 500-1200ms | Low |
| Balanced (50ms/50ms) | 50-200ms | Medium |
| Low latency (10ms/10ms) | 20-100ms | High |

**Trade-off**: Lower = faster but higher DB load. In clusters, each node polls independently.

See [persistence.conf](https://github.com/akkadotnet/Akka.Persistence.Sql/blob/dev/src/Akka.Persistence.Sql/persistence.conf#L352-L398) for detailed settings documentation.

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
