// -----------------------------------------------------------------------
//  <copyright file="MigratorCompatibilitySpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------
/*
using System;
using System.Threading.Tasks;
using Akka.Configuration;
using Akka.Hosting;
using Akka.Persistence.Linq2Db.Data.Compatibility.Tests.Internal;
using Akka.Persistence.Linq2Db.HelperLib;
using Xunit.Abstractions;

namespace Akka.Persistence.Linq2Db.Data.Compatibility.Tests
{
    public abstract class MigratorCompatibilitySpec<T>: DataCompatibilitySpec<T> where T: ITestContainer, new()
    {
        protected MigratorCompatibilitySpec(ITestOutputHelper output) : base(output)
        {
        }

        private readonly Config _config = @"
akka.persistence.journal.linq2db.tag-write-mode = Both
akka.persistence.query.journal.linq2db.tag-read-mode = TagTable";
        
        protected override async Task InitializeTestAsync()
        {
            await base.InitializeTestAsync();

            try
            {
                var config = _config.WithFallback(Config());
                var migrator = new TagTableMigrator(config);
                migrator.Migrate(0, 1000).Wait(TimeSpan.FromMinutes(1));
            }
            catch
            {
                if(TestCluster is { })
                    await TestCluster.DisposeAsync();
                await Fixture.DisposeAsync();
                throw;
            }
        }

        protected override void Setup(AkkaConfigurationBuilder builder, IServiceProvider provider)
        {
            base.Setup(builder, provider);
            builder.AddHocon(_config, HoconAddMode.Prepend);
        }

    }
}
*/