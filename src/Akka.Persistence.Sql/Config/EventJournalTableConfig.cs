// -----------------------------------------------------------------------
//  <copyright file="EventJournalTableConfig.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;

namespace Akka.Persistence.Sql.Config
{
    public sealed class EventJournalTableConfig : IEquatable<EventJournalTableConfig>
    {
        public EventJournalTableConfig(Configuration.Config config, string tableMapName)
        {
            var journalConfig = config.GetConfig("journal");
            TableMapName = tableMapName;
            Name = journalConfig.GetString("table-name");
            ColumnNames = new JournalTableColumnNames(journalConfig);
        }

        public string TableMapName { get; }
        
        public string Name { get; }

        public JournalTableColumnNames ColumnNames { get; }

        public bool Equals(EventJournalTableConfig other)
        {
            if (ReferenceEquals(null, other))
                return false;

            if (ReferenceEquals(this, other))
                return true;

            return Name == other.Name && Equals(ColumnNames, other.ColumnNames);
        }

        public override bool Equals(object obj)
            => ReferenceEquals(this, obj) || (obj is EventJournalTableConfig other && Equals(other));

        public override int GetHashCode()
            => HashCode.Combine(Name, ColumnNames);
    }
}
