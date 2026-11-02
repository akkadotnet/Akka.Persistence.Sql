using Akka.Persistence.Sql.Tests.Common.Containers;
using Xunit;
#if !DEBUG
using Akka.Persistence.Sql.Tests.Common.Internal.Xunit;
#endif

namespace Akka.Persistence.Sql.Tests.PostgreSql;

#if !DEBUG
[SkipWindows]
#endif
[Collection(nameof(PostgreSqlPersistenceSpec))]
public class PostgreSqlTagTableFastInsertSpec : TagTableFastInsertSpecBase
{
    private readonly PostgreSqlContainer _fixture;

    public PostgreSqlTagTableFastInsertSpec(ITestOutputHelper output, PostgreSqlContainer fixture) : base(output)
    {
        _fixture = fixture;
    }

    protected override int ProviderMaxParameters => 32766;

    protected override int? RaisedMaxParameters => 65535;

    protected override string ProviderName => _fixture.ProviderName;

    protected override string ConnectionString(string databaseId) => _fixture.ConnectionString;
}
