// -----------------------------------------------------------------------
//  <copyright file="MetadataTableOptions.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Text;
using Akka.Hosting;

namespace Akka.Persistence.Sql.Hosting
{
    public sealed class MetadataTableOptions
    {
        public static MetadataTableOptions Default => new()
        {
            TableName = "journal_metadata",
            PersistenceIdColumnName = "persistence_id",
            SequenceNumberColumnName = "sequence_number"
        };
        
        public static MetadataTableOptions SqlServer => new()
        {
            TableName = "Metadata",
            PersistenceIdColumnName = "PersistenceId",
            SequenceNumberColumnName = "SequenceNr"
        };
        
        public static MetadataTableOptions Sqlite => new()
        {
            TableName = "journal_metadata",
            PersistenceIdColumnName = "persistence_id",
            SequenceNumberColumnName = "sequence_nr"
        };
        
        public static MetadataTableOptions PostgreSql => new()
        {
            TableName = "metadata",
            PersistenceIdColumnName = "persistence_id",
            SequenceNumberColumnName = "sequence_nr"
        };
        
        public static MetadataTableOptions MySql => new()
        {
            TableName = "metadata",
            PersistenceIdColumnName = "persistence_id",
            SequenceNumberColumnName = "sequence_nr"
        };
        
        public string? TableName { get; set; }
        
        public string? PersistenceIdColumnName { get; set; }
        
        public string? SequenceNumberColumnName { get; set; }

        internal void Build(StringBuilder psb)
        {
            var sb = new StringBuilder();
            if (TableName is { })
                sb.AppendLine($"table-name = {TableName.ToHocon()}");
            
            var columnSb = new StringBuilder();
            if (PersistenceIdColumnName is { })
                columnSb.AppendLine($"persistence-id = {PersistenceIdColumnName.ToHocon()}");
            
            if (SequenceNumberColumnName is { })
                columnSb.AppendLine($"sequence-number = {SequenceNumberColumnName.ToHocon()}");

            if (columnSb.Length > 0)
            {
                sb.AppendLine("columns {");
                sb.Append(columnSb);
                sb.AppendLine("}");
            }

            if (sb.Length > 0)
            {
                psb.AppendLine("metadata {");
                psb.Append(sb);
                psb.AppendLine("}");
            }
        }
    }
}
