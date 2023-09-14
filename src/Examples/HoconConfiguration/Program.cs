// See https://aka.ms/new-console-template for more information

using Akka.Actor;
using Akka.Persistence;
using Akka.Persistence.Query;
using Akka.Persistence.Sql.Query;
using Akka.Streams;
using Akka.Streams.Dsl;
using HoconConfiguration;

const string config =
    """
    akka.persistence {
        journal {
            plugin = "akka.persistence.journal.sql"
            sql {
                class = "Akka.Persistence.Sql.Journal.SqlWriteJournal, Akka.Persistence.Sql"
                connection-string = "DataSource=db/test.db;"
                provider-name = "SQLite.MS"
            }
        }
        query.journal.sql {
            class = "Akka.Persistence.Sql.Query.SqlReadJournalProvider, Akka.Persistence.Sql"
            connection-string = "DataSource=db/test.db;"
            provider-name = "SQLite.MS"
        }
        snapshot-store {
            plugin = "akka.persistence.snapshot-store.sql"
            sql {
                class = "Akka.Persistence.Sql.Snapshot.SqlSnapshotStore, Akka.Persistence.Sql"
                connection-string = "DataSource=db/test.db;"
                provider-name = "SQLite.MS"
            }
        }
    }
    """;

var sys = ActorSystem.Create("my-system", config);
var actor = sys.ActorOf(TestPersistenceActor.Props("test"));
var reader = sys.ReadJournalFor<SqlReadJournal>("akka.persistence.query.journal.sql");

foreach (var i in Enumerable.Range(0, 200))
{
    var chr = (char)(65 + i % 26);
    actor.Tell(new string(new []{chr}));
    await Task.Delay(TimeSpan.FromSeconds(0.05));
}

_ = reader.CurrentAllEvents(Offset.NoOffset())
    .Select(e =>
    {
        Console.WriteLine($"New event: {e.Event}");
        return e;
    }).RunWith(Sink.Ignore<EventEnvelope>(), sys.Materializer());

Console.ReadKey();

await sys.Terminate();