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
