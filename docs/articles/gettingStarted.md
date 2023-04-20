---
uid: sql-getting-started
title: Getting Started
---

# Getting Started

## The Easy `Akka.Hosting` Way

> [!NOTE]
> `connectionString` and `providerName` parameters are mandatory

Assuming a Microsoft SQL Server 2019 setup:
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

> [!NOTE]
> For best performance, one should use the most specific provider name possible. i.e. `LinqToDB.ProviderName.SqlServer2019` instead of `LinqToDB.ProviderName.SqlServer`. Otherwise certain provider detections have to run more frequently which may impair performance slightly.

## The Classic HOCON Way

> [!NOTE]
> `connection-string` and `provider-name` properties are mandatory

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
* **provider-name**: A case-sensitive string constant defining the database type to connect to, valid values are defined inside `LinqToDB.ProviderName` static class. Refer to the Members of [`LinqToDb.ProviderName`](https://linq2db.github.io/api/LinqToDB.ProviderName.html) for included providers.

> [!NOTE]
> For best performance, one should use the most specific provider name possible. i.e. `LinqToDB.ProviderName.SqlServer2019` instead of `LinqToDB.ProviderName.SqlServer`. Otherwise certain provider detections have to run more frequently which may impair performance slightly.