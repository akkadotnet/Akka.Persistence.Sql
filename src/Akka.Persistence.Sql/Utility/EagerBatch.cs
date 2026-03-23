#nullable enable

using System;
using Akka.Streams.Stage;
using Akka.Streams.Supervision;
using Akka.Util;

namespace Akka.Streams.Dsl
{
    // ========================================================================
    // GraphStage implementation
    // ========================================================================

    /// <summary>
    /// A more aggressive variant of the built-in <c>Batch</c> that maximizes batching
    /// by NOT immediately flushing partial aggregates when the outlet is available
    /// during <c>OnPush</c>.  Instead it continues pulling from upstream to fill the
    /// batch, only emitting when:
    /// <list type="bullet">
    ///   <item>The batch reaches capacity (cost budget exhausted → pending element set)</item>
    ///   <item>Downstream explicitly pulls (<c>OnPull</c>)</item>
    ///   <item>Upstream completes (<c>OnUpstreamFinish</c>)</item>
    /// </list>
    ///
    /// <para>
    /// This is particularly useful when followed by stages like <c>SelectAsync</c>
    /// that pull rapidly — the standard <c>Batch</c> would flush single-element
    /// "batches" in that scenario.
    /// </para>
    /// </summary>
    /// <typeparam name="TIn">The input element type.</typeparam>
    /// <typeparam name="TOut">The output (aggregated) element type.</typeparam>
    public sealed class EagerBatchStage<TIn, TOut> : GraphStage<FlowShape<TIn, TOut>>
    {
        #region Logic

        private sealed class Logic : InAndOutGraphStageLogic
        {
            private readonly FlowShape<TIn, TOut> _shape;
            private readonly EagerBatchStage<TIn, TOut> _stage;
            private readonly Decider _decider;
            private Option<TOut> _aggregate;
            private long _left;
            private Option<TIn> _pending;

            public Logic(Attributes inheritedAttributes, EagerBatchStage<TIn, TOut> stage)
                : base(stage.Shape)
            {
                _shape = stage.Shape;
                _stage = stage;

                var attr = inheritedAttributes
                    .GetAttribute<ActorAttributes.SupervisionStrategy>(null);
                _decider = attr != null ? attr.Decider : Deciders.StoppingDecider;
                _left = stage._max;

                SetHandlers(_shape.Inlet, _shape.Outlet, this);
            }

            // -----------------------------------------------------------------
            // OnPush — the key behavioral difference lives here.
            //
            // Standard Batch does:
            //   if (IsAvailable(outlet)) Flush();   // ← flushes partial batches
            //
            // EagerBatch does:
            //   if (_pending.HasValue && IsAvailable(outlet)) Flush();
            //                                 ↑ only when the batch is FULL
            // -----------------------------------------------------------------
            public override void OnPush()
            {
                var element = Grab(_shape.Inlet);
                var cost = _stage._costFunc(element);

                if (!_aggregate.HasValue)
                {
                    // First element in a new batch — seed the aggregate.
                    try
                    {
                        _aggregate = _stage._seed(element);
                        _left -= cost;
                    }
                    catch (Exception ex)
                    {
                        HandleDecision(ex, restartAction: RestartState);
                    }
                }
                else if (_left < cost)
                {
                    // Batch is full — park this element as pending.
                    _pending = element;
                }
                else
                {
                    // Room in the batch — fold the element in.
                    try
                    {
                        _aggregate = _stage._aggregate(_aggregate.Value, element);
                        _left -= cost;
                    }
                    catch (Exception ex)
                    {
                        HandleDecision(ex, restartAction: RestartState);
                    }
                }

                // ✨ Only flush when the batch is FULL (pending is set).
                // This is the entire behavioral difference from standard Batch.
                if (_pending.HasValue && IsAvailable(_shape.Outlet))
                    Flush();

                // Keep pulling as long as we have room.
                if (!_pending.HasValue)
                    Pull(_shape.Inlet);
            }

            // -----------------------------------------------------------------
            // OnPull — always flush whatever we have accumulated so far.
            // This is identical to standard Batch behavior.
            // -----------------------------------------------------------------
            public override void OnPull()
            {
                if (!_aggregate.HasValue)
                {
                    if (IsClosed(_shape.Inlet))
                        CompleteStage();
                    else if (!HasBeenPulled(_shape.Inlet))
                        Pull(_shape.Inlet);
                }
                else if (IsClosed(_shape.Inlet))
                {
                    Push(_shape.Outlet, _aggregate.Value);
                    if (!_pending.HasValue)
                    {
                        CompleteStage();
                    }
                    else
                    {
                        try
                        {
                            _aggregate = _stage._seed(_pending.Value);
                        }
                        catch (Exception ex)
                        {
                            HandleDecision(ex, restartAction: () =>
                            {
                                RestartState();
                                if (!HasBeenPulled(_shape.Inlet))
                                    Pull(_shape.Inlet);
                            });
                        }

                        _pending = Option<TIn>.None;
                    }
                }
                else
                {
                    Flush();
                    if (!HasBeenPulled(_shape.Inlet))
                        Pull(_shape.Inlet);
                }
            }

