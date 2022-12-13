// -----------------------------------------------------------------------
//  <copyright file="EventJournalTable.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;

namespace Akka.Persistence.Sql.Linq2Db.Config;

public sealed class EventJournalTableConfig: IEquatable<EventJournalTableConfig>
{
    public string Name { get; }
    public JournalTableColumnNames ColumnNames { get; }

    public EventJournalTableConfig(Configuration.Config config)
    {
        var journalConfig = config.GetConfig("journal");
        Name = journalConfig.GetString("table-name");
        ColumnNames = new JournalTableColumnNames(journalConfig);
    }

    public bool Equals(EventJournalTableConfig other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Name == other.Name && Equals(ColumnNames, other.ColumnNames);
    }

    public override bool Equals(object obj)
    {
        return ReferenceEquals(this, obj) || obj is EventJournalTableConfig other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, ColumnNames);
    }
}
