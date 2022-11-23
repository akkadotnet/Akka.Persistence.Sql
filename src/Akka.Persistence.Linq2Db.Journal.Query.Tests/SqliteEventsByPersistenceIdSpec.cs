//-----------------------------------------------------------------------
// <copyright file="SqliteEventsByPersistenceIdSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2020 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2020 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using Akka.Configuration;
using Akka.Persistence.Linq2Db.Journal.Query.Tests;
using Akka.Persistence.Query;
using Akka.Persistence.Sql.Linq2Db;
using Akka.Persistence.Sql.Linq2Db.Journal;
using Akka.Persistence.Sql.Linq2Db.Query;
using Akka.Persistence.TCK.Query;
using Akka.Util.Internal;
using LinqToDB;
using Xunit.Abstractions;

namespace Akka.Persistence.Sqlite.Tests.Query
{
    public class SqliteEventsByPersistenceIdSpec : EventsByPersistenceIdSpec
    {
        public static readonly AtomicCounter Counter = new AtomicCounter(0);
        public static Config Config(int id)
        {
            var connectionString = $"Filename=file:memdb-l2db-journal-eventsbypersistenceid-{id}.db;Mode=Memory;Cache=Shared";
            ConnectionContext.Remember(connectionString);
            return ConfigurationFactory.ParseString($@"
akka.loglevel = INFO
akka.persistence.journal.plugin = ""akka.persistence.journal.linq2db""
akka.persistence.journal.linq2db {{
    plugin-dispatcher = ""akka.actor.default-dispatcher""
    table-name = event_journal
    metadata-table-name = journal_metadata
    auto-initialize = on
    provider-name = ""{ProviderName.SQLiteMS}""
    table-compatibility-mode = sqlite
    connection-string = ""{connectionString}""
    refresh-interval = 1s
    tables {{
       journal {{
         table-name = event_journal
         metadata-table-name = journal_metadata
         auto-init = true 
         warn-on-auto-init-fail = false
       }} 
    }}
}}
akka.persistence.query.journal.linq2db
{{
    provider-name = ""{ProviderName.SQLiteMS}""
    table-compatibility-mode = sqlite
    connection-string = ""{connectionString}""
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

        public SqliteEventsByPersistenceIdSpec(ITestOutputHelper output) : base(Config(Counter.GetAndIncrement()), nameof(SqliteEventsByPersistenceIdSpec), output)
        {
            ReadJournal = Sys.ReadJournalFor<Linq2DbReadJournal>(Linq2DbReadJournal.Identifier);
        }
    }
}
