using Akka.Persistence.Sql.Linq2Db.Tests.Docker;
using Akka.Persistence.Sql.Linq2Db.Tests.Docker.Docker;
using Xunit;

namespace Akka.Persistence.Linq2Db.CompatibilityTests.Docker.Postgres
{
    [CollectionDefinition("PostgreSQLSpec")]
    public sealed class PostgreSQLSpecsFixture : ICollectionFixture<PostgreSQLFixture>
    {
    }
}