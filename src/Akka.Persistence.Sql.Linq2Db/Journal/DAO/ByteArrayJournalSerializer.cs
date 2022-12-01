using System;
using System.Collections.Immutable;
using System.Linq;
using Akka.Actor;
using Akka.Persistence.Sql.Linq2Db.Config;
using Akka.Persistence.Sql.Linq2Db.Journal.Types;
using Akka.Persistence.Sql.Linq2Db.Serialization;
using Akka.Serialization;
using Akka.Util;

namespace Akka.Persistence.Sql.Linq2Db.Journal.Dao
{
    /// <summary>
    /// Serializes <see cref="IPersistentRepresentation"/> 
    /// </summary>
    public sealed class ByteArrayJournalSerializer : FlowPersistentReprSerializer<JournalRow>
    {
        private readonly Akka.Serialization.Serialization _serializer;
        private readonly string _separator;
        private readonly IProviderConfig<JournalTableConfig> _journalConfig;
        private readonly string[] _separatorArray;
        private readonly TagWriteMode _tagWriteMode;

        public ByteArrayJournalSerializer(IProviderConfig<JournalTableConfig> journalConfig, Akka.Serialization.Serialization serializer, string separator)
        {
            _journalConfig = journalConfig;
            _serializer = serializer;
            _separator = separator;
            _separatorArray = new[] {_separator};
            _tagWriteMode = journalConfig.TableConfig.TagWriteMode;
        }

        /// <summary>
        /// Concatenates a set of tags using a provided separator. 
        /// </summary>
        /// <param name="tags"></param>
        /// <param name="separator"></param>
        /// <returns></returns>
        private static string StringSep(IImmutableSet<string> tags, string separator)
        {
            return tags.Count == 0 ? "" : $"{separator}{string.Join(separator, tags)}{separator}";
        }
        
        private JournalRow CreateJournalRow(IImmutableSet<string> tags, IPersistentRepresentation representation, long ts)
        {
            return _tagWriteMode switch
            {
                TagWriteMode.Csv => new JournalRow
                {
                    Tags = StringSep(tags, _separator),
                    Timestamp = representation.Timestamp == 0 ? ts : representation.Timestamp
                },
                
                TagWriteMode.TagTable => new JournalRow
                {
                    Tags = "",
                    TagArr = tags.ToArray(),
                    Timestamp = representation.Timestamp == 0 ? ts : representation.Timestamp
                },
                
                _ => throw new Exception($"Invalid Tag Write Mode! Was: {_tagWriteMode}")
            };
        }

        protected override Try<JournalRow> Serialize(
            IPersistentRepresentation persistentRepr,
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
                            persistentRepr, 
                            _serializer.FindSerializerForType(persistentRepr.Payload.GetType(), _journalConfig.DefaultSerializer),
                            CreateJournalRow(tTags,persistentRepr,timeStamp)
                        ),
                        action: state =>
                        {
                            var (representation, serializer, row) = state;
                            if (serializer is SerializerWithStringManifest withStringManifest)
                            {
                                row.Manifest = withStringManifest.Manifest(representation.Payload);
                            }
                            else if (serializer.IncludeManifest)
                            {
                                row.Manifest = representation.Payload.GetType().TypeQualifiedName();
                            }
                            else
                            {
                                row.Manifest = "";
                            }

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
                //object deserialized = null;
                var identifierMaybe = t.Identifier;
                if (!identifierMaybe.HasValue)
                {
                    var type = Type.GetType(t.Manifest, true);

                    // TODO: hack. Replace when https://github.com/akkadotnet/akka.net/issues/3811
                    return new Try<(IPersistentRepresentation, IImmutableSet<string>, long)>((
                        new Persistent(
                            payload: Akka.Serialization.Serialization.WithTransport(
                                system: _serializer.System, 
                                state: (_serializer.FindSerializerForType(type,_journalConfig.DefaultSerializer), message: t.Message, type),
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
                            .ToImmutableHashSet() ?? t.TagArr?.ToImmutableHashSet()?? ImmutableHashSet<string>.Empty,
                        t.Ordering));
                }
                
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
                        .ToImmutableHashSet() ?? t.TagArr?.ToImmutableHashSet()?? ImmutableHashSet<string>.Empty,
                    t.Ordering));
            }
            catch (Exception e)
            {
                return new Try<(IPersistentRepresentation, IImmutableSet<string>, long)>(e);
            }
            
        }
    }
}