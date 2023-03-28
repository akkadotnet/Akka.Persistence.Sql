// -----------------------------------------------------------------------
//  <copyright file="SqlServerJournalSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Persistence.TCK.Journal;
using FluentAssertions.Extensions;
using Xunit;
using Xunit.Abstractions;
#if !DEBUG
using Akka.Persistence.Sql.Tests.Common.Internal.Xunit;
#endif

namespace Akka.Persistence.Sql.Tests.SqlServer
{
#if !DEBUG
    [SkipWindows]
#endif
    [Collection(nameof(SqlServerPersistenceSpec))]
    public class SqlServerJournalSpec : JournalSpec
    {
        private readonly SqlServerContainer _fixture;

        public SqlServerJournalSpec(
            ITestOutputHelper output,
            SqlServerContainer fixture)
            : base(
                Configuration(fixture),
                nameof(SqlServerJournalSpec),
                output)
        {
            _fixture = fixture;
            Initialize();
        }

        // TODO: hack. Replace when https://github.com/akkadotnet/akka.net/issues/3811
        protected override bool SupportsSerialization => false;

        protected override void AfterAll()
        {
            base.AfterAll();
            Shutdown();
            if (!_fixture.InitializeDbAsync().Wait(10.Seconds()))
                throw new Exception("Failed to clean up database in 10 seconds");
        }

        private static Configuration.Config Configuration(SqlServerContainer fixture)
            => SqlServerJournalSpecConfig.Create(
                fixture.ConnectionString,
                "journalSpec");
    }
}
