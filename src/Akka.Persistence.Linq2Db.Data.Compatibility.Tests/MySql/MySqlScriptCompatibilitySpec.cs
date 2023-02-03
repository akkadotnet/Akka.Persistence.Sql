// -----------------------------------------------------------------------
//  <copyright file="MySqlScriptCompatibilitySpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Text;
using MySql.Data.MySqlClient;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Linq2Db.Data.Compatibility.Tests.MySql
{
    [Collection("SqlCompatSpec")]
    public class MySqlScriptCompatibilitySpec : SqlScriptCompatibilitySpec<MySqlFixture>
    {
        public MySqlScriptCompatibilitySpec(ITestOutputHelper output) : base(output)
        {
        }

        protected override TestSettings Settings => MySqlSpecSettings.Instance;
        protected override string ScriptFolder => "MySql";
        protected override void ExecuteSqlScripts(string setup, string migration, string cleanup)
        {
            using var conn = new MySqlConnection(Fixture.ConnectionString);
            var script = new MySqlScript(conn);
            
            script.Query = setup;
            script.Execute();
            
            script.Query = migration;
            script.Execute();
            
            script.Query = cleanup;
            script.Execute();
        }
    }
}