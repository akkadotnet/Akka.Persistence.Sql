﻿// -----------------------------------------------------------------------
//  <copyright file="ByteArraySnapshotSerializer.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Persistence.Sql.Config;
using Akka.Serialization;
using Akka.Util;

namespace Akka.Persistence.Sql.Snapshot
{
    public class ByteArraySnapshotSerializer : ISnapshotSerializer<SnapshotRow>
    {
        private readonly SnapshotConfig _config;
        private readonly Akka.Serialization.Serialization _serialization;

        public ByteArraySnapshotSerializer(Akka.Serialization.Serialization serialization, SnapshotConfig config)
        {
            _serialization = serialization;
            _config = config;
        }

        public Try<SnapshotRow> Serialize(SnapshotMetadata metadata, object snapshot)
            => Try<SnapshotRow>.From(() => ToSnapshotEntry(metadata, snapshot));

        public Try<SelectedSnapshot> Deserialize(SnapshotRow t)
            => Try<SelectedSnapshot>.From(() => ReadSnapshot(t));

        protected SelectedSnapshot ReadSnapshot(SnapshotRow reader)
        {
            var metadata = new SnapshotMetadata(
                reader.PersistenceId,
                reader.SequenceNumber,
                reader.Created);

            var snapshot = GetSnapshot(reader);

            return new SelectedSnapshot(metadata, snapshot);
        }

        protected object GetSnapshot(SnapshotRow reader)
        {
            var manifest = reader.Manifest;
            var binary = reader.Payload;

            if (reader.SerializerId is null)
            {
                var type = Type.GetType(manifest, true);

                // TODO: hack. Replace when https://github.com/akkadotnet/akka.net/issues/3811
                return Akka.Serialization.Serialization.WithTransport(
                    system: _serialization.System,
                    state: (serializer: _serialization.FindSerializerForType(type, _config.DefaultSerializer), binary, type),
                    action: state => state.serializer.FromBinary(state.binary, state.type));
            }

            var serializerId = reader.SerializerId.Value;
            return _serialization.Deserialize(binary, serializerId, manifest);
        }

        private SnapshotRow ToSnapshotEntry(SnapshotMetadata metadata, object snapshot)
        {
            var snapshotType = snapshot.GetType();
            var serializer = _serialization.FindSerializerForType(snapshotType, _config.DefaultSerializer);
            var binary = Akka.Serialization.Serialization.WithTransport(
                system: _serialization.System,
                state: (serializer, snapshot),
                action: state => state.serializer.ToBinary(state.snapshot));

            var manifest = serializer switch
            {
                SerializerWithStringManifest stringManifest => stringManifest.Manifest(snapshot),
                { IncludeManifest: true } => snapshotType.TypeQualifiedName(),
                _ => string.Empty
            };

            return new SnapshotRow
            {
                PersistenceId = metadata.PersistenceId,
                SequenceNumber = metadata.SequenceNr,
                Created = metadata.Timestamp,
                Manifest = manifest,
                Payload = binary,
                SerializerId = serializer.Identifier
            };
        }
    }
}
