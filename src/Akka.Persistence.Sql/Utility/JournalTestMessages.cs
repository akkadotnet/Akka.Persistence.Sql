// -----------------------------------------------------------------------
//  <copyright file="JournalTestMessages.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

namespace Akka.Persistence.Sql.Utility
{
    internal sealed class IsInitialized
    {
        public static readonly IsInitialized Instance = new();
        private IsInitialized() { }
    }

    internal sealed class Initialized
    {
        public static readonly Initialized Instance = new();
        private Initialized() { }
    }
}
