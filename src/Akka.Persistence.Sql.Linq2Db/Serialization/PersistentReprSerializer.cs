using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Akka.Persistence.Journal;

namespace Akka.Persistence.Sql.Linq2Db.Serialization
{
    public abstract class PersistentReprSerializer<T>
    {
        public List<Util.Try<List<T>>> Serialize(IEnumerable<AtomicWrite> messages, long timeStamp = 0)
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
                    return new Util.Try<List<T>>(
                        new ArgumentNullException(
                            $"{aw.PersistenceId} received empty payload for sequenceNr range " +
                            $"{aw.LowestSequenceNr} - {aw.HighestSequenceNr}"));
                }
                
                //Preallocate our list; In the common case
                //This saves a tiny bit of garbage
                var retList = new List<T>(payloads.Count);
                if (payloads.Count == 1)
                {
                    // If there's only one payload
                    // Don't allocate the enumerable.
                    var ser = Serialize(payloads[0], timeStamp);
                    var opt = ser.Success;
                    if (opt.HasValue)
                    {
                        retList.Add(opt.Value);    
                        return new Util.Try<List<T>>(retList);
                    }
                    
                    return new Util.Try<List<T>>(ser.Failure.Value);
                }

                foreach (var p in payloads)
                {
                    var ser = Serialize(p, timeStamp);
                    var opt = ser.Success;
                    if (opt.HasValue)
                    {
                        retList.Add(opt.Value);
                    }
                    else
                    {
                        return new Util.Try<List<T>>(ser.Failure.Value);
                    }
                }

                return new Util.Try<List<T>>(retList);
            }).ToList();
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