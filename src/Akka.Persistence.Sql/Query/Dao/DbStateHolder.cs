// -----------------------------------------------------------------------
//  <copyright file="DbStateHolder.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Data;
using System.Threading;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Db;

namespace Akka.Persistence.Sql.Query.Dao
{
    /// <summary>
    /// Used to help improve capture usage and ease composition via extension methods.
    /// </summary>
    internal class DbStateHolder
    {
        public readonly AkkaPersistenceDataConnectionFactory ConnectionFactory;
        public readonly IsolationLevel IsolationLevel;
        public readonly CancellationToken ShutdownToken;
        public readonly TagMode Mode;
        public DbStateHolder(
            AkkaPersistenceDataConnectionFactory connectionFactory,
            IsolationLevel isolationLevel, 
            CancellationToken shutdownToken, 
            TagMode mode
        )
        {
            ConnectionFactory = connectionFactory;
            IsolationLevel = isolationLevel;
            ShutdownToken = shutdownToken;
            Mode = mode;
        }
    }
}
