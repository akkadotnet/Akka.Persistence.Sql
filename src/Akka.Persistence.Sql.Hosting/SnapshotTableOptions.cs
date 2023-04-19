// -----------------------------------------------------------------------
//  <copyright file="SnapshotTableOptions.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Text;
using Akka.Hosting;

namespace Akka.Persistence.Sql.Hosting
{
    public sealed class SnapshotTableOptions
    {
        public static SnapshotTableOptions Default => new()
        {
            TableName = "snapshot",
            PersistenceIdColumnName = "persistence_id",
            SequenceNumberColumnName = "sequence_number",
            CreatedColumnName = "created",
            SnapshotColumnName = "snapshot",
            ManifestColumnName = "manifest",
            SerializerIdColumnName = "serializer_id",
        };

        public static SnapshotTableOptions SqlServer => new()
        {
            TableName = "SnapshotStore",
            PersistenceIdColumnName = "PersistenceId",
            SequenceNumberColumnName = "SequenceNr",
            CreatedColumnName = "Timestamp",
            SnapshotColumnName = "Snapshot",
            ManifestColumnName = "Manifest",
            SerializerIdColumnName = "SerializerId",
        };

        public static SnapshotTableOptions Sqlite => new()
        {
            TableName = "snapshot",
            PersistenceIdColumnName = "persistence_id",
            SequenceNumberColumnName = "sequence_nr",
            CreatedColumnName = "created_at",
            SnapshotColumnName = "payload",
            ManifestColumnName = "manifest",
            SerializerIdColumnName = "serializer_id",
        };

        public static SnapshotTableOptions PostgreSql => new()
        {
            TableName = "snapshot_store",
            PersistenceIdColumnName = "persistence_id",
            SequenceNumberColumnName = "sequence_nr",
            CreatedColumnName = "created_at",
            SnapshotColumnName = "payload",
            ManifestColumnName = "manifest",
            SerializerIdColumnName = "serializer_id",
        };

        public static SnapshotTableOptions MySql => new()
        {
            TableName = "snapshot_store",
            PersistenceIdColumnName = "persistence_id",
            SequenceNumberColumnName = "sequence_nr",
            CreatedColumnName = "created_at",
            SnapshotColumnName = "snapshot",
            ManifestColumnName = "manifest",
            SerializerIdColumnName = "serializer_id",
        };

        public string? TableName { get; set; }

        public string? PersistenceIdColumnName { get; set; }
        public string? SequenceNumberColumnName { get; set; }
        public string? CreatedColumnName { get; set; }
        public string? SnapshotColumnName { get; set; }
        public string? ManifestColumnName { get; set; }
        public string? SerializerIdColumnName { get; set; }

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

            if (CreatedColumnName is { })
                columnSb.AppendLine($"created = {CreatedColumnName.ToHocon()}");

            if (SnapshotColumnName is { })
                columnSb.AppendLine($"snapshot = {SnapshotColumnName.ToHocon()}");

            if (ManifestColumnName is { })
                columnSb.AppendLine($"manifest = {ManifestColumnName.ToHocon()}");

            if (SerializerIdColumnName is { })
                columnSb.AppendLine($"serializerId = {SerializerIdColumnName.ToHocon()}");

            if (columnSb.Length > 0)
            {
                sb.AppendLine("columns {");
                sb.Append(columnSb);
                sb.AppendLine("}");
            }

            if (sb.Length > 0)
            {
                psb.AppendLine("snapshot {");
                psb.Append(sb);
                psb.AppendLine("}");
            }
        }
    }
}
