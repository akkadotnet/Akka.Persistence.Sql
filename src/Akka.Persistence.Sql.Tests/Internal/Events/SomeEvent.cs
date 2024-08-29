// -----------------------------------------------------------------------
//  <copyright file="SomeEvent.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;

namespace Akka.Persistence.Sql.Tests.Internal.Events
{
    public sealed class SomeEvent : IEquatable<SomeEvent>
    {
        public string EventName { get; set; } = string.Empty;
        public int Number { get; set; }
        public Guid Guid { get; set; }

        public bool Equals(SomeEvent? other)
        {
            if (ReferenceEquals(null, other))
                return false;

            if (ReferenceEquals(this, other))
                return true;

            return EventName == other.EventName && Number == other.Number && Guid.Equals(other.Guid);
        }

        public override bool Equals(object? obj)
            => ReferenceEquals(this, obj) || (obj is SomeEvent other && Equals(other));

        public override int GetHashCode()
            => HashCode.Combine(EventName, Number, Guid);
    }
}
