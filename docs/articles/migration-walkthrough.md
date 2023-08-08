# Migration Walkthrough

> ### Video
>
> See the accompanying Youtube video [here](https://youtu.be/gSmqUrVHPq8)

In this walkthrough, we will be migrating an existing simulated Akka.NET cluster from using `Akka.Persistence.SqlServer` persistence plugin to `Akka.Persistence.Sql`.

For this demo, we will be using one of the code samples provided in the [Petabridge Akka.NET code sample repository](https://github.com/petabridge/akkadotnet-code-samples/), specifically the [sharding-sqlserver](https://github.com/petabridge/akkadotnet-code-samples/tree/master/src/clustering/sharding-sqlserver) sample code.

All of the changes are explained inside the [Migration Guide](migration.md)

<!-- TOC start -->
- [Requirements](#requirements)
- [Setup](#setup)
    * [1. Clone The Sample Code Repository](#1-clone-the-sample-code-repository)
    * [2. Start The Microsoft Sql Server Docker Container](#2-start-the-microsoft-sql-server-docker-container)
    * [3. Seed The Database](#3-seed-the-database)
    * [4. Duplicate The SqlSharding.Host Project](#4-duplicate-the-sqlshardinghost-project)
- [Migrating To `Akka.Persistence.Sql` Full Compatibility Mode](#migrating-to-akkapersistencesql-full-compatibility-mode)
    * [Modify The New SqlSharding.Host.Migration Project](#modify-the-new-sqlshardinghostmigration-project)
        + [`SqlSharding.Sql.Migration.csproj`](#sqlshardingsqlmigrationcsproj)
        + [`Program.cs`](#programcs)
            - [Header Changes](#header-changes)
            - [Replace Cluster Sharding Persistence Configuration](#replace-cluster-sharding-persistence-configuration)
            - [Replace Persistence Configuration](#replace-persistence-configuration)
        + [`ProductIndexActor.cs`](#productindexactorcs)
        + [`SoldProductIndexActor.cs`](#soldproductindexactorcs)
        + [`WarningEventIndexActor.cs`](#warningeventindexactorcs)
    * [Run The Application](#run-the-application)
- [Enable WriterUuid Feature](#enable-writeruuid-feature)
    * [Modify The Database Schema](#modify-the-database-schema)
    * [Modify `Program.cs`](#modify-programcs)
    * [Run The Application](#run-the-application-1)
- [Upgrade To Tag Table](#upgrade-to-tag-table)
    * [Migrate Database To Support Tag Table](#migrate-database-to-support-tag-table)
    * [Modify `Program.cs`](#modify-programcs-1)
    * [Delete The `SqlSharding.Host` And `SqlSharding.Sql.Host` Project](#delete-the-sqlshardinghost-and-sqlshardingsqlhost-project)
    * [Confirm Tag Table Is Working (Optional)](#confirm-tag-table-is-working-optional)
    * [Run The Application](#run-the-application-2)
- [Done!](#done)
<!-- TOC end -->

# Requirements

This walkthrough will require some tools to be installed on your computer.
- [Microsoft .NET SDK](https://dotnet.microsoft.com/en-us/download)
- [Git](https://git-scm.com/downloads)
- [Docker](https://www.docker.com/products/docker-desktop/)
- [go-sqlcmd](https://learn.microsoft.com/en-us/sql/tools/sqlcmd/go-sqlcmd-utility?view=sql-server-ver16&tabs=windows)
- [Powershell](https://learn.microsoft.com/en-us/powershell/scripting/install/installing-powershell-on-windows?view=powershell-7.3)

[Back to Top](#migration-walkthrough)
# Setup

## 1. Clone The Sample Code Repository

```powershell
PS C:\> mkdir AkkaTutorial
PS C:\> cd AkkaTutorial
PS C:\AkkaTutorial> git clone https://github.com/petabridge/akkadotnet-code-samples.git
PS C:\AkkaTutorial> cd .\akkadotnet-code-samples\src\clustering\sharding-sqlserver\
PS C:\AkkaTutorial\akkadotnet-code-samples\src\clustering\sharding-sqlserver>
```

From this point forward, all console commands will be executed from inside this directory path.

[Back to Top](#migration-walkthrough)
## 2. Start The Microsoft Sql Server Docker Container

This script will start the Microsoft SqlServer Docker container

```powershell
.\start-dependencies.cmd
```

[Back to Top](#migration-walkthrough)
## 3. Seed The Database

- In two different console windows, run these two projects:

  ```powershell
  dotnet run --project .\SqlSharding.WebApp\SqlSharding.WebApp.csproj
  ```
  
  ```powershell
  dotnet run --project .\SqlSharding.Host\SqlSharding.Host.csproj -- seed-db
  ```

- Wait for the seeding process to complete (should take less than 10 seconds)
- Open `https://localhost:5001` in a browser to make sure that everything is working
- Stop both application by pressing `Ctrl-C` on both console windows.

[Back to Top](#migration-walkthrough)
## 4. Duplicate The SqlSharding.Host Project

We will be migrating the `SqlSharding.Host` project to use `Akka.Persistence.Sql` persistence plugin. Copy the project and register it with the solution file:

```powershell
Copy-Item -Path .\SqlSharding.Host -Destination .\SqlSharding.Host.Migration -Recurse
Rename-Item -Path .\SqlSharding.Host.Migration\SqlSharding.Host.csproj -NewName SqlSharding.Host.Migration.csproj
dotnet sln add .\SqlSharding.Host.Migration\SqlSharding.Host.Migration.csproj
```

We will be working with the `SqlSharding.Host.Migration` project from now on.

[Back to Top](#migration-walkthrough)
# Migrating To `Akka.Persistence.Sql` Full Compatibility Mode

## Modify The New SqlSharding.Host.Migration Project

At the end of this step, the content of the `SqlSharding.Host.Migration` project should be identical to the `SqlSharding.Sql.Host` project

[Back to Top](#migration-walkthrough)
### `SqlSharding.Sql.Migration.csproj`

- Remove the package reference to `Akka.Persistence.SqlServer.Hosting`
- Add package references to `Akka.Persistence.Sql.Hosting` and `Microsoft.Data.SqlClient`

The package reference section should look like this after the modification:
```xml
<ItemGroup>
  <PackageReference Include="Akka.Persistence.Sql.Hosting" Version="1.5.12-beta1" />
  <PackageReference Include="Microsoft.Data.SqlClient" Version="5.1.1" />
  <PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="$(MicrosoftExtensionsVersion)" />
  <PackageReference Include="Microsoft.Extensions.Configuration.EnvironmentVariables" Version="$(MicrosoftExtensionsVersion)" />
  <PackageReference Include="Petabridge.Cmd.Cluster" Version="$(PbmVersion)" />
  <PackageReference Include="Petabridge.Cmd.Cluster.Sharding" Version="$(PbmVersion)" />
  <PackageReference Include="Petabridge.Cmd.Host" Version="$(PbmVersion)" />
</ItemGroup>
```

[Back to Top](#migration-walkthrough)
### `Program.cs`

#### Header Changes
Replace
```csharp
using Akka.Persistence.SqlServer.Hosting;
```
with
```csharp
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Hosting;
using LinqToDB;
using SqlJournalOptions = Akka.Persistence.Sql.Hosting.SqlJournalOptions;
using SqlSnapshotOptions = Akka.Persistence.Sql.Hosting.SqlSnapshotOptions;
```

#### Replace Cluster Sharding Persistence Configuration
- Replace
  ```csharp
  var shardingJournalOptions = new SqlServerJournalOptions(
      isDefaultPlugin: false, 
      identifier: "sharding")
  {
      ConnectionString = connectionString,
      TableName = "ShardingEventJournal", 
      MetadataTableName = "ShardingMetadata",
      AutoInitialize = true
  };
  ```
  with
  ```csharp
  var shardingJournalDbOptions = JournalDatabaseOptions.SqlServer;
  shardingJournalDbOptions.JournalTable!.TableName = "ShardingEventJournal";
  shardingJournalDbOptions.MetadataTable!.TableName = "ShardingMetadata";

  var shardingJournalOptions = new SqlJournalOptions(
      isDefaultPlugin: false, 
      identifier: "sharding")
  {
      ConnectionString = connectionString,
      ProviderName = ProviderName.SqlServer2019,
      DatabaseOptions = shardingJournalDbOptions,
      TagStorageMode = TagMode.Csv,
      DeleteCompatibilityMode = true,
      AutoInitialize = false
  };
  ```

- Replace
  ```csharp
  var shardingSnapshotOptions = new SqlServerSnapshotOptions(
      isDefaultPlugin: false, 
      identifier: "sharding")
  {
      ConnectionString = connectionString,
      TableName = "ShardingSnapshotStore",
      AutoInitialize = true
  };
  ```
  with
  ```csharp
  var shardingSnapshotDbOptions = SnapshotDatabaseOptions.SqlServer;
  shardingSnapshotDbOptions.SnapshotTable!.TableName = "ShardingSnapshotStore";
  
  var shardingSnapshotOptions = new SqlSnapshotOptions(
      isDefaultPlugin: false, 
      identifier: "sharding")
  {
      ConnectionString = connectionString,
      ProviderName = ProviderName.SqlServer2019,
      DatabaseOptions = shardingSnapshotDbOptions, 
      AutoInitialize = false
  };
  ```

[Back to Top](#migration-walkthrough)
#### Replace Persistence Configuration

Replace
```csharp
.WithSqlServerPersistence(
    connectionString: connectionString,
    journalBuilder: builder =>
    {
        builder.AddWriteEventAdapter<MessageTagger>("product-tagger", new[] { typeof(IProductEvent) });
    })
```
with
```csharp
.WithSqlPersistence(
    connectionString: connectionString,
    providerName: ProviderName.SqlServer2019,
    databaseMapping: DatabaseMapping.SqlServer,
    tagStorageMode: TagMode.Csv,
    deleteCompatibilityMode: true,
    useWriterUuidColumn: false,
    autoInitialize: false,
    journalBuilder: builder =>
    {
        builder.AddWriteEventAdapter<MessageTagger>("product-tagger", new[] { typeof(IProductEvent) });
    })
```

[Back to Top](#migration-walkthrough)
### `ProductIndexActor.cs`

Replace `using Akka.Persistence.Query.Sql;` with `using Akka.Persistence.Sql.Query;`

[Back to Top](#migration-walkthrough)
### `SoldProductIndexActor.cs`

Replace `using Akka.Persistence.Query.Sql;` with `using Akka.Persistence.Sql.Query;`

[Back to Top](#migration-walkthrough)
### `WarningEventIndexActor.cs`

Replace `using Akka.Persistence.Query.Sql;` with `using Akka.Persistence.Sql.Query;`

[Back to Top](#migration-walkthrough)
## Run The Application

At this point, the migration project is in `Akka.Persistence.Sql` full compatibility mode. All 3 version of the `Host` project can co-exist in the same Akka.NET cluster.

- In three different console windows, run all of the projects:

  ```powershell
  dotnet run --project .\SqlSharding.WebApp\SqlSharding.WebApp.csproj
  ```

  ```powershell
  dotnet run --project .\SqlSharding.Host\SqlSharding.Host.csproj
  ```

  ```powershell
  dotnet run --project .\SqlSharding.Host.Migration\SqlSharding.Host.Migration.csproj
  ```

- Open `https://localhost:5001` in a browser to make sure that everything is working
- Stop all application by pressing `Ctrl-C` on all console windows.

[Back to Top](#migration-walkthrough)
# Enable WriterUuid Feature

To go straight to this step, you can directly check out the git branch:
```powershell
git checkout Migration_01
```

[Back to Top](#migration-walkthrough)
## Modify The Database Schema

In a Powershell console, execute:
```powershell
sqlcmd -S "localhost,1533" -d Akka -U sa -P "yourStrong(!)Password" -Q "ALTER TABLE [dbo].[EventJournal] ADD writer_uuid VARCHAR(128)"
```

[Back to Top](#migration-walkthrough)
## Modify `Program.cs`

Inside `Program.cs`, change the `useWriterUuidColumn` argument parameter of the `.WithSqlPersistence()` to `true`.
```csharp
.WithSqlPersistence(
    connectionString: connectionString,
    providerName: ProviderName.SqlServer2019,
    databaseMapping: DatabaseMapping.SqlServer,
    tagStorageMode: TagMode.Csv,
    deleteCompatibilityMode: true,
    useWriterUuidColumn: true, // Change this parameter value to true
    autoInitialize: false,
    journalBuilder: builder =>
    {
        builder.AddWriteEventAdapter<MessageTagger>("product-tagger", new[] { typeof(IProductEvent) });
    })
```

[Back to Top](#migration-walkthrough)
## Run The Application

- In two different console windows, run the projects:

  ```powershell
  dotnet run --project .\SqlSharding.WebApp\SqlSharding.WebApp.csproj
  ```

  ```powershell
  dotnet run --project .\SqlSharding.Host.Migration\SqlSharding.Host.Migration.csproj
  ```

- Open `https://localhost:5001` in a browser to make sure that everything is working
- Stop all application by pressing `Ctrl-C` on all console windows.

[Back to Top](#migration-walkthrough)
# Upgrade To Tag Table

To go straight to this step, you can directly check out the git branch:
```powershell
git checkout Migration_02
```

[Back to Top](#migration-walkthrough)
## Migrate Database To Support Tag Table

1. Download these SQL script files:
   - [1_Migration_Setup.sql](https://github.com/akkadotnet/Akka.Persistence.Sql/blob/dev/scripts/SqlServer/1_Migration_Setup.sql)
   - [2_Migration.sql](https://github.com/akkadotnet/Akka.Persistence.Sql/blob/dev/scripts/SqlServer/2_Migration.sql)
   - [3_Post_Migration_Cleanup.sql](https://github.com/akkadotnet/Akka.Persistence.Sql/blob/dev/scripts/SqlServer/3_Post_Migration_Cleanup.sql)
2. Copy these SQL script into a folder called `Scripts`
3. Execute the scripts in order:
    ```powershell
    sqlcmd -S "localhost,1533" -d Akka -U sa -P "yourStrong(!)Password" -i .\Scripts\1_Migration_Setup.sql
    
    sqlcmd -S "localhost,1533" -d Akka -U sa -P "yourStrong(!)Password" -i .\Scripts\2_Migration.sql
    
    sqlcmd -S "localhost,1533" -d Akka -U sa -P "yourStrong(!)Password" -i .\Scripts\3_Post_Migration_Cleanup.sql
    ```

[Back to Top](#migration-walkthrough)
## Modify `Program.cs`

Inside `Program.cs`, change the `tagStorageMode` argument parameter of the `.WithSqlPersistence()` to `TagMode.TagTable`.

[Back to Top](#migration-walkthrough)
## Delete The `SqlSharding.Host` And `SqlSharding.Sql.Host` Project

The `SqlSharding.Host` and `SqlSharding.Sql.Host` project is not compatible with `SqlSharding.Host.Migration` anymore, you can delete these projects.

```powershell
dotnet sln remove .\SqlSharding.Host\SqlSharding.Host.csproj
dotnet sln remove .\SqlSharding.Sql.Host\SqlSharding.Sql.Host.csproj
Remove-Item -Recurse -Force .\SqlSharding.Host\
Remove-Item -Recurse -Force .\SqlSharding.Sql.Host\
```

[Back to Top](#migration-walkthrough)
## Confirm Tag Table Is Working (Optional)

To confirm that the tag table is working, lets delete the old tag data:
```powershell
sqlcmd -S "localhost,1533" -d Akka -U sa -P "yourStrong(!)Password" -Q "UPDATE [dbo].[EventJournal] SET Tags = NULL"
```

[Back to Top](#migration-walkthrough)
## Run The Application

- In two different console windows, run the projects:

  ```powershell
  dotnet run --project .\SqlSharding.WebApp\SqlSharding.WebApp.csproj
  ```

  ```powershell
  dotnet run --project .\SqlSharding.Host.Migration\SqlSharding.Host.Migration.csproj
  ```

- Open `https://localhost:5001` in a browser to make sure that everything is working
- Stop all application by pressing `Ctrl-C` on all console windows.

[Back to Top](#migration-walkthrough)
# Done!

To go straight to this step, you can directly check out the git branch:
```powershell
git checkout Migration_03
```
