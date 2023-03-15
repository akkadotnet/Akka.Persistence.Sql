// -----------------------------------------------------------------------
//  <copyright file="JournalSequenceRetrievalConfig.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;

namespace Akka.Persistence.Sql.Config
{
    public class JournalSequenceRetrievalConfig
    {
        public JournalSequenceRetrievalConfig(Configuration.Config config)
        {
            BatchSize = config.GetInt("journal-sequence-retrieval.batch-size", 10000);
            MaxTries = config.GetInt("journal-sequence-retrieval.max-tries", 10);
            QueryDelay = config.GetTimeSpan("journal-sequence-retrieval.query-delay", TimeSpan.FromSeconds(1));
            MaxBackoffQueryDelay = config.GetTimeSpan("journal-sequence-retrieval.max-backoff-query-delay", TimeSpan.FromSeconds(60));
            AskTimeout = config.GetTimeSpan("journal-sequence-retrieval.ask-timeout", TimeSpan.FromSeconds(1));
        }

        public TimeSpan AskTimeout { get; }

        public TimeSpan MaxBackoffQueryDelay { get; }

        public TimeSpan QueryDelay { get; }

        public int MaxTries { get; }

        public int BatchSize { get; }

        public static JournalSequenceRetrievalConfig Apply(Configuration.Config config) => new(config);
    }
}
