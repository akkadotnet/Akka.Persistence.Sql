## Fix TagTable mode on SQL Server < 2017

### Problem

When using `tag-write-mode = TagTable` with SQL Server 2016 (or any version prior to 2017), the read journal throws:

```
LinqToDB.Linq.LinqException: '...StringAggregate(";", r => r.TagValue).ToValue()' cannot be converted to SQL.
```

This happens because LinqToDB's `StringAggregate` maps to SQL Server's `STRING_AGG` function, which was introduced in SQL Server 2017. Users on SQL Server 2016 or earlier cannot use TagTable mode at all.

### Solution

Added runtime SQL Server version detection via LinqToDB's `SqlServerDataProvider.Version` property. When the detected version is below 2017, tag aggregation falls back to a client-side implementation that:

1. Fetches journal rows first
2. Queries tag rows separately using `WHERE OrderingId IN (...)`
3. Joins tags to journal rows in memory

This is transparent to the user -- no configuration changes needed. The generic `provider-name = "SqlServer"` works correctly because LinqToDB auto-detects the version by querying `sys.databases` for the database compatibility level.

### Changes

**Core fix (2 files):**
- `AkkaDataConnection.cs` -- Added `SupportsStringAggregate` property that checks `SqlServerDataProvider.Version >= v2017` at runtime. Returns `true` for all non-SQL Server providers (PostgreSQL, MySQL, SQLite all support equivalent functions).
- `BaseByteReadArrayJournalDao.cs` -- Split `AddTagDataFromTagTableAsync` into two paths: `AddTagDataFromTagTableWithStringAggregateAsync` (existing SQL path) and `AddTagDataFromTagTableClientSideAsync` (new fallback). The dispatcher checks `connection.SupportsStringAggregate` to choose.

**Test infrastructure (2 files):**
- `SqlServer2016Container.cs` -- Runs SQL Server 2022 but sets `COMPATIBILITY_LEVEL = 130` to simulate SQL Server 2016. Uses generic `"SqlServer"` provider name for auto-detection.
- `SqlServer2016PersistenceSpec.cs` -- xUnit collection definition for the 2016 compat fixture.

**Tests (9 files):**
- `SupportsStringAggregateSpec.cs` -- Unit tests verifying version detection returns correct values for v2005-v2016 (false), v2017+ (true), and non-SQL Server providers (true). No database required.
- `SqlServer2016EventsByTagSpec.cs` -- Includes a proof test that directly calls `StringAggregate` and asserts it throws on compat level 130, plus the standard TCK EventsByTag tests running through the fallback path.
- `SqlServer2016CurrentEventsByTagSpec.cs`, `SqlServer2016AllEventsSpec.cs`, `SqlServer2016CurrentAllEventsSpec.cs`, `SqlServer2016EventsByPersistenceIdSpec.cs`, `SqlServer2016PersistenceIdsSpec.cs`, `SqlServer2016QueryThrottleSpecs.cs` -- Full TagTable integration test suite running against the 2016 compat container.
