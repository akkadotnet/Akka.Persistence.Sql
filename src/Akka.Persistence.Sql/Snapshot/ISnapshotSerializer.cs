// -----------------------------------------------------------------------
//  <copyright file="ISnapshotSerializer.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Util;

namespace Akka.Persistence.Sql.Snapshot
{
    public interface ISnapshotSerializer<T>
    {
        Try<T> Serialize(
            SnapshotMetadata metadata,
            object snapshot);

        Try<SelectedSnapshot> Deserialize(
            T t);
    }
}
