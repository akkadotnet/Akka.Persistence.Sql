---
uid: sql-configuration
title: Configuration 
---

# Configuration

Please note that you -must- provide a Connection String (`connection-string`) and Provider name (`provider-name`).

## `parallelism`

Controls the number of Akka.Streams Queues used to write to the DB. Default in JVM is `8`. We use `3`

  - For SQL Server, Based on testing `3` is a fairly optimal number in .NET and and thus chosen as default. You may wish to adjust up if you are dealing with a large number of actors.

    Testing indicates that `2` will provide performance on par or better than both batching and non-batching journal.

  - For SQLite, you may want to just put `1` here, because SQLite allows at most a single writer at a time even in WAL mode.
   
    Keep in mind there may be some latency/throughput trade-offs if your write-set gets large.
       
Note that unless `materializer-dispatcher` is changed, by default these run on the thread pool, not on dedicated threads. Setting this number too high may steal work from other actors. 

It's worth noting that LinqToDb's Bulk Copy implementations are very efficient here, since under many DBs the batch can be done in a single async round-trip.

## `materializer-dispatcher` 

May be used to change the dispatcher that the Akka.Streams Queues use for scheduling.

You can define a different dispatcher here if worried about stealing from the thread-pool, for instance a dedicated thread-pool dispatcher.

## `delete-compatibility-mode` 

specifies to perform deletes in a way that is compatible with Akka.Persistence.Sql.Common. This will use a journal metadata table

## `use-clone-connection` 
is a bit of a hack. Currently Linq2Db has a performance penalty for custom mapping schemas. Cloning the connection is faster but may not work for all scenarios.
    
tl;dr - If a password or similar is in the connection string, leave `use-clone-connection` set to `false`. If you don't have a password or similar, run some tests with it set to `true`. You'll see improved write and read performance.

# Batching options

## `batch-size`

Controls the maximum size of the batch used in the Akka.Streams Batch. A single batch is written to the DB in a transaction, with 1 or more round trips.

If more than `batch-size` is in a single `AtomicWrite`, That atomic write will still be atomic, just treated as it's own batch.

## `db-round-trip-max-batch-size` 

Tries to hint to Linq2Db multirow insert the maximum number of rows to send in a round-trip to the DB.
  - multiple round-trips will still be contained in a single transaction.
  - You will want to Keep this number higher than `batch-size`, if you are persisting lots of events with `PersistAll/(Async)`.
  - 
## `prefer-parameters-on-multirow-insert` 

Controls whether Linq2Db will try to use parameters instead of building raw strings for inserts.

Linq2Db is incredibly fast and memory efficient at building binary strings. In most cases, this will be faster than the cost of parsing/marshalling parameters by ADO and the DB.

## Table Configuration

Note that Tables/Columns will be created with the casing provided, and selected in the same way (i.e. if using a DB with case sensitive columns, be careful!)

# HOCON Default Configuration Reference

## Journal And Query

> [!NOTE]
> Please note that you -must- provide a Connection String and Provider name.

```hocon
akka.persistence {
  journal {
    sql {
      class = "Akka.Persistence.Sql.Journal.SqlWriteJournal, Akka.Persistence.Sql"
      plugin-dispatcher = "akka.persistence.dispatchers.default-plugin-dispatcher"
      
      # Connection String is Required!
      connection-string = ""

      # Provider name is required!
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
      # to help atomic writes be faster
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
          # A flag to indicate if the writer_uuid column should 
          # be generated and be populated in run-time.
          # Notes: 
          #   1. The column will only be generated if auto-initialize is 
          #      set to true.
          #   2. This feature is Akka.Persistence.Sql specific, setting 
          #      this to true will break backward compatibility with 
          #      databases generated by other Akka.Persistence plugins.
          #   3. To make this feature work with legacy plugins, 
          #      you will have to alter the old journal table:
          #        ALTER TABLE [journal_table_name] ADD [writer_uuid_column_name] VARCHAR(128);
          #   4. If set to true, the code will not check for backward 
          #      compatibility. It will expect  that the `writer-uuid` 
          #      column to be present inside the journal table.
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

## Snapshot Store

> [!NOTE]
> Please note that you -must- provide a Connection String and Provider name.

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