// -----------------------------------------------------------------------
//  <copyright file="TagMode.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

namespace Akka.Persistence.Sql.Config
{
    public enum TagMode
    {
        Csv,
        TagTable,
        Both,
    }
}
