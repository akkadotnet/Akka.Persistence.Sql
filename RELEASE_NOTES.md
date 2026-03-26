#### 1.5.62 March 26th 2026 ####

* [Bump Akka.NET to 1.5.62](https://github.com/akkadotnet/akka.net/releases/tag/1.5.62)
* [Bump Akka.Hosting to 1.5.62](https://github.com/akkadotnet/Akka.Hosting/releases/tag/1.5.62)
* [Fix SQL Server 2016 tag table compatibility bug](https://github.com/akkadotnet/Akka.Persistence.Sql/pull/581) - Fix `StringAggregate()` Linq2DB bug on SQLServer 2016 and below.

#### 1.5.60.1 February 14th 2026 ####

* [Bump Akka.NET to 1.5.60](https://github.com/akkadotnet/akka.net/releases/tag/1.5.60)
* [Bump Akka.Hosting to 1.5.60](https://github.com/akkadotnet/Akka.Hosting/releases/tag/1.5.60)
* [Fix linq2db assembly binding conflict](https://github.com/akkadotnet/Akka.Persistence.Sql/pull/572) - bumped `linq2db` from 5.4.1 to 5.4.1.9 to resolve assembly version mismatch that prevented projects from building when NuGet resolved the newer package version.

#### 1.5.59 January 26th 2026 ####

* [Bump Akka.NET to 1.5.59](https://github.com/akkadotnet/akka.net/releases/tag/1.5.59)
* [Bump Akka.Hosting to 1.5.59](https://github.com/akkadotnet/Akka.Hosting/releases/tag/1.5.59)

#### 1.5.55.1 October 29th 2025 ####

**Improved API**

This release introduces the simplified Akka.Hosting 1.5.55.1 API for connectivity health checks, eliminating redundant parameter passing:

**New Simplified API (Recommended):**
```csharp
journalBuilder: journal =>
{
    journal.WithConnectivityCheck(); // Options automatically accessed from builder
}
```

**Previous API (Still Supported):**
```csharp
journalBuilder: journal =>
{
    journal.WithConnectivityCheck(journalOptions); // Explicit parameter passing
}
```

The new API automatically accesses options from `builder.Options`, making the code cleaner and less error-prone. The previous API is marked as `[Obsolete]` but remains functional for backward compatibility.

* Update `WithConnectivityCheck()` extension methods to use simplified Akka.Hosting 1.5.55.1 API pattern
* Add comprehensive integration tests for simplified API
* Update documentation with examples for both API styles

#### 1.5.55 October 26th 2025 ####

* [Bump AkkaVersion and AkkaHostingVersion to 1.5.55](https://github.com/akkadotnet/akka.net/releases/tag/1.5.55)
* [Add SQL connectivity health checks for all database providers](https://github.com/akkadotnet/Akka.Persistence.Sql/pull/558)
* [Add customizable tags parameter to health check methods](https://github.com/akkadotnet/Akka.Persistence.Sql/pull/559)

Adds new `WithConnectivityCheck()` methods for proactive database connectivity verification with customizable tags. Supports all database providers (SQL Server, PostgreSQL, MySQL, SQLite).

#### 1.5.53 October 14th 2025 ####

**Critical Bug Fix**

This release fixes a critical regression introduced in v1.5.51.1 where `IWriteEventAdapter` and `IReadEventAdapter` instances were not being applied when loading events using `BySequenceNr` queries. This caused event tagging and other event adapter functionality to fail in production scenarios.

* **[Fix EventAdapter regression from v1.5.51.1](https://github.com/akkadotnet/Akka.Persistence.Sql/issues/552)** - Event adapters configured via `WithSqlPersistence()` now work correctly when the method is called multiple times (e.g., once for default persistence with adapters, then again for sharding configuration). Fixed by upgrading to Akka.NET v1.5.53 which includes the fix from [Akka.Hosting#669](https://github.com/akkadotnet/Akka.Hosting/pull/669).
* [Add runtime reproduction test for event adapter regression](https://github.com/akkadotnet/Akka.Persistence.Sql/pull/554)
* [Bump AkkaVersion and AkkaHostingVersion to 1.5.53](https://github.com/akkadotnet/akka.net/releases/tag/1.5.53)

#### 1.5.51.1 October 2nd 2025 ####

* [Fix health check registration bug in Akka.Hosting extensions](https://github.com/akkadotnet/Akka.Persistence.Sql/pull/549)
* [Add Akka.Hosting health check documentation to README](https://github.com/akkadotnet/Akka.Persistence.Sql/pull/548)

#### 1.5.51 October 1st 2025 ####

* [Bump AkkaVersion and AkkaHostingVersion to 1.5.51](https://github.com/akkadotnet/akka.net/releases/tag/1.5.51)

#### 1.5.44 June 23rd 2025 ####

* [Bump AkkaVersion and AkkaHostingVersion to 1.5.44](https://github.com/akkadotnet/akka.net/releases/tag/1.5.44)
* [Improve journal delete performance](https://github.com/akkadotnet/Akka.Persistence.Sql/pull/538)
* [Fix snapshot store `CancellationTokenSource` memory leak](https://github.com/akkadotnet/Akka.Persistence.Sql/pull/542)

#### 1.5.42 May 22nd 2025 ####

* [Bump AkkaVersion to 1.5.42](https://github.com/akkadotnet/akka.net/releases/tag/1.5.42)
* [Fix SqlServerRetryPolicy.GetNextDelay returns negative TimeSpan](https://github.com/akkadotnet/Akka.Persistence.Sql/pull/539)

#### 1.5.40.1 April 7th 2025 ####

* [Fix duplicate actor name for read journal sequencer actors](https://github.com/akkadotnet/Akka.Persistence.Sql/pull/531)