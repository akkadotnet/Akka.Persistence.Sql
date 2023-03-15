// -----------------------------------------------------------------------
//  <copyright file="ISnapshotDao.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Akka.Util;

namespace Akka.Persistence.Sql.Snapshot
{
    public interface ISnapshotDao
    {
        Task DeleteAllSnapshots(
            string persistenceId);

        Task DeleteUpToMaxSequenceNr(
            string persistenceId,
            long maxSequenceNr);

        Task DeleteUpToMaxTimestamp(
            string persistenceId,
            DateTime maxTimestamp);

        Task DeleteUpToMaxSequenceNrAndMaxTimestamp(
            string persistenceId,
            long maxSequenceNr,
            DateTime maxTimestamp);

        Task<Option<SelectedSnapshot>> LatestSnapshot(
            string persistenceId);

        Task<Option<SelectedSnapshot>> SnapshotForMaxTimestamp(
            string persistenceId,
            DateTime timestamp);

        Task<Option<SelectedSnapshot>> SnapshotForMaxSequenceNr(
            string persistenceId,
            long sequenceNr);

        Task<Option<SelectedSnapshot>> SnapshotForMaxSequenceNrAndMaxTimestamp(
            string persistenceId,
            long sequenceNr,
            DateTime timestamp);

        Task Delete(
            string persistenceId,
            long sequenceNr);

        Task Save(
            SnapshotMetadata snapshotMetadata,
            object snapshot);
    }
}
