//-----------------------------------------------------------------------
// <copyright file="QueryThrottler.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2024 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2024 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Akka.Event;
using Akka.Pattern;

namespace Akka.Persistence.Sql.Query;

/// <summary>
/// Request token from throttler
/// </summary>
internal sealed class RequestQueryStart
{
    public DateTime DeadlineTime { get; }

    public RequestQueryStart(TimeSpan timeout)
    {
        DeadlineTime = DateTime.UtcNow.Add(timeout);
    }
}

internal sealed class PendingRequest
{
    public PendingRequest(IActorRef requester, DateTime deadlineTime)
    {
        Requester = requester;
        DeadlineTime = deadlineTime;
    }
    
    public IActorRef Requester { get; }
    public DateTime DeadlineTime { get; }
    
    public bool IsExpired => DeadlineTime < DateTime.UtcNow;
}

/// <summary>
/// Token request granted
/// </summary>
internal sealed class QueryStartGranted
{
    public static readonly QueryStartGranted Instance = new();
    private QueryStartGranted() { }
}

/// <summary>
/// Return token to throttler
/// </summary>
internal sealed class ReturnQueryStart
{
    public static readonly ReturnQueryStart Instance = new();
    private ReturnQueryStart() { }
}

#region Test classes

/// <summary>
/// For testing purposes
/// </summary>
internal sealed class GetUsedPermits
{
    public static readonly GetUsedPermits Instance = new();
    private GetUsedPermits() { }
}

/// <summary>
/// For testing purposes
/// </summary>
internal sealed class GetPendingRequests
{
    public static readonly GetPendingRequests Instance = new();
    private GetPendingRequests() { }
}

internal sealed class GetWatchCount
{
    public static readonly GetWatchCount Instance = new();
    private GetWatchCount() { }
}

#endregion

/// <summary>
/// Token bucket throttler that grants queries permissions to run each iteration
/// </summary>
/// <remarks>
/// Works almost identically to the RecoveryPermitter built into Akka.Persistence.
/// 
/// NOTE: Since this permitter works with Ask and we can't death watch an Ask temp
///       actor, there is a possibility that a permit requester failed to return
///       the request permit.
/// 
///       ALWAYS USE A TRY...FINALLY BLOCK WHEN USING ASK AND RETURN THE PERMIT IN
///       THE FINALLY BLOCK
/// </remarks>
internal sealed class QueryThrottler : ReceiveActor
{
    private readonly LinkedList<PendingRequest> _pending = new();
    private readonly ILoggingAdapter _log = Context.GetLogger();
    private long _watchCount;
    private int _usedPermits;
    private int _maxPendingStats;

    public QueryThrottler(int maxPermits)
    {
        MaxPermits = maxPermits;
        
        Receive<RequestQueryStart>(request =>
        {
            if(Sender is ActorRefWithCell)
            {
                _watchCount++;
                Context.Watch(Sender);
            }
            
            if (_usedPermits >= MaxPermits)
            {
                if (_pending.Count == 0)
                    _log.Debug("Exceeded max-concurrent-queries[{0}]. First pending {1}", MaxPermits, Sender);
                _pending.AddLast(new PendingRequest(Sender, request.DeadlineTime));
                _maxPendingStats = Math.Max(_maxPendingStats, _pending.Count);
            }
            else
            {
                QueryStartGranted(Sender);   
            }
        });
        
        Receive<ReturnQueryStart>(_ =>
        {
            if(Sender is ActorRefWithCell)
                Context.Unwatch(Sender);
            
            ReturnQueryPermit();
        });
        
        Receive<Terminated>(terminated =>
        {
            var actor = terminated.ActorRef;
            if(actor is ActorRefWithCell)
                Context.Unwatch(actor);
            
            var pending = _pending.FirstOrDefault(p => p.Requester.Equals(actor));
            if (pending is not null)
            {
                _pending.Remove(pending);
            }
            else
            {
                ReturnQueryPermit();
            }
        });

        #region Test handlers
        Receive<GetUsedPermits>(_ => Sender.Tell(_usedPermits));
        Receive<GetPendingRequests>(_ => Sender.Tell(_pending.ToArray()));
        Receive<GetWatchCount>(_ => Sender.Tell(_watchCount));
        #endregion
    }

    public int MaxPermits { get; }
    
    private void QueryStartGranted(IActorRef actorRef)
    {
        _usedPermits++;
        actorRef.Tell(Query.QueryStartGranted.Instance);
    }
    
    private void ReturnQueryPermit()
    {
        _usedPermits--;

        if (_usedPermits < 0)
        {
            _log.Warning("Permits must not be negative");
            _usedPermits = 0;
            return;
        }

        while (_pending.First is not null)
        {
            var pending = _pending.First.Value;
            _pending.RemoveFirst();
            if (pending is not null && pending.IsExpired)
                pending = null;

            if (pending is null)
                continue;
            
            QueryStartGranted(pending.Requester);
            break;
        }

        if (_pending.Count != 0 || _maxPendingStats <= 0)
            return;
        
        if(_log.IsDebugEnabled)
            _log.Debug("Drained pending recovery permit requests, max in progress was [{0}], still [{1}] in progress", _usedPermits + _maxPendingStats, _usedPermits);
        _maxPendingStats = 0;
    }
}
