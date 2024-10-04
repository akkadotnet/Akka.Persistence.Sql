#### 1.5.30 October 3rd 2024 ####

* [Bump Akka to 1.5.30](https://github.com/akkadotnet/akka.net/releases/tag/1.5.30)
* [Bump Akka.Hosting to v1.5.30](https://github.com/akkadotnet/Akka.Hosting/releases/tag/1.5.30)
* [PostgreSql: Use BIGINT for ordering column if PostgreSql version is greater than 10](https://github.com/akkadotnet/Akka.Persistence.Sql/pull/459)

#### 1.5.28 September 9th 2024 ####

* [Bump Akka to 1.5.28](https://github.com/akkadotnet/akka.net/releases/tag/1.5.28)
* [Bump Akka.Hosting to v1.5.28](https://github.com/akkadotnet/Akka.Hosting/releases/tag/1.5.28)
* [Harden SQL journal and snapshot store against initialization failures](https://github.com/akkadotnet/Akka.Persistence.Sql/pull/444)
* [Cleanup nullability warnings](https://github.com/akkadotnet/Akka.Persistence.Sql/pull/437)
* [Port Akka.NET #7313: Made DateTime.UtcNow the default timestamp for SnapshotMetadata](https://github.com/akkadotnet/Akka.Persistence.Sql/pull/448)
* [Add DataOptions support](https://github.com/akkadotnet/Akka.Persistence.Sql/pull/426)

**Linq2Db DataOptions Support**

You can now use [DataOptions](https://linq2db.github.io/api/linq2db/LinqToDB.DataOptions.html) to set up your persistence journal, read journal, and snapshot store with a new `Akka.Persistence.Sql.Hosting` API.

Here is an example of setting up persistence on PostgreSQL using `NpgsqlDataSource` instead of the previous connection string and provider name setup.

```csharp
var dataSource = new NpgsqlDataSourceBuilder(_myConnectionString).Build();

var dataOptions = new DataOptions()
    .UseDataProvider(DataConnection.GetDataProvider(ProviderName.PostgreSQL, dataSource.ConnectionString))
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

#### 1.5.27.1 August 1st 2024 ####

* [Fix missing "writer-plugin" generation from Akka.Persistence.Sql.Hosting](https://github.com/akkadotnet/Akka.Persistence.Sql/pull/427)

#### 1.5.27 July 30th 2024 ####

* [Bump Akka to 1.5.27.1](https://github.com/akkadotnet/akka.net/releases/tag/1.5.27.1)
* [Bump Akka.Hosting to v1.5.27](https://github.com/akkadotnet/Akka.Hosting/releases/tag/1.5.27)
* [Resolved: Auto initialization of tables isn't supported from the read journal side.](https://github.com/akkadotnet/Akka.Persistence.Sql/issues/344)
* [Add EventEnvelope Tags support on queries](https://github.com/akkadotnet/Akka.Persistence.Sql/pull/426)

#### 1.5.26 July 9th 2024 ####

* [Bump AkkaVersion to 1.5.26](https://github.com/akkadotnet/akka.net/releases/tag/1.5.26)
* [Bump LanguageExt.Core to 4.4.9](https://github.com/akkadotnet/Akka.Persistence.Sql/pull/411)
* [Fix long delay during JournalSequenceActor initialization](https://github.com/akkadotnet/Akka.Persistence.Sql/pull/415)

#### 1.5.25 June 17th 2024 ####

* [Bump AkkaVersion to 1.5.25](https://github.com/akkadotnet/akka.net/releases/tag/1.5.25)
* [Bump Akka.Hosting version to 1.5.25](https://github.com/akkadotnet/Akka.Hosting/releases/tag/1.5.25)

#### 1.5.24 June 13th 2024 ####

* [Bump AkkaVersion to 1.5.24](https://github.com/akkadotnet/akka.net/releases/tag/1.5.24)
* [Bump Akka.Hosting version to 1.5.24](https://github.com/akkadotnet/Akka.Hosting/releases/tag/1.5.24)
* [Fix missing Query configuration value override for non-default journal ids](https://github.com/akkadotnet/Akka.Persistence.Sql/pull/404)
* [Bump System.Reactive.Linq version to 6.0.1](https://github.com/akkadotnet/Akka.Persistence.Sql/pull/389)

#### 1.5.20 May 8th 2024 ####

* [Fix missing take and optimize captures](https://github.com/akkadotnet/Akka.Persistence.Sql/pull/347)
* [Bump Linq2Db to 5.4.1](https://github.com/akkadotnet/Akka.Persistence.Sql/pull/356)
* [Bump AkkaVersion to 1.5.20](https://github.com/akkadotnet/Akka.Persistence.Sql/pull/371)
* [Bump Akka.Hosting version to 1.5.20](https://github.com/akkadotnet/Akka.Persistence.Sql/pull/371)
* [Bump FluentMigrator version to 5.2.0](https://github.com/akkadotnet/Akka.Persistence.Sql/pull/372)
* [Bump LanguageExt.Core version to 4.4.8](https://github.com/akkadotnet/Akka.Persistence.Sql/pull/365)

#### 1.5.14-alpha April 19 2024 ###
* [Migrate build system to FAKE v6 and dotnet 8](https://github.com/akkadotnet/Akka.Persistence.Sql/pull/361)

#### 1.5.13 September 28 2023 ###

* [Update Akka.NET to 1.5.13](https://github.com/akkadotnet/akka.net/releases/tag/1.5.13)
* [Fix missing Persistence.Query configuration in legacy HOCON configuration mode](https://github.com/akkadotnet/Akka.Persistence.Sql/pull/317)
* [Bump Akka.Hosting version to 1.5.12.1](https://github.com/akkadotnet/Akka.Persistence.Sql/pull/312)
* [Bump Language.Ext.Core version to 4.4.4](https://github.com/akkadotnet/Akka.Persistence.Sql/pull/315)
* [Bump System.Reactive.Linq version to 6.0.0](https://github.com/akkadotnet/Akka.Persistence.Sql/pull/260)
* [Bump linq2db version to 5.2.2](https://github.com/akkadotnet/Akka.Persistence.Sql/pull/266)

#### 1.5.12 August 8 2023 ###

Akka.Persistence.Sql is now out of beta and ready for general use.

#### 1.5.12-beta1 August 4 2023 ###

* [Update Akka.NET to 1.5.12](https://github.com/akkadotnet/akka.net/releases/tag/1.5.12)

#### 1.5.9-beta1 July 20 2023 ###

* [Update Akka.NET to 1.5.9](https://github.com/akkadotnet/akka.net/releases/tag/1.5.9)
* [Bump Akka.Hosting to 1.5.8.1](https://github.com/akkadotnet/Akka.Persistence.Sql/pull/276)
* [Persistence.Query: Fix invalid generated HOCON config](https://github.com/akkadotnet/Akka.Persistence.Sql/pull/283)

#### 1.5.4-beta1 April 25 2023 ###

* [Update Akka.NET from 1.5.2 to 1.5.4](https://github.com/akkadotnet/akka.net/releases/tag/1.5.4)
* [Add per SQL transaction isolation level support](https://github.com/akkadotnet/Akka.Persistence.Sql/pull/207)

Added transaction for every SQL queries with adjustable isolation level for read and write operations. You can go to the [official Microsoft documentation](https://learn.microsoft.com/en-us/dotnet/api/system.data.isolationlevel?#fields) to read more about these transaction isolation level settings.

Four new HOCON settings are introduced:
* `akka.persistence.journal.sql.read-isolation-level`
* `akka.persistence.journal.sql.write-isolation-level`
* `akka.persistence.snapshot-store.sql.read-isolation-level`
* `akka.persistence.snapshot-store.sql.write-isolation-level`

In Akka.Persistence.Sql.Hosting, These settings can be set programmatically through these new properties:

* `SqlJournalOptions.ReadIsolationLevel`
* `SqlJournalOptions.WriteIsolationLevel`
* `SqlSnapshotOptions.ReadIsolationLevel`
* `SqlSnapshotOptions.WriteIsolationLevel`

> **NOTE**
> 
> Currently, there is a bug with Linq2Db and MySql implementation that can cause the SQL generator to throw an exception if you use the default `IsolationLevel.Unspecified` setting. Please use `IsolationLevel.ReadCommitted` if this happens to you.

#### 1.5.2-beta3 April 19 2023 ###

> **NOTE: Database schema changes**
>
> 1.5.2-beta2 package should be considered as deprecated. If you experimented with 1.5.2-beta1 and/or 1.5.2-beta2, you will need to drop existing persistence tables and recreate them using 1.5.2-beta3

* [Fix SQL scripts for database table constraint and indices](https://github.com/akkadotnet/Akka.Persistence.Sql/pull/220)
* [Add official MySql support](https://github.com/akkadotnet/Akka.Persistence.Sql/pull/221)
* [Optimize sequence number and tag query](https://github.com/akkadotnet/Akka.Persistence.Sql/pull/222)
* [Optimize tag query by avoiding multiple DB queries](https://github.com/akkadotnet/Akka.Persistence.Sql/pull/223)
* [Add missing migration support to hosting extension method](https://github.com/akkadotnet/Akka.Persistence.Sql/pull/225)

This beta version introduces database schema optimization to:
* Improve the tag table based query performance even more.
* Improve inter-compatibility with other SQL persistence plugins.

**Tag Query Benchmark**

Benchmark is performed on a worst possible scenario:
* Event journal table with 3 million row entries
* Tagged events near the end of the table
* Numbers are measured as the time required to complete one operation (complete retrieval of N tagged events).

| Tag Count |  TagMode |         Mean |      Error |     StdDev |
|-----------|--------- |-------------:|-----------:|-----------:|
| 10        |      Csv | 1,760.393 ms | 27.1970 ms | 25.4401 ms |
| 100       |      Csv | 1,766.355 ms | 25.0182 ms | 23.4021 ms |
| 1000      |      Csv | 1,755.960 ms | 33.8171 ms | 34.7276 ms |
| 10000     |      Csv | 1,905.026 ms | 22.3564 ms | 20.9122 ms |
| 10        | TagTable |     2.336 ms |  0.0389 ms |  0.0344 ms |
| 100       | TagTable |     3.943 ms |  0.0705 ms |  0.0660 ms |
| 1000      | TagTable |    18.597 ms |  0.3570 ms |  0.3506 ms |
| 10000     | TagTable |   184.446 ms |  3.3447 ms |  2.9650 ms |

#### 1.5.2-beta2 April 14 2023 ###

> **NOTE: Database schema changes**
> 
> 1.5.2-beta1 package should be considered as deprecated. If you experimented with 1.5.2-beta1, you will need to drop existing persistence tables and recreate them using 1.5.2-beta2

* [Fix event journal table and tag table constraints and indices](https://github.com/akkadotnet/Akka.Persistence.Sql/pull/211)
* [Fix snapshot table constraints and indices](https://github.com/akkadotnet/Akka.Persistence.Sql/pull/216)

This beta version introduces database schema optimization to:
* Improve the tag table based query performance, without compromising overall persistence performance.
* Improve inter-compatibility with other SQL persistence plugins. 

**Tag Query Benchmark**

Benchmark is performed on a worst possible scenario:
* Event journal table with 3 million row entries
* Tagged events near the end of the table

|        Tag Count |  TagMode |         Mean |      Error |     StdDev |
|-----------------:|--------- |-------------:|-----------:|-----------:|
|               10 |      Csv | 1,746.621 ms | 27.8946 ms | 29.8469 ms |
|              100 |      Csv | 1,724.465 ms | 25.4638 ms | 23.8189 ms |
|             1000 |      Csv | 1,723.063 ms | 26.2311 ms | 24.5366 ms |
|            10000 |      Csv | 1,873.467 ms | 26.1173 ms | 23.1523 ms |
|               10 | TagTable |     3.201 ms |  0.0633 ms |  0.1479 ms |
|              100 | TagTable |     5.163 ms |  0.1018 ms |  0.1358 ms |
|             1000 | TagTable |    25.545 ms |  0.4952 ms |  0.4864 ms |
|            10000 | TagTable |   441.877 ms |  3.5410 ms |  2.9569 ms |

#### 1.5.2-beta1 April 12 2023 ###

> **NOTE: This beta release is intended for greenfield projects only.**
>
> Until backward compatibility is properly tested and documented, it is recommended to use this plugin only on new greenfield projects that does not rely on existing persisted data.

Akka.Persistence.Sql is a successor of Akka.Persistence.Linq2Db. It is being retooled to provide a better inter-compatibility with other SQL based Akka.Persistence plugin family.

Currently supported database family:
* Microsoft SQL Server
* MS SQLite
* System.Data.SQLite
* PostgreSQL using binary payload

**Akka.Hosting Extension Setup**

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

ProviderName is a string constant defining the database type to connect to, valid values are defined inside `LinqToDB.ProviderName` static class. Refer to the Members of [`LinqToDb.ProviderName`](https://linq2db.github.io/api/LinqToDB.ProviderName.html) for included providers.

**HOCON Configuration Setup**

```hocon
akka.persistence {
    journal {
        plugin = "akka.persistence.journal.sql"
        sql {
            connection-string = "{database-connection-string}"
            provider-name = "{provider-name}"
        }
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
