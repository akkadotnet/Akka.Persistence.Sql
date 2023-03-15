// -----------------------------------------------------------------------
//  <copyright file="Linq2DbReadJournalProvider.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Actor;
using Akka.Persistence.Query;

namespace Akka.Persistence.Sql.Query
{
    public class Linq2DbReadJournalProvider : IReadJournalProvider
    {
        private readonly Configuration.Config _config;
        private readonly string _configPath;
        private readonly ExtendedActorSystem _system;

        public Linq2DbReadJournalProvider(
            ExtendedActorSystem system,
            Configuration.Config config)
        {
            _system = system;
            _config = config;
            _configPath = "linq2db";
        }

        public Linq2DbReadJournalProvider(
            ExtendedActorSystem system,
            Configuration.Config config,
            string configPath)
        {
            _system = system;
            _config = config;
            _configPath = configPath;
        }

        public IReadJournal GetReadJournal()
            => new Linq2DbReadJournal(_system, _config, _configPath);
    }
}
