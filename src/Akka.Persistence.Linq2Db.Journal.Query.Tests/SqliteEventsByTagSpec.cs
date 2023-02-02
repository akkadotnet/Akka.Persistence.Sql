﻿//-----------------------------------------------------------------------
// <copyright file="SqliteEventsByTagSpec.cs" company="Akka.NET Project">
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
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sqlite.Tests.Query
{
    public class SqliteEventsByTagSpec : EventsByTagSpec
    {
        public static readonly AtomicCounter Counter = new AtomicCounter(0);

        public static Config Config(int id)
        {
            var connString = $"Filename=file:memdb-l2db-journal-eventsbytag-{id}.db;Mode=Memory;Cache=Shared";
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
    provider-name = ""{ProviderName.SQLiteMS}""
    table-mapping = sqlite
    auto-initialize = on
    connection-string = ""{connString}""
    refresh-interval = 1s
}}
akka.persistence.query.journal.linq2db
{{
    provider-name = ""{ProviderName.SQLiteMS}""
    table-mapping = sqlite
    connection-string = ""{connString}""
    auto-initialize = on
}}
akka.test.single-expect-default = 10s")
                .WithFallback(Linq2DbPersistence.DefaultConfiguration);
        }

        public SqliteEventsByTagSpec(ITestOutputHelper output) : base(Config(Counter.GetAndIncrement()), nameof(SqliteEventsByTagSpec), output)
        {
            ReadJournal = Sys.ReadJournalFor<Linq2DbReadJournal>(Linq2DbReadJournal.Identifier);
        }
    }
}
