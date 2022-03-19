using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Akka.Persistence.Journal;
using Akka.Persistence.Sql.Linq2Db.Utility;
using LanguageExt;
using static LanguageExt.Prelude;

namespace Akka.Persistence.Sql.Linq2Db.Serialization
{
    public abstract class PersistentReprSerializer<T>
    {
        public List<Akka.Util.Try<T[]>> Serialize(
            IEnumerable<AtomicWrite> messages, long timeStamp = 0)
        {
            return messages.Select(aw =>
            {
                //Hot Path:
                //Instead of using Try.From (worst, extra delegate)
                //Or Try/Catch (other penalties)
                //We cheat here and use fast state checking of Try<T>/Option<T>.
                //Also, if we are only persisting a single event
                //We will only enumerate if we have more than one element.
                var payloads =
                    (aw.Payload as IImmutableList<IPersistentRepresentation>
                    );
                if (payloads is null)
                {
                    return new Util.Try<T[]>(
                        new ArgumentNullException(
                            $"{aw.PersistenceId} received empty payload for sequenceNr range " +
                            $"{aw.LowestSequenceNr} - {aw.HighestSequenceNr}"));
                }

                //Preallocate our list; In the common case
                //This saves a tiny bit of garbage
                var retList = new T[payloads.Count];
                if (payloads.Count == 1)
                {
                    // If there's only one payload
                    // Don't allocate the enumerable.
                    var ser = Serialize(payloads[0], timeStamp);
                    var opt = ser.Success;
                    if (opt.HasValue)
                    {
                        retList[0] = opt.Value;
                        return new Util.Try<T[]>(retList);
                    }
                    else
                    {
                        return new Util.Try<T[]>(ser.Failure.Value);
                    }
                }
                else
                {
                    int idx = 0;
                    foreach (var p in payloads)
                    {
                        var ser = Serialize(p, timeStamp);
                        var opt = ser.Success;
                        if (opt.HasValue)
                        {
                            retList[idx] = opt.Value;
                            idx = idx + 1;
                        }
                        else
                        {
                            return new Util.Try<T[]>(ser.Failure.Value);
                        }
                    }

                    return new Util.Try<T[]>(retList);
                }

                //return new Util.Try<List<T>>(retList);
            }).ToList();
        }

        private List<Util.Try<T[]>> HandleSerializeList(long timeStamp, AtomicWrite[] msgArr)
        {
            List<Akka.Util.Try<T[]>> fullSet =
                new List<Util.Try<T[]>>(msgArr.Length);
            for (int i = 0; i < msgArr.Length; i++)
            {
                var payloads =
                    (msgArr[i].Payload as
                        IImmutableList<IPersistentRepresentation>
                    );
                if (payloads is null)
                {
                    fullSet.Add(new Util.Try<T[]>(
                        new ArgumentNullException(
                            $"{msgArr[i].PersistenceId} received empty payload for sequenceNr range " +
                            $"{msgArr[i].LowestSequenceNr} - {msgArr[i].HighestSequenceNr}")));
                }
                else
                {
                    fullSet.Add(serializerItem(timeStamp, payloads));
                }
            }

            return fullSet;
        }

        private Util.Try<T[]> serializerItem(long timeStamp, IImmutableList<IPersistentRepresentation> payloads)
        {
            var retList = new T[payloads.Count];
            for (int j = 0; j < payloads.Count; j++)
            {
                var ser = Serialize(payloads[j], timeStamp);
                var opt = ser.Success;
                if (opt.HasValue)
                {
                    retList[j] = opt.Value;
                }
                else
                {
                    return new Util.Try<T[]>(ser.Failure.Value);
                }
            }

            return new Util.Try<T[]>(retList);
        }


        public Akka.Util.Try<T> Serialize(IPersistentRepresentation persistentRepr, long timeStamp = 0)
        {
            switch (persistentRepr.Payload)
            {
                case Tagged t:
                    return Serialize(persistentRepr.WithPayload(t.Payload), t.Tags, timeStamp);
                default:
                    return Serialize(persistentRepr,
                        ImmutableHashSet<string>.Empty, timeStamp);
            }
        }

        protected abstract Akka.Util.Try<T> Serialize(
            IPersistentRepresentation persistentRepr,
            IImmutableSet<string> tTags, long timeStamp =0);

        protected abstract Akka.Util.Try<(IPersistentRepresentation, IImmutableSet<string>, long)>
            Deserialize(
                T t);
    }
}