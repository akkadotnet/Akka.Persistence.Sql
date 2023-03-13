// -----------------------------------------------------------------------
//  <copyright file="SqlScriptCompatibilitySpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.IO;
using System.Threading.Tasks;
using Akka.Hosting;
using Akka.Persistence.Sql.Data.Compatibility.Tests.Internal;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Data.Compatibility.Tests
{
    public abstract class SqlScriptCompatibilitySpec<T>: DataCompatibilitySpec<T> where T: ITestContainer, new()
    {
        protected abstract string ScriptFolder { get; }

        public SqlScriptCompatibilitySpec(ITestOutputHelper output) : base(output)
        {
        }

        protected override async Task InitializeTestAsync()
        {
            await base.InitializeTestAsync();

            var workingDir = Path.GetDirectoryName(GetType().Assembly.Location);
            var migrationSetup = await File.ReadAllTextAsync(Path.Combine(workingDir!, ScriptFolder, "1_Migration_Setup.sql"));
            var migration = await File.ReadAllTextAsync(Path.Combine(workingDir!, ScriptFolder, "2_Migration.sql"));
            var migrationCleanup = await File.ReadAllTextAsync(Path.Combine(workingDir!, ScriptFolder, "3_Post_Migration_Cleanup.sql"));

            ExecuteSqlScripts(migrationSetup, migration, migrationCleanup);
        }

        protected override void Setup(AkkaConfigurationBuilder builder, IServiceProvider provider)
        {

            base.Setup(builder, provider);
            builder.AddHocon(@"
akka.persistence.journal.linq2db.tag-write-mode = TagTable
akka.persistence.query.journal.linq2db.tag-read-mode = TagTable", HoconAddMode.Prepend);
        }

        protected abstract void ExecuteSqlScripts(string setup, string migration, string cleanup);
    }
}
