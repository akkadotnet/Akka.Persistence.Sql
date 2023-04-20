// -----------------------------------------------------------------------
//  <copyright file="JournalTestMessages.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

namespace Akka.Persistence.Sql.Utility
{
    /// <summary>
    ///     Sent to the write journal to check if the write journal has completed initializing.
    /// </summary>
    internal sealed class IsInitialized
    {
        public static readonly IsInitialized Instance = new();
        private IsInitialized() { }
    }

    /// <summary>
    ///     Sent from the write journal in response to <see cref="IsInitialized" />
    ///     message, indicating that the write journal has completed initializing
    /// </summary>
    internal sealed class Initialized
    {
        public static readonly Initialized Instance = new();
        private Initialized() { }
    }
}
