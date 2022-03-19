using System;
using System.Collections.Immutable;
using System.Linq;
using Akka.Actor;
using Akka.Persistence.Sql.Linq2Db.Config;
using Akka.Persistence.Sql.Linq2Db.Journal.Types;
using Akka.Persistence.Sql.Linq2Db.Serialization;
using Akka.Serialization;
using Akka.Streams.Dsl;
using Akka.Util;

namespace Akka.Persistence.Sql.Linq2Db.Journal.DAO
{
    /// <summary>
    /// Serializes <see cref="IPersistentRepresentation"/> 
    /// </summary>
    public class ByteArrayJournalSerializer : FlowPersistentReprSerializer<JournalRow>
    {
        private Akka.Serialization.Serialization _serializer;
        private string _separator;
        private IProviderConfig<JournalTableConfig> _journalConfig;
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
        private static string StringSep(IImmutableSet<string> tags,
            string separator)
        {
            if (tags.Count == 0)
            {
                return "";
            }

            return tags.Aggregate((tl, tr) =>
                tl + separator + tr);
        }

        private JournalRow CreateJournalRow(
            IImmutableSet<string> tags, IPersistentRepresentation _persistentRepr, long ts)
        {
            switch (_tagWriteMode)
            {
                case TagWriteMode.CommaSeparatedArray:
                    return new JournalRow()
                    {
                        tags = StringSep(tags, _separator),
                        Timestamp = _persistentRepr.Timestamp == 0
                            ? ts
                            : _persistentRepr.Timestamp
                    };
                case TagWriteMode.TagTable:
                    return new JournalRow()
                    {
                        tags = "",
                        tagArr = tags.ToArray(),
                        Timestamp = _persistentRepr.Timestamp == 0
                            ? ts
                            : _persistentRepr.Timestamp
                    };
                case TagWriteMode.CommaSeparatedArrayAndTagTable:
                    return new JournalRow()
                    {
                        tags = StringSep(tags, _separator),
                        tagArr = tags.ToArray(),
                        Timestamp = _persistentRepr.Timestamp == 0
                            ? ts
                            : _persistentRepr.Timestamp
                    };
                default:
                    throw new Exception("Invalid Tag Write Mode!");
            }
        }
        

        protected override Try<JournalRow> Serialize(IPersistentRepresentation persistentRepr, IImmutableSet<string> tTags, long timeStamp = 0)
        {
            try
            {
                // TODO: hack. Replace when https://github.com/akkadotnet/akka.net/issues/3811
                return Akka.Serialization.Serialization.WithTransport(
                        _serializer.System, (persistentRepr
                            , _serializer.FindSerializerForType(persistentRepr.Payload.GetType(),_journalConfig.DefaultSerializer),
                            CreateJournalRow(tTags,persistentRepr,timeStamp)
                            ),
                        state =>
                        {
                            var (_persistentRepr, serializer,row) = state;
                            
                            if (serializer is SerializerWithStringManifest withStringManifest)
                            {
                                row.manifest =
                                    withStringManifest.Manifest(_persistentRepr.Payload);
                            }
                            else if (serializer.IncludeManifest)
                            {
                                row.manifest = _persistentRepr.Payload
                                        .GetType().TypeQualifiedName();
                            }
                            else
                            {
                                row.manifest = "";
                            }

                            {
                                row.message =
                                    serializer.ToBinary(_persistentRepr
                                        .Payload);
                                row.persistenceId =
                                    _persistentRepr.PersistenceId;
                                row.Identifier = serializer.Identifier;
                                row.sequenceNumber = _persistentRepr.SequenceNr;
                            }
                            return new Try<JournalRow>(row
                            );
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
                if (identifierMaybe.HasValue == false)
                {
                    var type = System.Type.GetType(t.manifest, true);

                    // TODO: hack. Replace when https://github.com/akkadotnet/akka.net/issues/3811
                    
                    return new Try<(IPersistentRepresentation, IImmutableSet<string>
                        , long)>((
                        new Persistent(    Akka.Serialization.Serialization.WithTransport(
                                _serializer.System, (_serializer.FindSerializerForType(type,_journalConfig.DefaultSerializer),t.message,type),
                                (state) =>
                                {
                                    return state.Item1.FromBinary(
                                        state.message, state.type);
                                }), t.sequenceNumber,
                            t.persistenceId,
                            t.manifest, t.deleted, ActorRefs.NoSender, null, t.Timestamp),
                        t.tags?.Split(_separatorArray,
                                StringSplitOptions.RemoveEmptyEntries)
                            .ToImmutableHashSet() ?? t.tagArr?.ToImmutableHashSet()?? ImmutableHashSet<string>.Empty,
                        t.ordering));
                }
                else
                {
                    return new Try<(IPersistentRepresentation, IImmutableSet<string>
                        , long)>((
                        new Persistent(_serializer.Deserialize(t.message,
                                    identifierMaybe.Value,t.manifest), t.sequenceNumber,
                            t.persistenceId,
                            t.manifest, t.deleted, ActorRefs.NoSender, null, t.Timestamp),
                        t.tags?.Split(_separatorArray,
                                StringSplitOptions.RemoveEmptyEntries)
                            .ToImmutableHashSet() ??t.tagArr?.ToImmutableHashSet()?? ImmutableHashSet<string>.Empty,
                        t.ordering));
                    // TODO: hack. Replace when https://github.com/akkadotnet/akka.net/issues/3811
                }
            }
            catch (Exception e)
            {
                return new Try<(IPersistentRepresentation, IImmutableSet<string>
                    , long)>(e);
            }
            
        }
    }
}