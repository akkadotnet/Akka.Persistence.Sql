﻿// -----------------------------------------------------------------------
//  <copyright file="SqlEndToEndSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Actor.Setup;
using Akka.Event;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.TestKit;
using Akka.TestKit.Xunit2;
using Akka.TestKit.Xunit2.Internals;
using FluentAssertions;
using LanguageExt.UnitsOfMeasure;
using Xunit;
using Xunit.Abstractions;

#nullable enable
namespace Akka.Persistence.Sql.Tests
{
    public class SqlEndToEndSpecBase<TContainer> : 
        TestKitBase,
        IClassFixture<TContainer>,
        IAsyncLifetime 
        where TContainer: class, ITestContainer
    {
        private static string Config(ITestContainer fixture) => 
$$"""
akka.persistence {
    journal {
        plugin = "akka.persistence.journal.sql"
        sql {
            class = "Akka.Persistence.Sql.Journal.SqlWriteJournal, Akka.Persistence.Sql"
            connection-string = "{{fixture.ConnectionString}}"
            provider-name = "{{fixture.ProviderName}}"
        }
    }
    query.journal.sql {
        class = "Akka.Persistence.Sql.Query.SqlReadJournalProvider, Akka.Persistence.Sql"
        connection-string = "{{fixture.ConnectionString}}"
        provider-name = "{{fixture.ProviderName}}"
    }
    snapshot-store {
        plugin = "akka.persistence.snapshot-store.sql"
        sql {
            class = "Akka.Persistence.Sql.Snapshot.SqlSnapshotStore, Akka.Persistence.Sql"
            connection-string = "{{fixture.ConnectionString}}"
            provider-name = "{{fixture.ProviderName}}"
        }
    }
}
""";

        private const string GetAll = "getAll";
        private const string Ack = "ACK";
        private const string PId = "ac1";

        private readonly ITestOutputHelper? _output;
        private readonly TContainer _fixture;
        private IActorRef? _persistenceActor;

        public SqlEndToEndSpecBase(ITestOutputHelper? output, TContainer fixture) : base(new XunitAssertions(), null, "SqlEndToEndSpec")
        {
            _fixture = fixture;
            _output = output;
        }

        public async Task InitializeAsync()
        {
            await _fixture.InitializeAsync();
            
            var setup = ActorSystemSetup.Create(BootstrapSetup.Create().WithConfig(Config(_fixture)));
            base.InitializeTest(null, setup, "SqlEndToEndSpec", null);
            InitializeLogger(Sys);
            
            _persistenceActor = Sys.ActorOf(Props.Create(() => new MyPersistenceActor(PId)));
        }

        public Task DisposeAsync()
            => Task.CompletedTask;

        /// <summary>
        /// Initializes a new <see cref="TestOutputLogger"/> used to log messages.
        /// </summary>
        /// <param name="system">The actor system used to attach the logger</param>
        private void InitializeLogger(ActorSystem system)
        {
            if (_output is null)
                return;

            var extSystem = (ExtendedActorSystem)system;
            var logger = extSystem.SystemActorOf(Props.Create(() => new TestOutputLogger(_output)), "log-test");
            logger.Ask<LoggerInitialized>(new InitializeLogger(system.EventStream), TimeSpan.FromSeconds(3))
                .ConfigureAwait(false).GetAwaiter().GetResult();
        }
        
        protected override void InitializeTest(ActorSystem system, ActorSystemSetup config, string actorSystemName, string testActorName)
        {
            // no-op, call after database are set up
        }

        [Fact]
        public async Task Should_Start_ActorSystem_wth_Sql_Persistence()
        {
            var timeout = 3.Seconds();

            // act
            (await _persistenceActor.Ask<string>(1, timeout)).Should().Be(Ack);
            (await _persistenceActor.Ask<string>(2, timeout)).Should().Be(Ack);
            var snapshot = await _persistenceActor.Ask<int[]>(GetAll, timeout);

            // assert
            snapshot.Should().BeEquivalentTo(new[] { 1, 2 });

            // kill + recreate actor with same PersistentId
            await _persistenceActor.GracefulStop(timeout);
            var myPersistentActor2 = Sys.ActorOf(Props.Create(() => new MyPersistenceActor(PId)));

            var snapshot2 = await myPersistentActor2.Ask<int[]>(GetAll, timeout);
            snapshot2.Should().BeEquivalentTo(new[] { 1, 2 });

            // validate configs
            var config = Sys.Settings.Config;
            config.GetString("akka.persistence.journal.plugin").Should().Be("akka.persistence.journal.sql");
            config.GetString("akka.persistence.snapshot-store.plugin").Should().Be("akka.persistence.snapshot-store.sql");
        }

        private sealed class MyPersistenceActor : ReceivePersistentActor
        {
            private List<int> _values = new();

            public MyPersistenceActor(string persistenceId)
            {
                PersistenceId = persistenceId;

                Recover<SnapshotOffer>(
                    offer =>
                    {
                        if (offer.Snapshot is IEnumerable<int> ints)
                            _values = new List<int>(ints);
                    });

                Recover<int>(_values.Add);

                Command<int>(
                    i =>
                    {
                        Persist(
                            i,
                            _ =>
                            {
                                _values.Add(i);
                                if (LastSequenceNr % 2 == 0)
                                    SaveSnapshot(_values);
                                Sender.Tell(Ack);
                            });
                    });

                Command<string>(str => str.Equals(GetAll), _ => Sender.Tell(_values.ToArray()));

                Command<SaveSnapshotSuccess>(_ => { });
            }

            public override string PersistenceId { get; }
        }
    }
}