            public override void OnUpstreamFinish()
            {
                if (!_aggregate.HasValue)
                    CompleteStage();
                // If we DO have an aggregate, we keep the stage alive so OnPull
                // can emit the remaining data before completing.
            }

            public override void PreStart() => Pull(_shape.Inlet);

            // -----------------------------------------------------------------
            // Helpers
            // -----------------------------------------------------------------

            private void Flush()
            {
                if (_aggregate.HasValue)
                {
                    Push(_shape.Outlet, _aggregate.Value);
                    _left = _stage._max;
                }

                if (_pending.HasValue)
                {
                    try
                    {
                        _aggregate = _stage._seed(_pending.Value);
                        _left -= _stage._costFunc(_pending.Value);
                        _pending = Option<TIn>.None;
                    }
                    catch (Exception ex)
                    {
                        HandleDecision(ex, restartAction: RestartState,
                            resumeAction: () => _pending = Option<TIn>.None);
                    }
                }
                else
                {
                    _aggregate = Option<TOut>.None;
                }
            }

            private void RestartState()
            {
                _aggregate = Option<TOut>.None;
                _left = _stage._max;
                _pending = Option<TIn>.None;
            }

            /// <summary>
            /// Applies the supervision <see cref="Decider"/> to <paramref name="ex"/>
            /// and runs the appropriate action.
            /// </summary>
            private void HandleDecision(
                Exception ex,
                Action? restartAction = null,
                Action? resumeAction = null)
            {
                switch (_decider(ex))
                {
                    case Directive.Stop:
                        FailStage(ex);
                        break;
                    case Directive.Restart:
                        restartAction?.Invoke();
                        break;
                    case Directive.Resume:
                        resumeAction?.Invoke();
                        break;
                }
            }
        }

        #endregion

        // Stage fields
        internal readonly long _max;
        internal readonly Func<TIn, long> _costFunc;
        internal readonly Func<TIn, TOut> _seed;
        internal readonly Func<TOut, TIn, TOut> _aggregate;

        /// <summary>
        /// Creates a new <see cref="EagerBatchStage{TIn,TOut}"/> that aggressively pulls
        /// from upstream to maximize batch sizes before emitting downstream.
        /// </summary>
        /// <param name="max">Maximum weight/cost budget for a single batch.</param>
        /// <param name="costFunc">Function to compute the cost of a single input element.</param>
        /// <param name="seed">Creates the initial aggregate from the first element in a batch.</param>
        /// <param name="aggregate">Folds subsequent elements into the running aggregate.</param>
        public EagerBatchStage(
            long max,
            Func<TIn, long> costFunc,
            Func<TIn, TOut> seed,
            Func<TOut, TIn, TOut> aggregate)
        {
            _max = max;
            _costFunc = costFunc;
            _seed = seed;
            _aggregate = aggregate;

            Shape = new FlowShape<TIn, TOut>(
                new Inlet<TIn>("EagerBatch.in"),
                new Outlet<TOut>("EagerBatch.out"));
        }

        /// <inheritdoc />
        public override FlowShape<TIn, TOut> Shape { get; }

        /// <inheritdoc />
        protected override GraphStageLogic CreateLogic(Attributes inheritedAttributes)
            => new Logic(inheritedAttributes, this);
    }

    // ========================================================================
    // Extension methods — drop-in replacements for .Batch() / .BatchWeighted()
    // ========================================================================

    /// <summary>
    /// Extension methods that wire <see cref="EagerBatchStage{TIn,TOut}"/> into
    /// Akka.Streams <see cref="Source{TOut,TMat}"/>, <see cref="Flow{TIn,TOut,TMat}"/>,
    /// and any <see cref="IFlow{TOut,TMat}"/>.
    /// </summary>
    public static class EagerBatchExtensions
    {
        // ── IFlow (covers Source, Flow, SubFlow) ────────────────────────

