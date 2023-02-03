// -----------------------------------------------------------------------
//  <copyright file="JournalTagRow.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2023 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

namespace Akka.Persistence.Sql.Linq2Db.Journal.Types;

public sealed class JournalTagRow
{
    public long JournalOrderingId { get; set; }
    
    public string TagValue { get; set; }
}
