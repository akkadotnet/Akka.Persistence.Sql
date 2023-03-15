// -----------------------------------------------------------------------
//  <copyright file="ContainsEvent.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;

namespace Akka.Persistence.Sql.Tests.Internal.Events
{
    public sealed class ContainsEvent
    {
        public Guid Guid { get; set; }
    }
}