        /// <summary>
        /// Aggressively batches upstream elements into a single aggregate, emitting
        /// only when the batch is full or downstream explicitly pulls.
        /// Each element has a cost of <c>1</c>.
        /// </summary>
        /// <typeparam name="TOut">The upstream element type.</typeparam>
        /// <typeparam name="TOut2">The aggregated output type.</typeparam>
        /// <typeparam name="TMat">The materialized value type.</typeparam>
        /// <param name="flow">The flow to attach to.</param>
        /// <param name="max">Maximum number of elements per batch.</param>
        /// <param name="seed">Creates the initial aggregate from the first element.</param>
        /// <param name="aggregate">Folds subsequent elements into the running aggregate.</param>
        public static IFlow<TOut2, TMat> EagerBatch<TOut, TOut2, TMat>(
            this IFlow<TOut, TMat> flow,
            long max,
            Func<TOut, TOut2> seed,
            Func<TOut2, TOut, TOut2> aggregate)
        {
            return flow.Via(new EagerBatchStage<TOut, TOut2>(max, _ => 1L, seed, aggregate));
        }

        /// <summary>
        /// Aggressively batches upstream elements into a single aggregate using a
        /// weighted cost function, emitting only when the cost budget is exhausted
        /// or downstream explicitly pulls.
        /// </summary>
        /// <typeparam name="TOut">The upstream element type.</typeparam>
        /// <typeparam name="TOut2">The aggregated output type.</typeparam>
        /// <typeparam name="TMat">The materialized value type.</typeparam>
        /// <param name="flow">The flow to attach to.</param>
        /// <param name="max">Maximum total weight per batch.</param>
        /// <param name="costFunction">Computes the weight of a single element.</param>
        /// <param name="seed">Creates the initial aggregate from the first element.</param>
        /// <param name="aggregate">Folds subsequent elements into the running aggregate.</param>
        public static IFlow<TOut2, TMat> EagerBatchWeighted<TOut, TOut2, TMat>(
            this IFlow<TOut, TMat> flow,
            long max,
            Func<TOut, long> costFunction,
            Func<TOut, TOut2> seed,
            Func<TOut2, TOut, TOut2> aggregate)
        {
            return flow.Via(new EagerBatchStage<TOut, TOut2>(max, costFunction, seed, aggregate));
        }

        // ── Flow<TIn, TOut, TMat> (strongly typed return) ──────────────

        /// <summary>
        /// Aggressively batches upstream elements. Strongly-typed overload for
        /// <see cref="Flow{TIn,TOut,TMat}"/>.
        /// </summary>
        public static Flow<TIn, TOut2, TMat> EagerBatch<TIn, TOut, TOut2, TMat>(
            this Flow<TIn, TOut, TMat> flow,
            long max,
            Func<TOut, TOut2> seed,
            Func<TOut2, TOut, TOut2> aggregate)
        {
            return (Flow<TIn, TOut2, TMat>)((IFlow<TOut, TMat>)flow)
                .EagerBatch(max, seed, aggregate);
        }

        /// <summary>
        /// Aggressively batches upstream elements by weight. Strongly-typed overload
        /// for <see cref="Flow{TIn,TOut,TMat}"/>.
        /// </summary>
        public static Flow<TIn, TOut2, TMat> EagerBatchWeighted<TIn, TOut, TOut2, TMat>(
            this Flow<TIn, TOut, TMat> flow,
            long max,
            Func<TOut, long> costFunction,
            Func<TOut, TOut2> seed,
            Func<TOut2, TOut, TOut2> aggregate)
        {
            return (Flow<TIn, TOut2, TMat>)((IFlow<TOut, TMat>)flow)
                .EagerBatchWeighted(max, costFunction, seed, aggregate);
        }

        // ── Source<TOut, TMat> (strongly typed return) ──────────────────

        /// <summary>
        /// Aggressively batches upstream elements. Strongly-typed overload for
        /// <see cref="Source{TOut,TMat}"/>.
        /// </summary>
        public static Source<TOut2, TMat> EagerBatch<TOut, TOut2, TMat>(
            this Source<TOut, TMat> source,
            long max,
            Func<TOut, TOut2> seed,
            Func<TOut2, TOut, TOut2> aggregate)
        {
            return (Source<TOut2, TMat>)((IFlow<TOut, TMat>)source)
                .EagerBatch(max, seed, aggregate);
        }

        /// <summary>
        /// Aggressively batches upstream elements by weight. Strongly-typed overload
        /// for <see cref="Source{TOut,TMat}"/>.
        /// </summary>
        public static Source<TOut2, TMat> EagerBatchWeighted<TOut, TOut2, TMat>(
            this Source<TOut, TMat> source,
            long max,
            Func<TOut, long> costFunction,
            Func<TOut, TOut2> seed,
            Func<TOut2, TOut, TOut2> aggregate)
        {
            return (Source<TOut2, TMat>)((IFlow<TOut, TMat>)source)
                .EagerBatchWeighted(max, costFunction, seed, aggregate);
        }
    }
}