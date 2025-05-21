// -----------------------------------------------------------------------
//  <copyright file="CustomSqlServerRetryPolicy.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using LinqToDB.DataProvider.SqlServer;

namespace Akka.Persistence.Sql.Db;

public class AkkaSqlServerRetryPolicy: SqlServerRetryPolicy
{
    public AkkaSqlServerRetryPolicy() { }
    public AkkaSqlServerRetryPolicy(int maxRetryCount) : base(maxRetryCount) { }
    public AkkaSqlServerRetryPolicy(
        int maxRetryCount,
        TimeSpan maxRetryDelay,
        double randomFactor,
        double exponentialBase,
        TimeSpan coefficient,
        ICollection<int>? errorNumbersToAdd) 
        : base(maxRetryCount, maxRetryDelay, randomFactor, exponentialBase, coefficient, errorNumbersToAdd) { }

    protected override TimeSpan? GetNextDelay(Exception lastException)
    {
        var nextDelay = base.GetNextDelay(lastException);
        return nextDelay is { TotalMilliseconds: < 0 } ? TimeSpan.Zero : nextDelay;
    } 
}
