using System;
using System.Collections.Immutable;
using System.Linq;
using Akka.Actor;
using Akka.Event;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Query.InternalProtocol;
using Akka.Persistence.Sql.Utility;
using Akka.Streams;
using Akka.Streams.Dsl;
using Akka.Util.Extensions;
using Akka.Util.Internal;

namespace Akka.Persistence.Sql.Query
{
    public class JournalSequenceActor : ActorBase, IWithTimers
    {
        private readonly JournalSequenceRetrievalConfig _config;
        private readonly IReadJournalDao _readJournalDao;
        private readonly TimeSpan _queryDelay;
        private readonly int _maxTries;
        private readonly ILoggingAdapter _log;
        private readonly ActorMaterializer _mat;

        public ITimerScheduler Timers { get; set; }

        public JournalSequenceActor(IReadJournalDao readJournalDao,
            JournalSequenceRetrievalConfig config)
        {
            _mat = Materializer.CreateSystemMaterializer(
                context: (ExtendedActorSystem)Context.System,
                settings: ActorMaterializerSettings.Create(Context.System),
                namePrefix: "linq2db-query");

            _readJournalDao = readJournalDao;
            _config = config;
            _queryDelay = config.QueryDelay;
            _maxTries = config.MaxTries;
            _log = Context.GetLogger();
        }

        private bool ReceiveHandler(object message)
        {
            return ReceiveHandler(message, 0, ImmutableDictionary<int, MissingElements>.Empty, 0, _queryDelay);
        }

        protected bool ReceiveHandler(
            object message,
            long currentMaxOrdering,
            IImmutableDictionary<int, MissingElements> missingByCounter,
            int moduloCounter,
            TimeSpan previousDelay)
        {
            switch (message)
            {
                case ScheduleAssumeMaxOrderingId s:
                    var delay = _queryDelay.Multiply(_maxTries);
                    Timers.StartSingleTimer(
                        key: AssumeMaxOrderingIdTimerKey.Instance,
                        msg: new AssumeMaxOrderingId(s.MaxInDatabase),
                        timeout: delay);
                    return true;

                case AssumeMaxOrderingId a:
                    if (currentMaxOrdering < a.Max)
                    {
                        Become(o => ReceiveHandler(o, _maxTries, missingByCounter, moduloCounter, previousDelay));
                    }
                    return true;

                case GetMaxOrderingId:
                    Sender.Tell(new MaxOrderingId(currentMaxOrdering));
                    return true;

                case QueryOrderingIds:
                    _readJournalDao
                        .JournalSequence(currentMaxOrdering, _config.BatchSize)
                        .RunWith(Sink.Seq<long>(), _mat)
                        .PipeTo(
                            recipient: Self,
                            sender: Self,
                            success: res => new NewOrderingIds(currentMaxOrdering, res));
                    return true;

                case NewOrderingIds nids when nids.MaxOrdering < currentMaxOrdering:
                    Self.Tell(QueryOrderingIds.Instance);
                    return true;

                case NewOrderingIds nids:
                    FindGaps(nids.Elements, currentMaxOrdering, missingByCounter, moduloCounter);
                    return true;

                case Status.Failure t:
                    var newDelay = _config.MaxBackoffQueryDelay.Min(previousDelay.Multiply(2));
                    if (newDelay == _config.MaxBackoffQueryDelay)
                    {
                        _log.Warning("Failed to query max Ordering ID Because of {0}, retrying in {1}", t, newDelay);
                    }

                    ScheduleQuery(newDelay);
                    Context.Become(o => ReceiveHandler(o, currentMaxOrdering, missingByCounter, moduloCounter, newDelay));
                    return true;

                default:
                    return false;
            }
        }

        private void FindGaps(
            IImmutableList<long> elements,
            long currentMaxOrdering,
            IImmutableDictionary<int, MissingElements> missingByCounter,
            int moduloCounter)
        {
            var givenUp = missingByCounter.ContainsKey(moduloCounter)
                ? missingByCounter[moduloCounter] : MissingElements.Empty;

            var (nextMax, _, missingElems) = elements.Aggregate(
                (currentMax: currentMaxOrdering, previousElement: currentMaxOrdering, missing: MissingElements.Empty),
                (agg, currentElement) =>
                {
                    long newMax;
                    if (new NumericRangeEntry(agg.Item1 + 1, currentElement).ToEnumerable().ForAll(p => givenUp.Contains(p)))
                    {
                        newMax = currentElement;
                    }
                    else
                    {
                        newMax = agg.currentMax;
                    }

                    MissingElements newMissing;
                    if (agg.previousElement + 1 == currentElement ||
                        newMax == currentElement)
                    {
                        newMissing = agg.missing;
                    }
                    else
                    {
                        newMissing = agg.missing.AddRange(agg.Item2 + 1, currentElement);
                    }

                    return (newMax, currentElement, newMissing);
                });

            var newMissingByCounter = missingByCounter.SetItem(moduloCounter, missingElems);
            var noGapsFound = missingElems.Isempty;
            var isFullBatch = elements.Count == _config.BatchSize;
            if (noGapsFound && isFullBatch)
            {
                Self.Tell(QueryOrderingIds.Instance);
                Context.Become(o => ReceiveHandler(o, nextMax, newMissingByCounter, moduloCounter, _queryDelay));
            }
            else
            {
                ScheduleQuery(_queryDelay);
                Context.Become(o => ReceiveHandler(o, nextMax, newMissingByCounter, (moduloCounter + 1) % _config.MaxTries, _queryDelay));
            }

        }

        private void ScheduleQuery(TimeSpan delay)
        {
            Timers.StartSingleTimer(QueryOrderingIdsTimerKey.Instance, QueryOrderingIds.Instance, delay);
        }

        protected override bool Receive(object message)
        {
            return ReceiveHandler(message);
        }

        protected override void PreStart()
        {
            var self = Self;
            self.Tell(QueryOrderingIds.Instance);
            try
            {
                _readJournalDao.MaxJournalSequenceAsync().ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        _log.Info(t.Exception, "Failed to recover fast, using event-by-event recovery instead");
                    }
                    else if (t.IsCompleted)
                    {
                        self.Tell(new ScheduleAssumeMaxOrderingId(t.Result));
                    }
                });
            }
            catch
            {
                //Leaving empty because we log above on failure.
            }
            base.PreStart();
        }
    }
}
