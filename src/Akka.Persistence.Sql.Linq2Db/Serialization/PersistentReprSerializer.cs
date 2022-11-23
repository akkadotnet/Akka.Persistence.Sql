using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Akka.Persistence.Journal;

namespace Akka.Persistence.Sql.Linq2Db.Serialization
{
    public abstract class PersistentReprSerializer<T>
    {
        public List<Util.Try<T[]>> Serialize(IEnumerable<AtomicWrite> messages, long timeStamp = 0)
        {
            return messages.Select(aw =>
            {
                //Hot Path:
                //Instead of using Try.From (worst, extra delegate)
                //Or Try/Catch (other penalties)
                //We cheat here and use fast state checking of Try<T>/Option<T>.
                //Also, if we are only persisting a single event
                //We will only enumerate if we have more than one element.
                if (aw.Payload is not IImmutableList<IPersistentRepresentation> payloads)
                {
                    return new Util.Try<T[]>(
                        new ArgumentNullException(
                            $"{aw.PersistenceId} received empty payload for sequenceNr range " +
                            $"{aw.LowestSequenceNr} - {aw.HighestSequenceNr}"));
                }
                
                //Preallocate our list; In the common case
                //This saves a tiny bit of garbage
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
                        return new Util.Try<T[]>(ser.Failure.Value);
                    }
                }

                return new Util.Try<T[]>(retList);
            }).ToList();
        }

        private List<Util.Try<T[]>> HandleSerializeList(long timeStamp, AtomicWrite[] msgArr)
        {
            var fullSet = new List<Util.Try<T[]>>(msgArr.Length);
            for (var i = 0; i < msgArr.Length; i++)
            {
                if (msgArr[i].Payload is not IImmutableList<IPersistentRepresentation> payloads)
                {
                    fullSet.Add(new Util.Try<T[]>(new ArgumentNullException(
                            $"{msgArr[i].PersistenceId} received empty payload for sequenceNr range " +
                            $"{msgArr[i].LowestSequenceNr} - {msgArr[i].HighestSequenceNr}")));
                }
                else
                {
                    fullSet.Add(SerializerItem(timeStamp, payloads));
                }
            }

            return fullSet;
        }

        private Util.Try<T[]> SerializerItem(long timeStamp, IImmutableList<IPersistentRepresentation> payloads)
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
                    return new Util.Try<T[]>(ser.Failure.Value);
                }
            }

            return new Util.Try<T[]>(retList);
        }        

        public Util.Try<T> Serialize(IPersistentRepresentation persistentRepr, long timeStamp = 0)
        {
            return persistentRepr.Payload switch
            {
                Tagged t => Serialize(persistentRepr.WithPayload(t.Payload), t.Tags, timeStamp),
                _ => Serialize(persistentRepr, ImmutableHashSet<string>.Empty, timeStamp)
            };
        }

        protected abstract Util.Try<T> Serialize(
            IPersistentRepresentation persistentRepr,
            IImmutableSet<string> tTags, long timeStamp =0);

        protected abstract Util.Try<(IPersistentRepresentation, IImmutableSet<string>, long)> Deserialize(T t);
    }
}