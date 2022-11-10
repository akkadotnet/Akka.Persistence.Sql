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

        public ByteArrayJournalSerializer(IProviderConfig<JournalTableConfig> journalConfig, Akka.Serialization.Serialization serializer, string separator)
        {
            _journalConfig = journalConfig;
            _serializer = serializer;
            _separator = separator;
            _separatorArray = new[] {_separator};
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

            return $"{separator}{string.Join(separator, tags)}{separator}";
        }

        protected override Try<JournalRow> Serialize(IPersistentRepresentation persistentRepr, IImmutableSet<string> tTags, long timeStamp = 0)
        {
            try
            {
                // TODO: hack. Replace when https://github.com/akkadotnet/akka.net/issues/3811
                return Akka.Serialization.Serialization.WithTransport(
                        _serializer.System, (persistentRepr
                            , _serializer.FindSerializerForType(persistentRepr.Payload.GetType(),_journalConfig.DefaultSerializer),
                            StringSep(tTags,_separator),
                            timeStamp
                            ),
                        state =>
                        {
                            var (_persistentRepr, serializer,tags,ts) = state;
                            string thisManifest = "";
                            if (serializer is SerializerWithStringManifest withStringManifest)
                            {
                                thisManifest =
                                    withStringManifest.Manifest(_persistentRepr.Payload);
                            }
                            else
                            {
                                if (serializer.IncludeManifest)
                                {
                                    thisManifest = _persistentRepr.Payload
                                        .GetType().TypeQualifiedName();
                                }
                            }
                            return new Try<JournalRow>(new JournalRow()
                            {
                                message =
                                    serializer.ToBinary(_persistentRepr.Payload),
                                manifest = thisManifest,
                                persistenceId = _persistentRepr.PersistenceId,
                                tags = tags,
                                Identifier = serializer.Identifier,
                                sequenceNumber = _persistentRepr.SequenceNr,
                                Timestamp = _persistentRepr.Timestamp == 0
                                    ? ts
                                    : _persistentRepr.Timestamp
                            });
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
                            .ToImmutableHashSet() ?? ImmutableHashSet<string>.Empty,
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
                            .ToImmutableHashSet() ?? ImmutableHashSet<string>.Empty,
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