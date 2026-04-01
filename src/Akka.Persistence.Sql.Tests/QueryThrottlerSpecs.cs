// -----------------------------------------------------------------------
//  <copyright file="QueryThrottlerSpecs.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Persistence.Sql.Query;
using FluentAssertions;
using FluentAssertions.Extensions;
using Xunit;

namespace Akka.Persistence.Sql.Tests.Query;

public class QueryThrottlerSpecs: Akka.TestKit.Xunit.TestKit
{
    public QueryThrottlerSpecs(ITestOutputHelper output) : base("{}", output)
    {
    }
    
    // Issue https://github.com/akkadotnet/Akka.Persistence.Sql/issues/516
    // Caused by Ask temp actor being watched and rooted in memory because it is not
    // compatible with death watch
    [Fact(DisplayName = "QueryThrottler should not watch permit request made using Ask")]
    public async Task MemoryLeakTest()
    {
        var throttler = Sys.ActorOf(Props.Create(() => new QueryThrottler(10)));
        
        foreach (var _ in Enumerable.Range(1, 10))
        {
            // IMPORTANT: the bug was caused by Ask, don't replace this with Tell and ExpectMsg
            await throttler.Ask<QueryStartGranted>(new RequestQueryStart(3.Seconds()));
            throttler.Tell(ReturnQueryStart.Instance);
        }
        
        throttler.Tell(GetWatchCount.Instance);
        ExpectMsg<long>().Should().Be(0);
    }

    [Fact(DisplayName = "QueryThrottler should discard expired pending requests")]
    public async Task DiscardExpiredTest()
    {
        var throttler = Sys.ActorOf(Props.Create(() => new QueryThrottler(1)));
        
        // Use all permits
        await throttler.Ask<QueryStartGranted>(new RequestQueryStart(3.Seconds()));
        
        // Request for more permits
        throttler.Tell(new RequestQueryStart(100.Milliseconds()), TestActor);
        throttler.Tell(new RequestQueryStart(100.Milliseconds()), TestActor);
        
        // Check that pending still contain pending requests
        throttler.Tell(GetPendingRequests.Instance, TestActor);
        var requests = await ExpectMsgAsync<PendingRequest[]>();
        requests.Length.Should().Be(2);
        
        // No permit should be granted, all extra request timed out
        await ExpectNoMsgAsync(200.Milliseconds());
        
        // Return the first permit
        throttler.Tell(ReturnQueryStart.Instance, TestActor);
        
        // No permit should not be granted
        await ExpectNoMsgAsync(200.Milliseconds());
        
        // Check that all timed out pending requests are discarded
        throttler.Tell(GetPendingRequests.Instance, TestActor);
        requests = await ExpectMsgAsync<PendingRequest[]>();
        requests.Length.Should().Be(0);
    }

    [Fact(DisplayName = "QueryThrottler should honor pending requests")]
    public async Task PendingRequestsTest()
    {
        var throttler = Sys.ActorOf(Props.Create(() => new QueryThrottler(1)));
        
        // Use all permits
        await throttler.Ask<QueryStartGranted>(new RequestQueryStart(3.Seconds()));
        
        // Request for more permits
        throttler.Tell(new RequestQueryStart(3.Seconds()), TestActor);
        throttler.Tell(new RequestQueryStart(3.Seconds()), TestActor);
        
        // Check that pending contain the last requests
        throttler.Tell(GetPendingRequests.Instance, TestActor);
        var requests = await ExpectMsgAsync<PendingRequest[]>();
        requests.Length.Should().Be(2);
        
        // No permit should be granted
        await ExpectNoMsgAsync(200.Milliseconds());
        
        // Return the first permit
        throttler.Tell(ReturnQueryStart.Instance, TestActor);

        // First pending response
        ExpectMsg<QueryStartGranted>();
        
        // Check that pending still contain pending requests
        throttler.Tell(GetPendingRequests.Instance, TestActor);
        requests = await ExpectMsgAsync<PendingRequest[]>();
        requests.Length.Should().Be(1);
        
        // No permit should be granted
        await ExpectNoMsgAsync(200.Milliseconds());
        
        // Return the second permit
        throttler.Tell(ReturnQueryStart.Instance, TestActor);

        // Second pending response
        ExpectMsg<QueryStartGranted>();
        
        // Check that pending is empty
        throttler.Tell(GetPendingRequests.Instance, TestActor);
        requests = await ExpectMsgAsync<PendingRequest[]>();
        requests.Length.Should().Be(0);
        
        // Return the third permit
        throttler.Tell(ReturnQueryStart.Instance, TestActor);
        
        // No permit should be granted (pending empty)
        await ExpectNoMsgAsync(200.Milliseconds());
    }
}
