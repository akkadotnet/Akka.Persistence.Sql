// -----------------------------------------------------------------------
//  <copyright file="SqlServerTagTableQueryCancellationSpecs.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Data.Common;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Persistence.Sql.Tests.SqlServer;
using Microsoft.Data.SqlClient;
using Xunit;
#if !DEBUG
using Akka.Persistence.Sql.Tests.Common.Internal.Xunit;
#endif

namespace Akka.Persistence.Sql.Tests.Query.SqlServer.TagTable;

#if !DEBUG
[SkipWindows]
#endif
[Collection(nameof(SqlServerPersistenceSpec))]
public class SqlServerTagTableQueryCancellationSpecs : QueryCancellationSpecsBase<SqlServerContainer>
{
    public SqlServerTagTableQueryCancellationSpecs(ITestOutputHelper output, SqlServerContainer fixture)
        : base(TagMode.TagTable, output, nameof(SqlServerTagTableQueryCancellationSpecs), fixture)
    {
    }

    protected override DbConnection CreateConnection(string connectionString)
        => new SqlConnection(connectionString);

    protected override string LockJournalTableSql => "SELECT COUNT(*) FROM dbo.journal WITH (TABLOCKX, HOLDLOCK)";

    protected override string SessionIdSql => "SELECT @@SPID";

    protected override string BlockedSessionsSql
        => "SELECT session_id FROM sys.dm_exec_requests WHERE blocking_session_id = @holder";
}
