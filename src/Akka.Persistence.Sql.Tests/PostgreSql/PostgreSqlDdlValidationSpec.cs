// -----------------------------------------------------------------------
//  <copyright file="PostgreSqlDdlValidationSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Persistence.Sql.Tests.Common.Internal.Xunit;
using Xunit;

namespace Akka.Persistence.Sql.Tests.PostgreSql
{
    [SkipWindows]
    [Collection(nameof(PostgreSqlPersistenceSpec))]
    public class PostgreSqlDdlValidationSpec : DdlValidationSpecBase
    {
        protected override string ProviderName => "postgresql";

        public PostgreSqlDdlValidationSpec(ITestOutputHelper output, PostgreSqlContainer fixture)
            : base(output, fixture)
        {
        }

        [Fact]
        public Task Default_DDL_Should_Execute_Successfully()
            => ValidateDdlFiles("default");

        [Fact]
        public Task Compat_DDL_Should_Execute_Successfully()
            => ValidateDdlFiles("compat");

        [Fact]
        public Task Default_DDL_Should_Be_Idempotent()
            => ValidateDdlIdempotency("default");

        [Fact]
        public Task Compat_DDL_Should_Be_Idempotent()
            => ValidateDdlIdempotency("compat");

        [Fact]
        public Task Default_DDL_Should_Work_With_AkkaPersistence()
            => ValidatePersistenceIntegration("default");

        [Fact]
        public Task Compat_DDL_Should_Work_With_AkkaPersistence()
            => ValidatePersistenceIntegration("compat");
    }
}
