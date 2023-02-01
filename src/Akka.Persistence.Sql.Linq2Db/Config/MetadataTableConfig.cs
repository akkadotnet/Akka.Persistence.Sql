// -----------------------------------------------------------------------
//  <copyright file="MetadataTable.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;

namespace Akka.Persistence.Sql.Linq2Db.Config;

public sealed class MetadataTableConfig: IEquatable<MetadataTableConfig>
{
    public string Name { get; }
    public MetadataTableColumnNames ColumnNames { get; }

    public MetadataTableConfig(Configuration.Config config)
    {
        var journalConfig = config.GetConfig("metadata");
        Name = journalConfig.GetString("table-name");
        ColumnNames = new MetadataTableColumnNames(journalConfig);
    }

    public bool Equals(MetadataTableConfig other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Name == other.Name && Equals(ColumnNames, other.ColumnNames);
    }

    public override bool Equals(object obj)
    {
        return ReferenceEquals(this, obj) || obj is MetadataTableConfig other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, ColumnNames);
    }
}