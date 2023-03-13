// -----------------------------------------------------------------------
//  <copyright file="OutputReceivedArgs.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;

namespace Akka.Persistence.Sql.Data.Compatibility.Tests.Internal
{
    public class OutputReceivedArgs : EventArgs
    {
        public OutputReceivedArgs(string output)
        {
            Output = output;
        }

        public string Output { get; }
    }
}

