# Akka.Persistence.Sql

A Cross-SQL-DB Engine Akka.Persistence plugin with broad database compatibility thanks to [Linq2Db](https://linq2db.github.io/).

This is a port of the amazing [akka-persistence-jdbc](https://github.com/akka/akka-persistence-jdbc) package from Scala, with a few improvements based on C# as well as our choice of data library.

Please read the documentation carefully. Some features may be specific to use case and have trade-offs (namely, compatibility modes)

## Notes

### This Is Still a Beta

Please note this is still considered 'work in progress' and only used if one understands the risks. While the TCK Specs pass you should still test in a 'safe' non-production environment carefully before deciding to fully deploy.

### Suitable For Greenfield Projects Only

Until backward compatibility is properly tested and documented, it is recommended to use this plugin only on new greenfield projects that does not rely on existing persisted data.

## Setup

### The Easy Way, Using `Akka.Hosting`

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

### The Classic Way, Using HOCON

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

### Supported By Linq2Db But Untested In Akka.Persistence 
- MySql
- Firebird
- Microsoft Access OleDB
- Microsoft Access ODBC
- IBM DB2
- Informix
- Oracle
- Sybase
- SAP HANA
- ClickHouse

## Features/Architecture

- Akka.Streams used aggressively for tune-able blocking overhead.
  - Up to `parallelism` writers write pushed messages
  - While writers are busy, messages are buffered up to `buffer-size` entries
  - Batches are flushed to Database at up to `batch-size`
    - For most DBs this will be done in a single built Multi-row-insert statement
    - PersistAll groups larger than `batch-size` will still be done as a single contiguous write

- Linq2Db usage for easier swapping of backend DBs.
  - `provider-name` is a [`LinqToDb.ProviderName`](https://linq2db.github.io/api/LinqToDB.ProviderName.html)
    - This handles DB Type mapping and Dialect-specific query building

- language-ext is used in place of Systems.Collections.Immutable where appropriate
  - Lower memory allocations, improve performance

- Recovery is also batched:
  - Up to `replay-batch-size` messages are fetched at a time
  - This is to both lower size of records fetched in single pass, as well as to prevent pulling too much data into memory at once.
  - If more messages are to be recovered, additional passes will be made.

- Attempts to stay in spirit and Structure of JDBC Port with a few differences:
  - Linq2Db isn't a Reactive Streams Compatible DB Provider; this means Some of the Query architecture is different, to deal with required semantic changes (i.e. Connection scoping)
  - Both due to above and differences between Scala and C#, Some changes have been made for optimal performance (i.e. memory, GC)
    - Classes used in place of ValueTuples in certain areas
    - We don't have separate Query classes at this time. This can definitely be improved in future
    - A couple of places around `WriteMessagesAsync` have had their logic moved to facilitate performance (i.e. use of `await` instead of `ContinueWith`)
  - Backwards Compatibility mode is implemented, to interoperate with existing journals and snapshot stores.

- Tag Table Support:
    - Allows the writing of tags to a separate table to allow for different performance strategies when working with tags.
    - Provides multiple modes of operation for reads and writes, note that there are separate switches for both read and write.
        - Csv: The old behavior, where the comma separated tags are held in a column
        - TagTable: will use the tag table for Read/Write
        - Both: (write only) will write to both csv column and tag table
    - Migration should be possible via the following ways:
        1. Run Migration script. The migration script will create new tables and migrate the legacy CSV column to the new tag table.
        2. Use the migration application.

## Currently Implemented

- Journal
  - With `JournalSpec` and `JournalPerfSpec` passing for MS SQL Server, Microsoft.Data.SQLite, and PostgreSQL
- Snapshot Store
  - With `SnapshotStoreSpec` passing for MS SQL Server, Microsoft.Data.SQLite, PostgreSQL
- Configuration
  - Only Functional tests at this time.
  - Custom provider configurations are supported.
- Compatibility with existing Akka.Persistence plugins is implemented via `table-mapping` setting.

## Incomplete

- Tests for Schema Usage
- Cleanup of Configuration classes/fallbacks.
  - Should still be usable in most common scenarios including multiple configuration instances: see [`SqlServerCustomConfigSpec`](src/Akka.Persistence.Sql.Tests/SqlServer/SQLServerJournalCustomConfigSpec.cs) for test and examples.

## Performance

Tests based on AMD Ryzen 9 3900X, 32GB Ram, Windows 10 Version 22H2.
Databases running on Docker WSL2.

All numbers are in msg/sec.

| Test            |          SqlServer | SqlServer Batching | Linq2Db | vs Normal | vs Batching |
|:----------------|-------------------:|-------------------:|--------:|----------:|------------:|
| Persist         |                304 |                299 |     496 |   163.16% |     165.89% |
| PersistAll      |               1139 |               1275 |    7893 |   692.98% |     619.06% |
| PersistAsync    |               1021 |               1371 |   31813 |  3115.87% |    2320.42% |
| PersistAllAsync |               2828 |               1395 |   29634 |  1047.88% |    2124.30% |
| PersistGroup10  |                986 |               1034 |    1675 |   169.88% |     161.99% |
| PersistGroup100 |               1054 |               1304 |    6249 |   592.88% |     479.22% |
| PersistGroup200 |                990 |               1662 |    8086 |   816.77% |     486.52% |
| PersistGroup25  |               1034 |               1010 |    3054 |   295.36% |     302.38% |
| PersistGroup400 |               1049 |               2113 |    7237 |   689.59% |     342.50% |
| PersistGroup50  |                971 |                980 |    4932 |   507.93% |     503.27% |
| Recovering      |              60516 |              77688 |   64457 |   106.51% |      82.96% |
| Recovering8     |             116401 |             101549 |  103463 |    88.89% |     101.88% |
| RecoveringFour  |              86107 |              73218 |   66512 |    77.24% |      90.84% |
| RecoveringTwo   |              60730 |              53062 |   43325 |    71.34% |      81.65% |

## Sql.Common Compatibility modes

- Delete Compatibility mode is available.
  - This mode will utilize a `journal_metadata` table containing the last sequence number
  - The main table delete is done the same way regardless of delete compatibility mode

**Delete Compatibility mode is expensive.**

- Normal Deletes involve first marking the deleted records as deleted, and then deleting them
  - Table compatibility mode adds an additional InsertOrUpdate and Delete
- **This all happens in a transaction**
  - In SQL Server this can cause issues because of pagelocks/etc.

## Configuration

### Journal

Please note that you -must- provide a Connection String (`connection-string`) and Provider name (`provider-name`).

* `parallelism` controls the number of Akka.Streams Queues used to write to the DB.
  - Default in JVM is `8`. We use `3`
    - For SQL Server, Based on testing `3` is a fairly optimal number in .NET and thusly chosen as the default. You may wish to adjust up if you are dealing with a large number of actors.
      - Testing indicates that `2` will provide performance on par or better than both batching and non-batching journal.
    - For SQLite, you may want to just put `1` here, because SQLite allows at most a single writer at a time even in WAL mode.
      - Keep in mind there may be some latency/throughput trade-offs if your write-set gets large.
  - Note that unless `materializer-dispatcher` is changed, by default these run on the threadpool, not on dedicated threads. Setting this number too high may steal work from other actors.
    - It's worth noting that LinqToDb's Bulk Copy implementations are very efficient here, since under many DBs the batch can be done in a single async round-trip.
- `materializer-dispatcher` may be used to change the dispatcher that the Akka.Streams Queues use for scheduling.
  - You can define a different dispatcher here if worried about stealing from the thread-pool, for instance a Dedicated thread-pool dispatcher.
- `logical-delete` 
  - if `true` will only set the deleted flag for items, i.e. will not actually delete records from DB.
  - if `false` all records are set as deleted, and then all but the top record is deleted. This top record is used for sequence number tracking in case no other records exist in the table.
- `delete-compatibility-mode` specifies to perform deletes in a way that is compatible with Akka.Persistence.Sql.Common.
  - This will use a Journal_Metadata table (or otherwise defined )
  - Note that this setting is independent of `logical-delete`
- `use-clone-connection` is a bit of a hack. Currently Linq2Db has a performance penalty for custom mapping schemas. Cloning the connection is faster but may not work for all scenarios.
  - tl;dr - If a password or similar is in the connection string, leave `use-clone-connection` set to `false`.
  - If you don't have a password or similar, run some tests with it set to `true`. You'll see improved write and read performance.

- Batching options:
  - `batch-size` controls the maximum size of the batch used in the Akka.Streams Batch. A single batch is written to the DB in a transaction, with 1 or more round trips.
    - If more than `batch-size` is in a single `AtomicWrite`, That atomic write will still be atomic, just treated as it's own batch.
  - `db-round-trip-max-batch-size` tries to hint to Linq2Db multirow insert the maximum number of rows to send in a round-trip to the DB.
    - multiple round-trips will still be contained in a single transaction.
    - You will want to Keep this number higher than `batch-size`, if you are persisting lots of events with `PersistAll/(Async)`.
  - `prefer-parameters-on-multirow-insert` controls whether Linq2Db will try to use parameters instead of building raw strings for inserts.
    - Linq2Db is incredibly speed and memory efficient at building binary strings. In most cases, this will be faster than the cost of parsing/marshalling parameters by ADO and the DB.

- For Table Configuration:
  - Note that Tables/Columns will be created with the casing provided, and selected in the same way (i.e. if using a DB with case sensitive columns, be careful!)

```hocon
akka.persistence {
  journal {
    sql {
      class = "Akka.Persistence.Sql.Journal.SqlWriteJournal, Akka.Persistence.Sql"
      plugin-dispatcher = "akka.persistence.dispatchers.default-plugin-dispatcher"
      connection-string = "" # Connection String is Required!

      # Provider name is required.
      # Refer to LinqToDb.ProviderName for values
      # Always use a specific version if possible
      # To avoid provider detection performance penalty
      # Don't worry if your DB is newer than what is listed;
      # Just pick the newest one (if yours is still newer)
      provider-name = ""

      # If true, journal_metadata is created and used for deletes
      # and max sequence number queries.
      # note that there is a performance penalty for using this.
      delete-compatibility-mode = false

      # The database schema, table names, and column names configuration mapping.
      # The details are described in their respective configuration block below.
      # If set to "sqlite", "sql-server", "mysql", or "postgresql",
      # column names will be compatible with legacy Akka.NET persistence sql plugins
      table-mapping = default

      # If more entries than this are pending, writes will be rejected.
      # This setting is higher than JDBC because smaller batch sizes
      # Work better in testing and we want to add more buffer to make up
      # For that penalty.
      buffer-size = 5000

      # Batch size refers to the number of items included in a batch to DB
      #  (In cases where an AtomicWrite is greater than batch-size,
      #   The Atomic write will still be handled in a single batch.)
      # JDBC Default is/was 400 but testing against scenarios indicates
      # 100 is better for overall latency. That said,
      # larger batches may be better if you have A fast/local DB.
      batch-size = 100

      # This batch size controls the maximum number of rows that will be sent
      # In a single round trip to the DB. This is different than the -actual- batch size,
      # And intentionally set larger than batch-size,
      # to help atomicwrites be faster
      # Note that Linq2Db may use a lower number per round-trip in some cases.
      db-round-trip-max-batch-size = 1000

      # Linq2Db by default will use a built string for multi-row inserts
      # Somewhat counterintuitively, this is faster than using parameters in most cases,
      # But if you would prefer parameters, you can set this to true.
      prefer-parameters-on-multirow-insert = false

      # Denotes the number of messages retrieved
      # Per round-trip to DB on recovery.
      # This is to limit both size of dataset from DB (possibly lowering locking requirements)
      # As well as limit memory usage on journal retrieval in CLR
      replay-batch-size = 1000

      # Number of Concurrennt writers.
      # On larger servers with more cores you can increase this number
      # But in most cases 2-4 is a safe bet.
      parallelism = 3

      # If a batch is larger than this number,
      # Plugin will utilize Linq2db's
      # Default bulk copy rather than row-by-row.
      # Currently this setting only really has an impact on
      # SQL Server and IBM Informix (If someone decides to test that out)
      # SQL Server testing indicates that under this number of rows, (or thereabouts,)
      # MultiRow is faster than Row-By-Row.
      max-row-by-row-size = 100

      # Only set to TRUE if unit tests pass with the connection string you intend to use!
      # This setting will go away once https://github.com/linq2db/linq2db/issues/2466 is resolved
      use-clone-connection = true

      # This dispatcher will be used for the Stream Materializers
      # Note that while all calls will be Async to Linq2Db,
      # If your provider for some reason does not support async,
      # or you are a very heavily loaded system,
      # You may wish to provide a dedicated dispatcher instead
      materializer-dispatcher = "akka.actor.default-dispatcher"

      # This setting dictates how journal event tags are being stored inside the database.
      # Valid values:
      #   * Csv 
      #     This value will make the plugin stores event tags in a CSV format in the 
      #     `tags` column inside the journal table. This is the backward compatible
      #     way of storing event tag information.
      #   * TagTable
      #     This value will make the plugin stores event tags inside a separate tag
      #     table to improve tag related query speed.
      tag-write-mode = TagTable

      # The character used to delimit the CSV formatted tag column.
      # This setting is only effective if `tag-write-mode` is set to `Csv`
      tag-separator = ";"

      # should corresponding journal table be initialized automatically
      # if delete-compatibility-mode is true, both tables are created
      # if delete-compatibility-mode is false, only journal table will be created.
      auto-initialize = true

      # if true, a warning will be logged
      # if auto-init of tables fails.
      # set to false if you don't want this warning logged
      # perhaps if running CI tests or similar.
      warn-on-auto-init-fail = true

      dao = "Akka.Persistence.Sql.Journal.Dao.ByteArrayJournalDao, Akka.Persistence.Sql"

      # Default serializer used as manifest serializer when applicable and payload serializer when
      # no specific binding overrides are specified.
      # If set to null, the default `System.Object` serializer is used.
      serializer = null

      # Default table name and column name mapping
      # Use this if you're not migrating from old Akka.Persistence plugins
      default {
        # If you want to specify a schema for your tables, you can do so here.
        schema-name = null

        journal {
          # A flag to indicate if the writer_uuid column should be generated and be populated in run-time.
          # Notes: 
          #   1. The column will only be generated if auto-initialize is set to true.
          #   2. This feature is Akka.Persistence.Sql specific, setting this to true will break 
          #      backward compatibility with databases generated by other Akka.Persistence plugins.
          #   3. To make this feature work with legacy plugins, you will have to alter the old
          #      journal table:
          #        ALTER TABLE [journal_table_name] ADD [writer_uuid_column_name] VARCHAR(128);
          #   4. If set to true, the code will not check for backward compatibility. It will expect 
          #      that the `writer-uuid` column to be present inside the journal table.
          use-writer-uuid-column = true

          table-name = "journal"
          columns {
            ordering = ordering
            deleted = deleted
            persistence-id = persistence_id
            sequence-number = sequence_number
            created = created
            tags = tags
            message = message
            identifier = identifier
            manifest = manifest
            writer-uuid = writer_uuid
          }
        }

        metadata {
          table-name = "journal_metadata"
          columns {
            persistence-id = persistence_id
            sequence-number = sequence_number
          }
        }

        tag {
          table-name = "tags"
          columns {
            ordering-id = ordering_id
            tag-value = tag
            persistence-id = persistence_id
            sequence-nr = sequence_nr
          }
        }
      }

      # Akka.Persistence.SqlServer compatibility table name and column name mapping
      sql-server {
        schema-name = dbo
        journal {
          use-writer-uuid-column = false
          table-name = "EventJournal"
          columns {
            ordering = Ordering
            deleted = IsDeleted
            persistence-id = PersistenceId
            sequence-number = SequenceNr
            created = Timestamp
            tags = Tags
            message = Payload
            identifier = SerializerId
            manifest = Manifest
          }
        }

        metadata {
          table-name = "Metadata"
          columns {
            persistence-id = PersistenceId
            sequence-number = SequenceNr
          }
        }
      }

      sqlserver = ${akka.persistence.journal.sql.sql-server} # backward compatibility naming

      # Akka.Persistence.Sqlite compatibility table name and column name mapping
      sqlite {
        schema-name = null

        journal {
          use-writer-uuid-column = false
          table-name = "event_journal"
          columns {
            ordering = ordering
            deleted = is_deleted
            persistence-id = persistence_id
            sequence-number = sequence_nr
            created = timestamp
            tags = tags
            message = payload
            identifier = serializer_id
            manifest = manifest
          }
        }

        metadata {
          table-name = "journal_metadata"
          columns {
            persistence-id = persistence_id
            sequence-number = sequence_nr
          }
        }
      }

      # Akka.Persistence.PostgreSql compatibility table name and column name mapping
      postgresql {
        schema-name = public
        journal {
          use-writer-uuid-column = false
          table-name = "event_journal"
          columns {
            ordering = ordering
            deleted = is_deleted
            persistence-id = persistence_id
            sequence-number = sequence_nr
            created = created_at
            tags = tags
            message = payload
            identifier = serializer_id
            manifest = manifest
          }
        }

        metadata {
          table-name = "metadata"
          columns {
            persistence-id = persistence_id
            sequence-number = sequence_nr
          }
        }
      }

      # Akka.Persistence.MySql compatibility table name and column name mapping
      mysql {
        schema-name = null
        journal {
          use-writer-uuid-column = false
          table-name = "event_journal"
          columns {
            ordering = ordering
            deleted = is_deleted
            persistence-id = persistence_id
            sequence-number = sequence_nr
            created = created_at
            tags = tags
            message = payload
            identifier = serializer_id
            manifest = manifest
          }
        }

        metadata {
          table-name = "metadata"
          columns {
            persistence-id = persistence_id
            sequence-number = sequence_nr
          }
        }
      }
    }
  }

  query {
    journal {
      sql {
        class = "Akka.Persistence.Sql.Query.SqlReadJournalProvider, Akka.Persistence.Sql"

        # You should specify your proper sql journal plugin configuration path here.
        write-plugin = ""

        max-buffer-size = 500 # Number of events to buffer at a time.
        refresh-interval = 1s # interval for refreshing

        connection-string = "" # Connection String is Required!

        # This setting dictates how journal event tags are being read from the database.
        # Valid values:
        #   * Csv 
        #     This value will make the plugin read event tags from a CSV formatted string 
        #     `tags` column inside the journal table. This is the backward compatible
        #     way of reading event tag information.
        #   * TagTable
        #     This value will make the plugin read event tags from the tag
        #     table to improve tag related query speed.
        tag-read-mode = TagTable

        journal-sequence-retrieval{
          batch-size = 10000
          max-tries = 10
          query-delay = 1s
          max-backoff-query-delay = 60s
          ask-timeout = 1s
        }

        # Provider name is required.
        # Refer to LinqToDb.ProviderName for values
        # Always use a specific version if possible
        # To avoid provider detection performance penalty
        # Don't worry if your DB is newer than what is listed;
        # Just pick the newest one (if yours is still newer)
        provider-name = ""

        # if set to "sqlite", "sqlserver", "mysql", or "postgresql",
        # Column names will be compatible with Akka.Persistence.Sql
        # You still must set your table name!
        table-mapping = default

        # If more entries than this are pending, writes will be rejected.
        # This setting is higher than JDBC because smaller batch sizes
        # Work better in testing and we want to add more buffer to make up
        # For that penalty.
        buffer-size = 5000

        # Batch size refers to the number of items included in a batch to DB
        # JDBC Default is/was 400 but testing against scenarios indicates
        # 100 is better for overall latency. That said,
        # larger batches may be better if you have A fast/local DB.
        batch-size = 100

        # Denotes the number of messages retrieved
        # Per round-trip to DB on recovery.
        # This is to limit both size of dataset from DB (possibly lowering locking requirements)
        # As well as limit memory usage on journal retrieval in CLR
        replay-batch-size = 1000

        # Number of Concurrennt writers.
        # On larger servers with more cores you can increase this number
        # But in most cases 2-4 is a safe bet.
        parallelism = 3

        # If a batch is larger than this number,
        # Plugin will utilize Linq2db's
        # Default bulk copy rather than row-by-row.
        # Currently this setting only really has an impact on
        # SQL Server and IBM Informix (If someone decides to test that out)
        # SQL Server testing indicates that under this number of rows, (or thereabouts,)
        # MultiRow is faster than Row-By-Row.
        max-row-by-row-size = 100

        # Only set to TRUE if unit tests pass with the connection string you intend to use!
        # This setting will go away once https://github.com/linq2db/linq2db/issues/2466 is resolved
        use-clone-connection = true

        tag-separator = ";"

        dao = "Akka.Persistence.Sql.Journal.Dao.ByteArrayJournalDao, Akka.Persistence.Sql"

        default = ${akka.persistence.journal.sql.default}
        sql-server = ${akka.persistence.journal.sql.sql-server}
        sqlite = ${akka.persistence.journal.sql.sqlite}
        postgresql = ${akka.persistence.journal.sql.postgresql}
        mysql = ${akka.persistence.journal.sql.mysql}
      }
    }
  }
}

```

### Snapshot Store

Please note that you -must- provide a Connection String and Provider name.

```hocon
akka.persistence {
  snapshot-store {
    sql {
      class = "Akka.Persistence.Sql.Snapshot.SqlSnapshotStore, Akka.Persistence.Sql"
      plugin-dispatcher = "akka.persistence.dispatchers.default-plugin-dispatcher"
      connection-string = ""

      # Provider name is required.
      # Refer to LinqToDb.ProviderName for values
      # Always use a specific version if possible
      # To avoid provider detection performance penalty
      # Don't worry if your DB is newer than what is listed;
      # Just pick the newest one (if yours is still newer)
      provider-name = ""

      # Only set to TRUE if unit tests pass with the connection string you intend to use!
      # This setting will go away once https://github.com/linq2db/linq2db/issues/2466 is resolved
      use-clone-connection = true

      # The database schema, table names, and column names configuration mapping.
      # The details are described in their respective configuration block below.
      # If set to "sqlite", "sql-server", "mysql", or "postgresql",
      # column names will be compatible with Akka.Persistence.Sql
      table-mapping = default

      # Default serializer used as manifest serializer when applicable and payload serializer when
      # no specific binding overrides are specified.
      # If set to null, the default `System.Object` serializer is used.
      serializer = null

      dao = "Akka.Persistence.Sql.Snapshot.ByteArraySnapshotDao, Akka.Persistence.Sql"

      # if true, tables will attempt to be created.
      auto-initialize = true

      # if true, a warning will be logged
      # if auto-init of tables fails.
      # set to false if you don't want this warning logged
      # perhaps if running CI tests or similar.
      warn-on-auto-init-fail = true

      default {
        schema-name = null
        snapshot {
          table-name = "snapshot"
          columns {
            persistence-id = persistence_id
            sequence-number = sequence_number
            created = created
            snapshot = snapshot
            manifest = manifest
            serializerId = serializer_id
          }
        }
      }

      sql-server {
        schema-name = dbo
        snapshot {
          table-name = "SnapshotStore"
          columns {
            persistence-id = PersistenceId
            sequence-number = SequenceNr
            created = Timestamp
            snapshot = Snapshot
            manifest = Manifest
            serializerId = SerializerId
          }
        }
      }

      sqlite {
        schema-name = null
        snapshot {
          table-name = "snapshot"
          columns {
            persistence-id = persistence_id
            sequence-number = sequence_nr
            snapshot = payload
            manifest = manifest
            created = created_at
            serializerId = serializer_id
          }
        }
      }

      postgresql {
        schema-name = public
        snapshot {
          table-name = "snapshot_store"
          columns {
            persistence-id = persistence_id
            sequence-number = sequence_nr
            snapshot = payload
            manifest = manifest
            created = created_at
            serializerId = serializer_id
          }
        }
      }

      mysql {
        schema-name = null
        snapshot {
          table-name = "snapshot_store"
          columns {
            persistence-id = persistence_id,
            sequence-number = sequence_nr,
            snapshot = snapshot,
            manifest = manifest,
            created = created_at,
            serializerId = serializer_id,
          }
        }
      }
    }
  }
}
```

## Building this solution

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
