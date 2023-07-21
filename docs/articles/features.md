---
uid: sql-features
title: Features/Architecture 
---

# Features/Architecture

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

- Tag table Support:
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
    - With `JournalSpec` and `JournalPerfSpec` passing for MS SQL Server, Microsoft.Data.SQLite, PostgreSQL, and MySql.
- Snapshot Store
    - With `SnapshotStoreSpec` passing for MS SQL Server, Microsoft.Data.SQLite, PostgreSQL, and MySql.
- Configuration
    - Custom provider configurations are supported.
- Compatibility with existing Akka.Persistence plugins is implemented via `table-mapping` setting.

## Incomplete

- Tests for Schema Usage