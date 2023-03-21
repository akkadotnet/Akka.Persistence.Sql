// -----------------------------------------------------------------------
//  <copyright file="ByteArrayJournalSerializer.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using Akka.Actor;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Journal.Types;
using Akka.Persistence.Sql.Serialization;
using Akka.Serialization;
using Akka.Util;

namespace Akka.Persistence.Sql.Journal.Dao
{
    /// <summary>
    ///     Serializes <see cref="IPersistentRepresentation" />
    /// </summary>
    public sealed class ByteArrayJournalSerializer : FlowPersistentReprSerializer<JournalRow>
    {
        private readonly IProviderConfig<JournalTableConfig> _journalConfig;
        private readonly string _separator;
        private readonly string[] _separatorArray;
        private readonly Akka.Serialization.Serialization _serializer;
        private readonly TagMode _tagWriteMode;

        public ByteArrayJournalSerializer(
            IProviderConfig<JournalTableConfig> journalConfig,
            Akka.Serialization.Serialization serializer,
            string separator)
        {
            _journalConfig = journalConfig;
            _serializer = serializer;
            _separator = separator;
            _separatorArray = new[] { _separator };
            _tagWriteMode = journalConfig.PluginConfig.TagMode;
        }

        /// <summary>
        ///     Concatenates a set of tags using a provided separator.
        /// </summary>
        /// <param name="tags"></param>
        /// <param name="separator"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string StringSep(IImmutableSet<string> tags, string separator)
            => tags.Count == 0
                ? string.Empty
                : $"{separator}{string.Join(separator, tags)}{separator}";

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static JournalRow CreateJournalRow(
            IImmutableSet<string> tags,
            IPersistentRepresentation representation,
            long timestamp,
            TagMode tagWriteMode,
            string separator)
            => tagWriteMode switch
            {
                TagMode.Csv => new JournalRow
                {
                    Tags = StringSep(tags, separator),
                    Timestamp = representation.Timestamp == 0
                        ? timestamp
                        : representation.Timestamp
                },

                TagMode.TagTable => new JournalRow
                {
                    Tags = string.Empty,
                    TagArr = tags.ToArray(),
                    Timestamp = representation.Timestamp == 0
                        ? timestamp
                        : representation.Timestamp
                },

                TagMode.Both => new JournalRow
                {
                    Tags = StringSep(tags, separator),
                    TagArr = tags.ToArray(),
                    Timestamp = representation.Timestamp == 0
                        ? timestamp
                        : representation.Timestamp
                },

                _ => throw new Exception($"Invalid Tag Write Mode! Was: {tagWriteMode}")
            };

        protected override Try<JournalRow> Serialize(
            IPersistentRepresentation persistentRepresentation,
            IImmutableSet<string> tTags,
            long timeStamp = 0)
        {
            try
            {
                // TODO: hack. Replace when https://github.com/akkadotnet/akka.net/issues/3811
                return Akka.Serialization.Serialization
                    .WithTransport(
                        system: _serializer.System,
                        state: (
                            persistentRepr: persistentRepresentation,
                            _serializer.FindSerializerForType(
                                persistentRepresentation.Payload.GetType(),
                                _journalConfig.DefaultSerializer),
                            CreateJournalRow(tTags, persistentRepresentation, timeStamp, _tagWriteMode, _separator)
                        ),
                        action: state =>
                        {
                            var (representation, serializer, row) = state;

                            row.Manifest = serializer switch
                            {
                                SerializerWithStringManifest stringManifest => stringManifest.Manifest(representation.Payload),
                                { IncludeManifest: true } => representation.Payload.GetType().TypeQualifiedName(),
                                _ => string.Empty
                            };

                            row.Message = serializer.ToBinary(representation.Payload);
                            row.PersistenceId = representation.PersistenceId;
                            row.Identifier = serializer.Identifier;
                            row.SequenceNumber = representation.SequenceNr;
                            row.EventManifest = representation.Manifest;

                            return new Try<JournalRow>(row);
                        });
            }
            catch (Exception e)
            {
                return new Try<JournalRow>(e);
            }
        }

        protected override Try<(IPersistentRepresentation, IImmutableSet<string>, long)> Deserialize(JournalRow t)
        {
            try
            {
                var identifierMaybe = t.Identifier;
                if (identifierMaybe.HasValue)
                {
                    // TODO: hack. Replace when https://github.com/akkadotnet/akka.net/issues/3811
                    return new Try<(IPersistentRepresentation, IImmutableSet<string>, long)>((
                        new Persistent(
                            payload: _serializer.Deserialize(t.Message, identifierMaybe.Value, t.Manifest),
                            sequenceNr: t.SequenceNumber,
                            persistenceId: t.PersistenceId,
                            manifest: t.EventManifest ?? t.Manifest,
                            isDeleted: t.Deleted,
                            sender: ActorRefs.NoSender,
                            writerGuid: null,
                            timestamp: t.Timestamp),
                        t.Tags?
                            .Split(_separatorArray, StringSplitOptions.RemoveEmptyEntries)
                            .ToImmutableHashSet() ?? t.TagArr?.ToImmutableHashSet() ?? ImmutableHashSet<string>.Empty,
                        t.Ordering));
                }

                var type = Type.GetType(t.Manifest, true);

                // TODO: hack. Replace when https://github.com/akkadotnet/akka.net/issues/3811
                return new Try<(IPersistentRepresentation, IImmutableSet<string>, long)>((
                    new Persistent(
                        payload: Akka.Serialization.Serialization.WithTransport(
                            system: _serializer.System,
                            state: (_serializer.FindSerializerForType(type, _journalConfig.DefaultSerializer), message: t.Message, type),
                            action: state => state.Item1.FromBinary(state.message, state.type)),
                        sequenceNr: t.SequenceNumber,
                        persistenceId: t.PersistenceId,
                        manifest: t.EventManifest ?? t.Manifest,
                        isDeleted: t.Deleted,
                        sender: ActorRefs.NoSender,
                        writerGuid: null,
                        timestamp: t.Timestamp),
                    t.Tags?
                        .Split(_separatorArray, StringSplitOptions.RemoveEmptyEntries)
                        .ToImmutableHashSet() ?? t.TagArr?.ToImmutableHashSet() ?? ImmutableHashSet<string>.Empty,
                    t.Ordering));

            }
            catch (Exception e)
            {
                return new Try<(IPersistentRepresentation, IImmutableSet<string>, long)>(e);
            }
        }
    }
}
