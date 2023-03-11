#### 1.4.28 Nov 18 2021 ####

**Perf Enhancements and fixes to failure reporting**

There was an issue found where persistence failures were being reported as rejections when they should not have. This has been fixed alongside some logic cleanup that should lead to more consistent performance.

#### 1.4.21 July 6 2021 ####

**First official Release for Akka.Persistence.Linq2Db**

Akka.Persistence.Linq2Db is an Akka.Net Persistence plug-in that is designed for both high performance as well as easy cross-database compatibility.

There is a compatibility mode also available for those who wish to migrate from the existing Sql.Common journals.

This release contains fixes for Transactions around batched writes, a fix for Sequence Number reading on Aggressive PersistAsync Usage, improved serialization/deserialization pipelines for improved write speed, and easier snapshot compatibility with Akka.Persistence.Sql.Common.

We are still looking for community help with adding tests/configurations for MySql and Oracle, as well as trying out the new plugin and [providing feedback](https://github.com/akkadotnet/Akka.Persistence.Linq2Db/issues).

[Please refer to the project page](https://github.com/akkadotnet/Akka.Persistence.Linq2Db/) for information on configuration.

#### 0.90.1 Feb 3 2021 ####

**Preview Release for Akka.Persistence.Linq2Db**

Akka.Persistence.Linq2Db is an Akka.Net Persistence plug-in that is designed for both high performance as well as easy cross-database compatibility.

This is currently marked as a preview release, with tests passing for MS Sql Server, PostgreSQL, and SQLite. We are looking for community help with adding tests, as well as trying out the new plugin and [providing feedback](https://github.com/akkadotnet/Akka.Persistence.Linq2Db/issues).

There is a compatibility mode also available for those who wish to migrate from the existing Sql.Common journals.

[Please refer to the project page](https://github.com/akkadotnet/Akka.Persistence.Linq2Db/) for information on configuration.
