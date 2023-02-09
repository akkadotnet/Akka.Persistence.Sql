﻿// -----------------------------------------------------------------------
//  <copyright file="TagTableColumnNames.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;

namespace Akka.Persistence.Sql.Linq2Db.Config;

public class TagTableColumnNames: IEquatable<TagTableColumnNames>
{
    public TagTableColumnNames(Configuration.Config config)
    {
        var cfg = config.GetConfig("columns");
        OrderingId = cfg.GetString("ordering-id", "ordering_id");
        Tag = cfg.GetString("tag-value", "tag");
    }
    
    public string OrderingId { get; }
    public string Tag { get; }

    public bool Equals(TagTableColumnNames other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return OrderingId == other.OrderingId;
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(this, obj)) return true;
        return obj is TagTableColumnNames tag && Equals(tag);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(OrderingId, Tag);
    }
}