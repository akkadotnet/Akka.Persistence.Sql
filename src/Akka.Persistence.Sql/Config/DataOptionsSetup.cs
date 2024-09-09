// -----------------------------------------------------------------------
//  <copyright file="DataOptionsSetup.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Actor.Setup;
using LinqToDB;

namespace Akka.Persistence.Sql.Config
{
    public sealed class DataOptionsSetup: Setup
    {
        private DataOptions? DataOptions { get; }

        public DataOptionsSetup(DataOptions dataOptions)
        {
            DataOptions = dataOptions;
        }

        internal ReadJournalConfig Apply(ReadJournalConfig config)
        {
            if (DataOptions != null)
                config = config.WithDataOptions(DataOptions);

            return config;
        }

        internal JournalConfig Apply(JournalConfig config)
        {
            if (DataOptions != null)
                config = config.WithDataOptions(DataOptions);

            return config;
        }

        internal SnapshotConfig Apply(SnapshotConfig config)
        {
            if (DataOptions != null)
                config = config.WithDataOptions(DataOptions);

            return config;
        }
    }
}
