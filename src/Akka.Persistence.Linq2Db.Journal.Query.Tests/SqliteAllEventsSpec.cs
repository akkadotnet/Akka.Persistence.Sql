using Akka.Configuration;
using Akka.Persistence.Linq2Db.Journal.Query.Tests;
using Akka.Persistence.Query;
using Akka.Persistence.Sql.Linq2Db;
using Akka.Persistence.Sql.Linq2Db.Query;
using Akka.Persistence.TCK.Query;
using Akka.Util.Internal;
using LinqToDB;
using Xunit.Abstractions;

namespace Akka.Persistence.Sqlite.Tests.Query
{
    public class SqliteAllEventsSpec : AllEventsSpec
    {
        public static readonly AtomicCounter Counter = new AtomicCounter(0);
        
        public static Config Config(int id)
        {
            var connString = $"Filename=file:memdb-l2db-journal-allevents-{id}.db;Mode=Memory;Cache=Shared";
            ConnectionContext.Remember(connString);
            return ConfigurationFactory.ParseString($@"
akka.loglevel = INFO
akka.persistence.journal.plugin = ""akka.persistence.journal.linq2db""
akka.persistence.journal.linq2db {{
    event-adapters {{
        color-tagger  = ""Akka.Persistence.TCK.Query.ColorFruitTagger, Akka.Persistence.TCK""
    }}
    event-adapter-bindings = {{
        ""System.String"" = color-tagger
    }}
    plugin-dispatcher = ""akka.actor.default-dispatcher""
    auto-initialize = on
    provider-name = ""{ProviderName.SQLiteMS}""
    table-compatibility-mode = sqlite
    tables {{
        journal {{
            table-name = event_journal
            metadata-table-name = journal_metadata
            auto-init = true 
            warn-on-auto-init-fail = false
        }} 
    }}
    connection-string = ""{connString}""
    refresh-interval = 1s
}}
akka.persistence.query.journal.linq2db {{
    provider-name = ""{ProviderName.SQLiteMS}""
    connection-string = ""{connString}""
    table-compatibility-mode = sqlite
    tables {{
        journal {{
            table-name = event_journal
            metadata-table-name = journal_metadata
            warn-on-auto-init-fail = false
        }}
    }}
}}
akka.test.single-expect-default = 10s")
                .WithFallback(Linq2DbPersistence.DefaultConfiguration());
        }
        
        public SqliteAllEventsSpec(ITestOutputHelper output) : base(Config(Counter.GetAndIncrement()), nameof(SqliteAllEventsSpec), output)
        {
            ReadJournal = Sys.ReadJournalFor<Linq2DbReadJournal>(Linq2DbReadJournal.Identifier);
        }
    }
}