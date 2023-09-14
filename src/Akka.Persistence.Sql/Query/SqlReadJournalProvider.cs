// -----------------------------------------------------------------------
//  <copyright file="SqlReadJournalProvider.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Actor;
using Akka.Persistence.Query;

namespace Akka.Persistence.Sql.Query
{
    public class SqlReadJournalProvider : IReadJournalProvider
    {
        private readonly Configuration.Config _config;
        private readonly ExtendedActorSystem _system;

        public SqlReadJournalProvider(
            ExtendedActorSystem system,
            Configuration.Config config)
        {
            _system = system;
            _config = config.WithFallback(SqlPersistence.DefaultQueryConfiguration);
        }

        public IReadJournal GetReadJournal()
            => new SqlReadJournal(_system, _config);
    }
}
