// -----------------------------------------------------------------------
//  <copyright file="FlowPersistentRepresentationSerializer.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;
using Akka.Streams.Dsl;
using Akka.Util;

namespace Akka.Persistence.Sql.Serialization
{
    public abstract class FlowPersistentRepresentationSerializer<T> : PersistentRepresentationSerializer<T>
    {
        public Flow<T, Try<(IPersistentRepresentation, string[], long)>, NotUsed> DeserializeFlow()
            => Flow.Create<T, NotUsed>().Select(Deserialize);
    }
}
