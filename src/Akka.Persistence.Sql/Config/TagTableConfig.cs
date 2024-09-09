// -----------------------------------------------------------------------
//  <copyright file="TagTableConfig.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;

namespace Akka.Persistence.Sql.Config
{
    // ReSharper disable once InconsistentNaming
    public class TagTableConfig : IEquatable<TagTableConfig>
    {
        public TagTableConfig(Configuration.Config config)
        {
            var journalConfig = config.GetConfig("tag");
            Name = journalConfig.GetString("table-name");
            ColumnNames = new TagTableColumnNames(journalConfig);
        }

        public string Name { get; }

        public TagTableColumnNames ColumnNames { get; }

        public bool Equals(TagTableConfig? other)
        {
            if (ReferenceEquals(null, other))
                return false;

            if (ReferenceEquals(this, other))
                return true;

            return Name == other.Name && ColumnNames.Equals(other.ColumnNames);
        }

        public override bool Equals(object? obj)
        {
            if(obj is null)
                return false;
            
            if (ReferenceEquals(this, obj))
                return true;

            return obj is TagTableConfig cfg && Equals(cfg);
        }

        public override int GetHashCode()
            => HashCode.Combine(Name, ColumnNames);
    }
}
