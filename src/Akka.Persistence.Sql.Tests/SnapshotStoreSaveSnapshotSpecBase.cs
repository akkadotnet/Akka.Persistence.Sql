// -----------------------------------------------------------------------
//  <copyright file="SnapshotStoreSaveSnapshotSpecBase.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Akka.Persistence.TCK.Serialization;
using Akka.Persistence.TCK.Snapshot;
using FluentAssertions;
using FluentAssertions.Extensions;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Tests
{
    public class SnapshotStoreSaveSnapshotSpecBase: SnapshotStoreSaveSnapshotSpec
    {
        protected SnapshotStoreSaveSnapshotSpecBase(Configuration.Config config, string actorSystemName, ITestOutputHelper? output = null) 
            : base(config, actorSystemName, output)
        {
            
        }
        
        [Fact(DisplayName = "Multiple SaveSnapshot invocation with default metadata should not throw")]
        public async Task MultipleSnapshotsWithDefaultMetadata()
        {
            var persistence = Persistence.Instance.Apply(Sys);
            var snapshotStore = persistence.SnapshotStoreFor(null);
            var snap = new TestPayload(SenderProbe.Ref);
        
            var now = DateTime.UtcNow;
            var metadata = new SnapshotMetadata(PersistenceId, 0, DateTime.MinValue);
            snapshotStore.Tell(new SaveSnapshot(metadata, snap), SenderProbe);
            var success = await SenderProbe.ExpectMsgAsync<SaveSnapshotSuccess>(10.Minutes());
            success.Metadata.PersistenceId.Should().Be(metadata.PersistenceId);
            success.Metadata.Timestamp.Should().BeAfter(now);
            success.Metadata.SequenceNr.Should().Be(metadata.SequenceNr);
        
            now = DateTime.UtcNow;
            metadata = new SnapshotMetadata(PersistenceId, 0, DateTime.MinValue);
            snapshotStore.Tell(new SaveSnapshot(metadata, 3), SenderProbe);
            success = await SenderProbe.ExpectMsgAsync<SaveSnapshotSuccess>();
            success.Metadata.PersistenceId.Should().Be(metadata.PersistenceId);
            success.Metadata.Timestamp.Should().BeAfter(now);
            success.Metadata.SequenceNr.Should().Be(metadata.SequenceNr);
        }        
    }
}
