using Akka.Persistence.Sql.Tests.Common.Containers;
using Xunit;
#if !DEBUG
using Akka.Persistence.Sql.Tests.Common.Internal.Xunit;
#endif

namespace Akka.Persistence.Sql.Tests.SqlServer;

#if !DEBUG
[SkipWindows]
#endif
[Collection(nameof(SqlServerPersistenceSpec))]
public class SqlServerTagTableFastInsertSpec : TagTableFastInsertSpecBase
{
    private readonly SqlServerContainer _fixture;

    public SqlServerTagTableFastInsertSpec(ITestOutputHelper output, SqlServerContainer fixture) : base(output)
    {
        _fixture = fixture;
    }

    // LinqToDB SqlServerBulkCopy.MaxParameters (SQL Server allows 2100; the ADO provider uses one)
    protected override int ProviderMaxParameters => 2099;

    protected override string ProviderName => _fixture.ProviderName;

    // Each fixture uses its own table names, so the shared container database is fine.
    protected override string ConnectionString(string databaseId) => _fixture.ConnectionString;
}
