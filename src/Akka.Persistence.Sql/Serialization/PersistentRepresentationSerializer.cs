// -----------------------------------------------------------------------
//  <copyright file="PersistentRepresentationSerializer.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Akka.Persistence.Journal;
using Akka.Util;

namespace Akka.Persistence.Sql.Serialization
{
    public abstract class PersistentRepresentationSerializer<T>
    {
        public List<Try<T[]>> Serialize(IEnumerable<AtomicWrite> messages, long timeStamp = 0)
            => messages.Select(
                aw =>
                {
                    // Hot Path:
                    // Instead of using Try.From (worst, extra delegate)
                    // Or Try/Catch (other penalties)
                    // We cheat here and use fast state checking of Try<T>/Option<T>.
                    // Also, if we are only persisting a single event
                    // We will only enumerate if we have more than one element.
                    if (aw.Payload is not IImmutableList<IPersistentRepresentation> payloads)
                    {
                        return new Try<T[]>(
                            new ArgumentNullException(
                                $"{aw.PersistenceId} received empty payload for sequenceNr range " +
                                $"{aw.LowestSequenceNr} - {aw.HighestSequenceNr}"));
                    }

                    // Preallocate our list; In the common case
                    // This saves a tiny bit of garbage
                    var retList = new T[payloads.Count];
                    for (var idx = 0; idx < payloads.Count; idx++)
                    {
                        var p = payloads[idx];
                        var ser = Serialize(p, timeStamp);
                        var opt = ser.Success;
                        if (opt.HasValue)
                        {
                            retList[idx] = opt.Value;
                        }
                        else
                        {
                            return new Try<T[]>(ser.Failure.Value);
                        }
                    }

                    return new Try<T[]>(retList);
                }).ToList();

        private List<Try<T[]>> HandleSerializeList(long timeStamp, AtomicWrite[] messages)
        {
            var fullSet = new List<Try<T[]>>(messages.Length);

            foreach (var message in messages)
            {
                if (message.Payload is not IImmutableList<IPersistentRepresentation> payloads)
                {
                    fullSet.Add(
                        new Try<T[]>(
                            new ArgumentNullException(
                                $"{message.PersistenceId} received empty payload for sequenceNr range " +
                                $"{message.LowestSequenceNr} - {message.HighestSequenceNr}")));
                }
                else
                {
                    fullSet.Add(SerializerItem(timeStamp, payloads));
                }
            }

            return fullSet;
        }

        private Try<T[]> SerializerItem(long timeStamp, IImmutableList<IPersistentRepresentation> payloads)
        {
            var retList = new T[payloads.Count];
            for (var j = 0; j < payloads.Count; j++)
            {
                var ser = Serialize(payloads[j], timeStamp);
                var opt = ser.Success;

                if (opt.HasValue)
                {
                    retList[j] = opt.Value;
                }
                else
                {
                    return new Try<T[]>(ser.Failure.Value);
                }
            }

            return new Try<T[]>(retList);
        }

        public Try<T> Serialize(IPersistentRepresentation persistentRepresentation, long timeStamp = 0)
            => persistentRepresentation.Payload switch
            {
                Tagged t => Serialize(persistentRepresentation.WithPayload(t.Payload), t.Tags, timeStamp),

                _ => Serialize(persistentRepresentation, ImmutableHashSet<string>.Empty, timeStamp),
            };

        protected abstract Try<T> Serialize(
            IPersistentRepresentation persistentRepresentation,
            IImmutableSet<string> tTags,
            long timeStamp = 0);

        protected abstract Try<(IPersistentRepresentation, string[], long)> Deserialize(T t);
    }
}
