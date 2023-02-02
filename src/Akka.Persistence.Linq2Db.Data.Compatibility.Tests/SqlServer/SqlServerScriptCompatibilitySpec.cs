// -----------------------------------------------------------------------
//  <copyright file="SqlServerScriptCompatibilitySpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using Xunit.Abstractions;

namespace Akka.Persistence.Linq2Db.Data.Compatibility.Tests.SqlServer
{
    public class SqlServerScriptCompatibilitySpec: SqlScriptCompatibilitySpec<SqlServerFixture>
    {
        public SqlServerScriptCompatibilitySpec(ITestOutputHelper output) : base(output)
        {
        }

        protected override TestSettings Settings => SqlServerSpecSettings.Instance;
        protected override string ScriptFolder => "SqlServer";
        
        protected override void ExecuteSqlScripts(string setup, string migration, string cleanup)
        {
            using var conn = new SqlConnection(Fixture.ConnectionString);
            var server = new Server(new ServerConnection(conn));

            server.ConnectionContext.ExecuteNonQuery(setup);
            server.ConnectionContext.ExecuteNonQuery(migration);
            server.ConnectionContext.ExecuteNonQuery(cleanup);
        }
    }
}