---
uid: sql-queries
title: Persistence Query Support
---

# Persistence Query Support

The [Akka.Persistence.Query documentation](https://getakka.net/articles/persistence/persistence-query.html) asks every read journal to document the queries it supports and their semantics. This page does that for `SqlReadJournal`.

```csharp
var readJournal = PersistenceQuery.Get(system)
    .ReadJournalFor<SqlReadJournal>(SqlReadJournal.Identifier); // "akka.persistence.query.journal.sql"
```

## Supported queries

`SqlReadJournal` implements all of the predefined queries:

| Query | Interface | Completes |
|---|---|---|
| `PersistenceIds()` | `IPersistenceIdsQuery` | never (live) |
| `CurrentPersistenceIds()` | `ICurrentPersistenceIdsQuery` | after the current ids |
| `EventsByPersistenceId(id, from, to)` | `IEventsByPersistenceIdQuery` | when `to` is reached, otherwise never |
| `CurrentEventsByPersistenceId(id, from, to)` | `ICurrentEventsByPersistenceIdQuery` | after the stored events |
| `EventsByTag(tag, offset)` | `IEventsByTagQuery` | never (live) |
| `CurrentEventsByTag(tag, offset)` | `ICurrentEventsByTagQuery` | after the stored events |
| `AllEvents(offset)` | `IAllEventsQuery` | never (live) |
| `CurrentAllEvents(offset)` | `ICurrentAllEventsQuery` | after the stored events |

The live queries poll the database, they are not pushed. The `Current*` queries read up to the highest `ordering` in the journal at the time the query starts and then complete. Events written after that are not included.

## Offsets

Every event in the journal table has an `ordering` column: a database-generated, increasing number shared by all persistence ids. `EventEnvelope.Offset` is always a `Sequence` holding that value, for all event queries, including `EventsByPersistenceId`.

`EventsByTag`, `CurrentEventsByTag`, `AllEvents` and `CurrentAllEvents` accept:

- `Offset.NoOffset()` or `Offset.Sequence(0)`: start from the beginning.
- `Offset.Sequence(n)`: return events with `ordering > n`. The offset is **exclusive**, so to resume a projection store the last processed `envelope.Offset` and pass it back unchanged.
- `Offset.FromEnd(count)`: start with the last `count` matching events, then continue like a normal query.

Any other offset type is treated as `NoOffset`.

`EventsByPersistenceId` and `CurrentEventsByPersistenceId` take sequence numbers instead of offsets. `fromSequenceNr` and `toSequenceNr` are both inclusive.

## Ordering and gaps

Within a query, events are emitted in `ordering` order.

`ordering` values can be committed out of order: a transaction that got ordering 11 may commit before the one that got 10. A query that has already emitted 11 would then never see 10. To avoid that, the journal sequence actor only lets queries read up to the highest `ordering` that has no gaps below it. When it finds a gap, it waits for it to be filled. If the gap is still there after `journal-sequence-retrieval.max-tries` checks, spaced `journal-sequence-retrieval.query-delay` apart (10 × 1s by default), the gap is treated as permanent and the query moves past it.

This means:

- An event can be emitted later than it was written, by up to about `query-delay × max-tries` when there is a gap. Gaps also appear when a transaction rolls back, because the reserved `ordering` value is never used.
- An event whose transaction commits after its gap was given up on is skipped by queries that have already moved past it. It is still in the journal, and it is returned by a new query that starts from an earlier offset.

Deleted events (`DeleteMessages`) are not returned by any query.

## Event adapters and tags

Events go through the read-side event adapters of the write journal named in `write-plugin`. An adapter that turns one stored event into several produces several envelopes with the same offset.

`EventsByTag` reads tags from the tag table or from the legacy CSV `tags` column, depending on `tag-read-mode`. See [Configuration](configuration.md) and the tag table migration in the [Features/Architecture](features.md) page.

## Polling and latency

Live queries fetch up to `max-buffer-size` events per round trip. After a batch smaller than that, they wait `refresh-interval` before polling again. The highest readable `ordering` is refreshed separately every `journal-sequence-retrieval.query-delay`, so the delay before a new event is emitted depends on both settings. Lowering only `refresh-interval` doesn't reduce it.

```hocon
akka.persistence.query.journal.sql {
  max-buffer-size = 500
  refresh-interval = 1s
  journal-sequence-retrieval {
    query-delay = 1s
    max-tries = 10
  }
}
```

`PersistenceIds()` reads all distinct persistence ids every `refresh-interval` and emits the ones it hasn't seen yet. Each id is emitted once.
