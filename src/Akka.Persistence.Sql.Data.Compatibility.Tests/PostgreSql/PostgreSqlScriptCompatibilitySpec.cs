// -----------------------------------------------------------------------
//  <copyright file="PostgreSqlScriptCompatibilitySpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Npgsql;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Data.Compatibility.Tests.PostgreSql
{
    [Collection("SqlCompatSpec")]
    public class PostgreSqlScriptCompatibilitySpec : SqlScriptCompatibilitySpec<PostgreSqlFixture>
    {
        public PostgreSqlScriptCompatibilitySpec(ITestOutputHelper output) : base(output)
        {
        }

        protected override TestSettings Settings => PostgreSqlSpecSettings.Instance;
        protected override string ScriptFolder => "PostgreSql";
        protected override void ExecuteSqlScripts(string setup, string migration, string cleanup)
        {
            using var conn = new NpgsqlConnection(Fixture.ConnectionString);
            conn.Open();

            var cmd = new NpgsqlCommand {
                Connection = conn
            };

            cmd.CommandText = setup;
            cmd.ExecuteNonQuery();
            cmd.CommandText = migration;
            cmd.ExecuteNonQuery();
            cmd.CommandText = cleanup;
            cmd.ExecuteNonQuery();
        }
    }
}
