// -----------------------------------------------------------------------
//  <copyright file="TagTableOptions.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Text;
using Akka.Hosting;

namespace Akka.Persistence.Sql.Hosting
{
    public sealed class TagTableOptions
    {
        public static TagTableOptions Default => new()
        {
            TableName = "tags",
            OrderingColumnName = "ordering_id",
            TagColumnName = "tag",
            PersistenceIdColumnName = "persistence_id",
            SequenceNumberColumnName = "sequence_nr",
        };

        public string? TableName { get; set; }

        public string? OrderingColumnName { get; set; }
        public string? TagColumnName { get; set; }
        public string? PersistenceIdColumnName { get; set; }
        public string? SequenceNumberColumnName { get; set; }

        internal void Build(StringBuilder psb)
        {
            var sb = new StringBuilder();
            if (TableName is { })
                sb.AppendLine($"table-name = {TableName.ToHocon()}");

            var columnSb = new StringBuilder();
            if (OrderingColumnName is { })
                columnSb.AppendLine($"ordering-id = {OrderingColumnName.ToHocon()}");

            if (TagColumnName is { })
                columnSb.AppendLine($"tag-value = {TagColumnName.ToHocon()}");

            if (PersistenceIdColumnName is { })
                columnSb.AppendLine($"persistence-id = {PersistenceIdColumnName.ToHocon()}");

            if (SequenceNumberColumnName is { })
                columnSb.AppendLine($"sequence-nr = {SequenceNumberColumnName.ToHocon()}");

            if (columnSb.Length > 0)
            {
                sb.AppendLine("columns {");
                sb.Append(columnSb);
                sb.AppendLine("}");
            }

            if (sb.Length > 0)
            {
                psb.AppendLine("tag {");
                psb.Append(sb);
                psb.AppendLine("}");
            }
        }
    }
}
