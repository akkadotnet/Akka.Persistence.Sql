// -----------------------------------------------------------------------
//  <copyright file="ProviderParameterLimits.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using LinqToDB.DataProvider;
using LinqToDB.Internal.DataProvider;
using LinqToDB.Internal.DataProvider.PostgreSQL;
using LinqToDB.Internal.DataProvider.SQLite;
using LinqToDB.Internal.DataProvider.SqlServer;

namespace Akka.Persistence.Sql.Db
{
    /// <summary>
    ///     Exposes the per-provider maximum parameter count LinqToDB uses to split
    ///     <c>BulkCopyType.MultipleRows</c> commands, so our own multi-row commands
    ///     follow the same rules (and track LinqToDB if those values change).
    /// </summary>
    /// <remarks>
    ///     LinqToDB keeps these values on protected members of its provider BulkCopy classes;
    ///     the nested subclasses below only surface them and are never used to copy data.
    /// </remarks>
    internal static class ProviderParameterLimits
    {
        public static int MaxParameters(IDataProvider provider)
            => provider switch
            {
                SqlServerDataProvider sqlServer => new SqlServerLimits(sqlServer).Parameters,
                PostgreSQLDataProvider postgres => new PostgreSqlLimits(postgres).Parameters,
                SQLiteDataProvider => new SqliteLimits().Parameters,
                _ => new DefaultLimits().Parameters,
            };

        private sealed class SqlServerLimits : SqlServerBulkCopy
        {
            public SqlServerLimits(SqlServerDataProvider provider) : base(provider) { }
            public int Parameters => MaxParameters;
        }

        private sealed class PostgreSqlLimits : PostgreSQLBulkCopy
        {
            public PostgreSqlLimits(PostgreSQLDataProvider provider) : base(provider) { }
            public int Parameters => MaxParameters;
        }

        private sealed class SqliteLimits : SQLiteBulkCopy
        {
            public int Parameters => MaxParameters;
        }

        private sealed class DefaultLimits : BasicBulkCopy
        {
            public int Parameters => MaxParameters;
        }
    }
}
