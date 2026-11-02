# Database-side journal retention

These operator-run scripts delete old journal events **and their tag rows**
from inside the database, for deployments that purge history with a scheduled
SQL job (SQL Server Agent, `pg_cron`, or similar) instead of Akka.Persistence's
`DeleteMessages`/`DeleteMessagesTo` API. The plugin's own delete API already
removes an entity's tag rows in the same transaction; these scripts are for
jobs that delete rows outside the plugin.

With `tag-write-mode = TagTable`, the tags table is a secondary index over the
journal: each tag row points to its event through
`tags.ordering_id -> journal.ordering`, and tag rows have no age of their own.
Retention therefore selects what to delete from the **journal** and removes the
matching tag rows together with those events. A job that deletes journal rows
without their tags leaves orphaned tag rows behind, and the tags table keeps
growing.

| Provider | Script |
| --- | --- |
| PostgreSQL | [delete_expired_events.sql](PostgreSql/delete_expired_events.sql) |
| SQL Server | [delete_expired_events.sql](SqlServer/delete_expired_events.sql) |

## What the script deletes

An event is deleted when both are true:

- its `created` value is older than `retention_days`. The column holds .NET
  `DateTime` ticks (100-nanosecond intervals since `0001-01-01` UTC), written by
  the plugin at persist time, and is indexed by the default DDL;
- it is **not** the highest sequence number of its persistence ID. The plugin
  reads that row to decide where an entity resumes writing; when the plugin
  deletes events itself, it marks that row `deleted` instead of removing it.
  Removing it would let the entity reuse sequence numbers, so the script always
  keeps it.

Each run selects at most `batch_size` eligible events in `ordering` order, then
deletes their tag rows and the events in one transaction, tags first. The tag
delete works whether or not the
[optional tag-table FK](../TagTableForeignKey/README.md) is installed; with the
FK, the cascade has nothing left to remove.

## Before scheduling it

The script cannot see application state. Choose `retention_days` so that:

1. **Recovery no longer needs the deleted history.** An entity replays its
   events on recovery. Only delete events that a newer snapshot covers, or
   whose history the entity no longer needs.
2. **Every projection has already read the deleted events.** An `EventsByTag`
   consumer that has not reached an event will never see it once it is
   deleted. Keep the cutoff behind the slowest consumer's offset.

Never delete tag rows whose journal event still exists. The event silently
disappears from `EventsByTag`, consumer offsets move past it, and in `TagTable`
mode the journal keeps no copy of the tags, so the tag cannot be rebuilt.

## Configure and run

1. Copy the provider's script into the deployment's maintenance assets and edit
   its configuration header.
2. Run it unchanged first. The default is a read-only report of how many events
   are eligible; it may scan large tables, so check query plans first.
3. Set `execute_delete` to `true` (PostgreSQL) or `@execute_delete` to `1`
   (SQL Server). Each run deletes one bounded batch and commits it. Schedule the
   script during low-traffic periods, or re-run it until the remaining count
   is zero. A failed run can be retried; completed batches stay committed.

| Setting | Default PostgreSQL | Default SQL Server | Compatibility mapping |
| --- | --- | --- | --- |
| `schema_name` | `public` | `dbo` | Use the deployment's configured schema. |
| `journal_table` | `journal` | `journal` | PostgreSQL: `event_journal`; SQL Server: `EventJournal`. |
| `journal_ordering_column` | `ordering` | `ordering` | SQL Server: `Ordering`. |
| `journal_persistence_id_column` | `persistence_id` | `persistence_id` | SQL Server: `PersistenceId`. |
| `journal_sequence_number_column` | `sequence_number` | `sequence_number` | PostgreSQL: `sequence_nr`; SQL Server: `SequenceNr`. |
| `journal_created_column` | `created` | `created` | PostgreSQL: `created_at`; SQL Server: `Timestamp`. |
| `tag_table` | `tags` | `tags` | Use the configured tag-table name. |
| `tag_ordering_id_column` | `ordering_id` | `ordering_id` | Use the configured tag ordering column. |
| `retention_days` | `90` | `90` | |
| `execute_delete` | `false` | `0` | |
| `batch_size` | `1000` | `1000` | 1 through 100000. |

PostgreSQL headers use `psql` `\set` commands and must run through `psql` with
autocommit; SQL Server headers are T-SQL declarations and run through `sqlcmd`,
an Agent job step, or any client that executes the complete batch. Do not wrap
either script in an outer transaction. Identifiers are quoted by the scripts;
do not replace that with raw string concatenation. Supply credentials through
the client's normal mechanism, outside the scripts:

```bash
psql "service=maintenance" -X -v ON_ERROR_STOP=1 -f PostgreSql/delete_expired_events.sql
sqlcmd -S "$SQLSERVER_HOST" -d "$DATABASE" -U "$SQLSERVER_USER" -b -i SqlServer/delete_expired_events.sql
```

## Tags left behind by earlier purges

If a job has already deleted journal rows without their tags, the tags table
contains orphans: tag rows whose journal row no longer exists. Audit and remove
them with [`01_cleanup_orphan_tags.sql`](../TagTableForeignKey/README.md), which
can be used without installing the FK.

## Other providers

MySQL and SQLite are outside this script set. The same approach applies: select
a bounded batch of eligible `ordering` values with the rules above, then delete
the matching `tags.ordering_id` rows before the journal rows in one transaction.
