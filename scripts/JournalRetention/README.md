# Database-side journal retention

If you purge old events with your own SQL instead of Akka.Persistence's
`DeleteMessagesTo`, you also have to delete their rows in the tags table. Tag
rows are an index over journal rows; leaving them behind makes the tags table
grow forever.

Example scripts, using the default table mapping:
[PostgreSQL](postgresql.sql) · [SQL Server](sqlserver.sql) ·
[MySQL](mysql.sql) · [SQLite](sqlite.sql)

Each one deletes up to 10,000 events older than 90 days, plus their tags. Change
the cutoff, the batch size, and the table names to fit your deployment, and
schedule it however you like.

## Rules

Whatever you delete, keep to these:

1. **Delete tags with their events.** Delete the tag rows first, then the
   journal rows, in one transaction. With the
   [optional FK](../TagTableForeignKey/README.md) installed, the database does
   this for you and the tags `DELETE` can be dropped.
2. **Keep each persistence ID's highest sequence number.** The plugin reads it
   to decide which sequence number an entity writes next.
3. **Don't delete events that are still needed.** That means events an entity
   replays on recovery (keep them unless a newer snapshot covers them), and
   events your `EventsByTag` consumers haven't read yet.
4. **Never delete tag rows whose event still exists.** The event silently drops
   out of `EventsByTag`.

`created` holds .NET `DateTime` ticks (100 ns since 0001-01-01 UTC), which is
why the examples convert the cutoff.

If earlier purges already left orphaned tag rows, clean them up with
[`01_cleanup_orphan_tags.sql`](../TagTableForeignKey/README.md).
