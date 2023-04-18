// -----------------------------------------------------------------------
//  <copyright file="JournalTableOptions.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Text;
using Akka.Hosting;

namespace Akka.Persistence.Sql.Hosting
{
    public sealed class JournalTableOptions
    {
        public static JournalTableOptions Default => new()
        {
            UseWriterUuidColumn = true,
            TableName = "journal",

            OrderingColumnName = "ordering",
            DeletedColumnName = "deleted",
            PersistenceIdColumnName = "persistence_id",
            SequenceNumberColumnName = "sequence_number",
            CreatedColumnName = "created",
            TagsColumnName = "tags",
            MessageColumnName = "message",
            IdentifierColumnName = "identifier",
            ManifestColumnName = "manifest",
            WriterUuidColumnName = "writer_uuid"
        };

        public static JournalTableOptions SqlServer => new()
        {
            UseWriterUuidColumn = false,
            TableName = "EventJournal",

            OrderingColumnName = "Ordering",
            DeletedColumnName = "IsDeleted",
            PersistenceIdColumnName = "PersistenceId",
            SequenceNumberColumnName = "SequenceNr",
            CreatedColumnName = "Timestamp",
            TagsColumnName = "Tags",
            MessageColumnName = "Payload",
            IdentifierColumnName = "SerializerId",
            ManifestColumnName = "Manifest"
        };

        public static JournalTableOptions Sqlite => new()
        {
            UseWriterUuidColumn = false,
            TableName = "event_journal",

            OrderingColumnName = "ordering",
            DeletedColumnName = "is_deleted",
            PersistenceIdColumnName = "persistence_id",
            SequenceNumberColumnName = "sequence_nr",
            CreatedColumnName = "timestamp",
            TagsColumnName = "tags",
            MessageColumnName = "payload",
            IdentifierColumnName = "serializer_id",
            ManifestColumnName = "manifest"
        };

        public static JournalTableOptions PostgreSql => new()
        {
            UseWriterUuidColumn = false,
            TableName = "event_journal",

            OrderingColumnName = "ordering",
            DeletedColumnName = "is_deleted",
            PersistenceIdColumnName = "persistence_id",
            SequenceNumberColumnName = "sequence_nr",
            CreatedColumnName = "created_at",
            TagsColumnName = "tags",
            MessageColumnName = "payload",
            IdentifierColumnName = "serializer_id",
            ManifestColumnName = "manifest"
        };

        public static JournalTableOptions MySql => new()
        {
            UseWriterUuidColumn = false,
            TableName = "event_journal",

            OrderingColumnName = "ordering",
            DeletedColumnName = "is_deleted",
            PersistenceIdColumnName = "persistence_id",
            SequenceNumberColumnName = "sequence_nr",
            CreatedColumnName = "created_at",
            TagsColumnName = "tags",
            MessageColumnName = "payload",
            IdentifierColumnName = "serializer_id",
            ManifestColumnName = "manifest"
        };

        public bool? UseWriterUuidColumn { get; set; }
        public string? TableName { get; set; }

        public string? OrderingColumnName { get; set; }
        public string? DeletedColumnName { get; set; }
        public string? PersistenceIdColumnName { get; set; }
        public string? SequenceNumberColumnName { get; set; }
        public string? CreatedColumnName { get; set; }
        public string? TagsColumnName { get; set; }
        public string? MessageColumnName { get; set; }
        public string? IdentifierColumnName { get; set; }
        public string? ManifestColumnName { get; set; }
        public string? WriterUuidColumnName { get; set; }

        internal void Build(StringBuilder psb)
        {
            var sb = new StringBuilder();

            if (UseWriterUuidColumn is { })
                sb.AppendLine($"use-writer-uuid-column = {UseWriterUuidColumn.ToHocon()}");

            if (TableName is { })
                sb.AppendLine($"table-name = {TableName.ToHocon()}");

            var columnSb = new StringBuilder();
            if (OrderingColumnName is { })
                columnSb.AppendLine($"ordering = {OrderingColumnName.ToHocon()}");

            if (DeletedColumnName is { })
                columnSb.AppendLine($"deleted = {DeletedColumnName.ToHocon()}");

            if (PersistenceIdColumnName is { })
                columnSb.AppendLine($"persistence-id = {PersistenceIdColumnName.ToHocon()}");

            if (SequenceNumberColumnName is { })
                columnSb.AppendLine($"sequence-number = {SequenceNumberColumnName.ToHocon()}");

            if (CreatedColumnName is { })
                columnSb.AppendLine($"created = {CreatedColumnName.ToHocon()}");

            if (TagsColumnName is { })
                columnSb.AppendLine($"tags = {TagsColumnName.ToHocon()}");

            if (MessageColumnName is { })
                columnSb.AppendLine($"message = {MessageColumnName.ToHocon()}");

            if (IdentifierColumnName is { })
                columnSb.AppendLine($"identifier = {IdentifierColumnName.ToHocon()}");

            if (ManifestColumnName is { })
                columnSb.AppendLine($"manifest = {ManifestColumnName.ToHocon()}");

            if (WriterUuidColumnName is { })
                columnSb.AppendLine($"writer-uuid = {WriterUuidColumnName.ToHocon()}");

            if (columnSb.Length > 0)
            {
                sb.AppendLine("columns {");
                sb.Append(columnSb);
                sb.AppendLine("}");
            }

            if (sb.Length > 0)
            {
                psb.AppendLine("journal {");
                psb.Append(sb);
                psb.AppendLine("}");
            }
        }
    }
}
