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