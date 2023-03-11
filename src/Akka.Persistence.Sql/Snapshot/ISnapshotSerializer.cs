﻿using Akka.Util;

namespace Akka.Persistence.Sql.Snapshot
{
    public interface ISnapshotSerializer<T>
    {
        Try<T> Serialize(SnapshotMetadata metadata, object snapshot);

        Try<SelectedSnapshot> Deserialize(T t);
    }
}
