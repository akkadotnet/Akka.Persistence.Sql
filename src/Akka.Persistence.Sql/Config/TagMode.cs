// -----------------------------------------------------------------------
//  <copyright file="TagMode.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

namespace Akka.Persistence.Sql.Config
{
    // ReSharper disable once InconsistentNaming
    public enum TagMode
    {
        Csv,

        // ReSharper disable once InconsistentNaming
        TagTable,

        Both,
    }
}
