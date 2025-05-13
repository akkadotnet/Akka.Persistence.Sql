// -----------------------------------------------------------------------
//  <copyright file="SqlReadJournalProvider.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Actor;
using Akka.Annotations;
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

        /// <summary>
        ///     Note that this is safe to do because the only place this is being called is
        ///     inside the `PersistenceQuery.ReadJournalFor{T}()` public method inside
        ///     Akka.Persistence.Query and the result of that method call is then cached
        ///     and reused for the duration of the ActorSystem lifetime.
        /// </summary>
        /// <returns>
        ///     A new instance of IReadJournal specific to this persistence plugin.
        /// </returns>
        [InternalApi]
        public IReadJournal GetReadJournal()
            => new SqlReadJournal(_system, _config);
    }
}
