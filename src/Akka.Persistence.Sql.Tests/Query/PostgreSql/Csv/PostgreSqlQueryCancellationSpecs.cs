// -----------------------------------------------------------------------
//  <copyright file="PostgreSqlQueryCancellationSpecs.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Data.Common;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Persistence.Sql.Tests.PostgreSql;
using Npgsql;
using Xunit;
#if !DEBUG
using Akka.Persistence.Sql.Tests.Common.Internal.Xunit;
#endif

namespace Akka.Persistence.Sql.Tests.Query.PostgreSql.Csv;

#if !DEBUG
[SkipWindows]
#endif
[Collection(nameof(PostgreSqlPersistenceSpec))]
public class PostgreSqlQueryCancellationSpecs : QueryCancellationSpecsBase<PostgreSqlContainer>
{
    public PostgreSqlQueryCancellationSpecs(ITestOutputHelper output, PostgreSqlContainer fixture)
        : base(TagMode.Csv, output, nameof(PostgreSqlQueryCancellationSpecs), fixture)
    {
    }

    protected override DbConnection CreateConnection(string connectionString)
        => new NpgsqlConnection(connectionString);

    protected override string LockJournalTableSql => "LOCK TABLE public.journal IN ACCESS EXCLUSIVE MODE";

    protected override string SessionIdSql => "SELECT pg_backend_pid()";

    protected override string BlockedSessionsSql
        => "SELECT pid FROM pg_stat_activity WHERE @holder::int = ANY(pg_blocking_pids(pid))";
}
