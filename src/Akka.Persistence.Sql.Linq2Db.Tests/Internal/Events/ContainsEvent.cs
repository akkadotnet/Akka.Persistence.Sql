using System;

namespace Akka.Persistence.Sql.Linq2Db.Tests.Internal.Events
{
    public sealed class ContainsEvent
    {
        public Guid Guid { get; set; } 
    }
}