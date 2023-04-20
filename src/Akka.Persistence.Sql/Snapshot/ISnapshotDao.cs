// -----------------------------------------------------------------------
//  <copyright file="ISnapshotDao.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Akka.Util;

namespace Akka.Persistence.Sql.Snapshot
{
    public interface ISnapshotDao
    {
        Task DeleteAllSnapshotsAsync(
            string persistenceId,
            CancellationToken cancellationToken = default);

        Task DeleteUpToMaxSequenceNrAsync(
            string persistenceId,
            long maxSequenceNr,
            CancellationToken cancellationToken = default);

        Task DeleteUpToMaxTimestampAsync(
            string persistenceId,
            DateTime maxTimestamp,
            CancellationToken cancellationToken = default);

        Task DeleteUpToMaxSequenceNrAndMaxTimestampAsync(
            string persistenceId,
            long maxSequenceNr,
            DateTime maxTimestamp,
            CancellationToken cancellationToken = default);

        Task<Option<SelectedSnapshot>> LatestSnapshotAsync(
            string persistenceId,
            CancellationToken cancellationToken = default);

        Task<Option<SelectedSnapshot>> SnapshotForMaxTimestampAsync(
            string persistenceId,
            DateTime timestamp,
            CancellationToken cancellationToken = default);

        Task<Option<SelectedSnapshot>> SnapshotForMaxSequenceNrAsync(
            string persistenceId,
            long sequenceNr,
            CancellationToken cancellationToken = default);

        Task<Option<SelectedSnapshot>> SnapshotForMaxSequenceNrAndMaxTimestampAsync(
            string persistenceId,
            long sequenceNr,
            DateTime timestamp,
            CancellationToken cancellationToken = default);

        Task DeleteAsync(
            string persistenceId,
            long sequenceNr,
            CancellationToken cancellationToken = default);

        Task SaveAsync(
            SnapshotMetadata snapshotMetadata,
            object snapshot,
            CancellationToken cancellationToken = default);
    }
}
