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
    [Collection("SqlCompatibilitySpec")]
    public class PostgreSqlScriptCompatibilitySpec : SqlScriptCompatibilitySpec<PostgreSqlFixture>
    {
        public PostgreSqlScriptCompatibilitySpec(ITestOutputHelper output) : base(output) { }

        protected override TestSettings Settings => PostgreSqlSpecSettings.Instance;
        protected override string ScriptFolder => "PostgreSql";

        protected override void ExecuteSqlScripts(string setup, string migration, string cleanup)
        {
            using var connection = new NpgsqlConnection(Fixture.ConnectionString);
            connection.Open();

            var command = new NpgsqlCommand
            {
                Connection = connection,
            };

            command.CommandText = setup;
            command.ExecuteNonQuery();

            command.CommandText = migration;
            command.ExecuteNonQuery();

            command.CommandText = cleanup;
            command.ExecuteNonQuery();
        }
    }
}
