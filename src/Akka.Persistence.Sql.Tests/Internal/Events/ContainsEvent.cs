using System;

namespace Akka.Persistence.Sql.Tests.Internal.Events
{
    public sealed class ContainsEvent
    {
        public Guid Guid { get; set; }
    }
}
