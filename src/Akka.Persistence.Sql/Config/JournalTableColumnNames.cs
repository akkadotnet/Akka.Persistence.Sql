// -----------------------------------------------------------------------
//  <copyright file="JournalTableColumnNames.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;

namespace Akka.Persistence.Sql.Config
{
    public class JournalTableColumnNames : IEquatable<JournalTableColumnNames>
    {
        public JournalTableColumnNames(Configuration.Config config)
        {
            var cfg = config.GetConfig("columns");
            Ordering = cfg.GetString("ordering", "ordering");
            Deleted = cfg.GetString("deleted", "deleted");
            PersistenceId = cfg.GetString("persistence-id", "persistence_id");
            SequenceNumber = cfg.GetString("sequence-number", "sequence_number");
            Created = cfg.GetString("created", "created");
            Tags = cfg.GetString("tags", "tags");
            Message = cfg.GetString("message", "message");
            Identifier = cfg.GetString("identifier", "identifier");
            Manifest = cfg.GetString("manifest", "manifest");
            WriterUuid = cfg.GetString("writer-uuid", "writer_uuid");
        }

        public string Ordering { get; }

        public string Deleted { get; }

        public string PersistenceId { get; }

        public string SequenceNumber { get; }

        public string Created { get; }

        public string Tags { get; }

        public string Message { get; }

        public string Identifier { get; }

        public string Manifest { get; }

        public string WriterUuid { get; }

        public bool Equals(JournalTableColumnNames other)
            => other is not null &&
               Ordering == other.Ordering &&
               Deleted == other.Deleted &&
               PersistenceId == other.PersistenceId &&
               SequenceNumber == other.SequenceNumber &&
               Created == other.Created &&
               Tags == other.Tags &&
               Message == other.Message &&
               Identifier == other.Identifier &&
               Manifest == other.Manifest;

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;

            return obj is JournalTableColumnNames j && Equals(j);
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCode();
            hashCode.Add(Ordering);
            hashCode.Add(Deleted);
            hashCode.Add(PersistenceId);
            hashCode.Add(SequenceNumber);
            hashCode.Add(Created);
            hashCode.Add(Tags);
            hashCode.Add(Message);
            hashCode.Add(Identifier);
            hashCode.Add(Manifest);
            return hashCode.ToHashCode();
        }
    }
}
