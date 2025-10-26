# Akka.Persistence.Sql SQL Connectivity Health Checks - Comprehensive Search Results

## Repository Status
- **Repository**: `/home/aaronontheweb/repositories/olympus/Akka.Persistence.Sql`
- **Branch**: `feature/sql-connectivity-health-checks` (UP TO DATE with remote)
- **Current Version**: 1.5.53
- **Git Status**: Clean (no uncommitted changes)

---

## 1. Connectivity Health Check Implementation Files

### Core Implementation Files (Hosting)
All located in: `/home/aaronontheweb/repositories/olympus/Akka.Persistence.Sql/src/Akka.Persistence.Sql.Hosting/`

#### 1.1 SqlJournalConnectivityCheck.cs
- **File Path**: `/home/aaronontheweb/repositories/olympus/Akka.Persistence.Sql/src/Akka.Persistence.Sql.Hosting/SqlJournalConnectivityCheck.cs`
- **Class**: `SqlJournalConnectivityCheck : IAkkaHealthCheck`
- **Purpose**: Health check that verifies connectivity to the SQL database used by the journal
- **Key Features**:
  - Implements `IAkkaHealthCheck` interface (from Akka.Hosting)
  - Accepts `connectionString`, `providerName`, and `journalId` in constructor
  - Uses LinqToDB `DataConnection` for connectivity tests
  - Executes simple `SELECT 1` query to test connectivity
  - Returns `HealthCheckResult.Healthy()` on success
  - Handles `OperationCanceledException` with unhealthy status (timeout)
  - Handles general exceptions with unhealthy status

#### 1.2 SqlSnapshotStoreConnectivityCheck.cs
- **File Path**: `/home/aaronontheweb/repositories/olympus/Akka.Persistence.Sql/src/Akka.Persistence.Sql.Hosting/SqlSnapshotStoreConnectivityCheck.cs`
- **Class**: `SqlSnapshotStoreConnectivityCheck : IAkkaHealthCheck`
- **Purpose**: Health check that verifies connectivity to the SQL database used by the snapshot store
- **Key Features**:
  - Nearly identical to `SqlJournalConnectivityCheck`
  - Accepts `connectionString`, `providerName`, and `snapshotStoreId` in constructor
  - Uses same connectivity test approach (SELECT 1)
  - Returns appropriate health status with detailed messages

### Hosting Extensions
#### 1.3 HostingExtensions.cs
- **File Path**: `/home/aaronontheweb/repositories/olympus/Akka.Persistence.Sql/src/Akka.Persistence.Sql.Hosting/HostingExtensions.cs`
- **Classes**:
  - `HostingExtensions` (main class with multiple `WithSqlPersistence` overloads)
  - `SqlConnectivityCheckExtensions` (NEW - Contains WithConnectivityCheck methods)

- **SqlConnectivityCheckExtensions Methods**:

  **WithConnectivityCheck for Journal**:
  ```csharp
  public static AkkaPersistenceJournalBuilder WithConnectivityCheck(
      this AkkaPersistenceJournalBuilder builder,
      SqlJournalOptions journalOptions,
      HealthStatus unHealthyStatus = HealthStatus.Unhealthy,
      string? name = null)
  ```
  - Creates `AkkaHealthCheckRegistration` with `SqlJournalConnectivityCheck`
  - Registers with tags: `["akka", "persistence", "sql", "journal", "connectivity"]`
  - Uses reflection to access internal `Builder` property of `AkkaPersistenceJournalBuilder`
  - Default name: `"Akka.Persistence.Sql.Journal.{id}.Connectivity"`

  **WithConnectivityCheck for Snapshot**:
  ```csharp
  public static AkkaPersistenceSnapshotBuilder WithConnectivityCheck(
      this AkkaPersistenceSnapshotBuilder builder,
      SqlSnapshotOptions snapshotOptions,
      HealthStatus unHealthyStatus = HealthStatus.Unhealthy,
      string? name = null)
  ```
  - Creates `AkkaHealthCheckRegistration` with `SqlSnapshotStoreConnectivityCheck`
  - Registers with tags: `["akka", "persistence", "sql", "snapshot-store", "connectivity"]`
  - Default name: `"Akka.Persistence.Sql.SnapshotStore.{id}.Connectivity"`

---

## 2. Akka.Hosting Version Information

### Current Akka.Hosting Version: 1.5.55-beta1
- **Akka.Hosting Location**: `/home/aaronontheweb/repositories/olympus/Akka.Hosting`
- **Current Commit**: `602df3a Prepare for 1.5.55-beta1 release (#685)`

### Key Akka.Hosting Components

#### 2.1 Persistence Health Check Infrastructure
Located in: `/home/aaronontheweb/repositories/olympus/Akka.Hosting/src/Akka.Persistence.Hosting/`

**HealthChecks.cs**:
- `HealthCheckExt`: Extension methods to convert persistence health check results to standard health checks
- `JournalHealthCheck`: Internal health check for journal plugin status
- `SnapshotStoreHealthCheck`: Internal health check for snapshot store plugin status
- Uses internal Akka.Persistence APIs (`Persistence.CheckJournalHealthAsync`, `Persistence.CheckSnapshotStoreHealthAsync`)

#### 2.2 Builder Classes in Akka.Persistence.Hosting

**AkkaPersistenceJournalBuilder.cs**:
```csharp
public sealed class AkkaPersistenceJournalBuilder
```
- Properties:
  - `JournalId`: string identifier
  - `Builder`: AkkaConfigurationBuilder reference
  - `Bindings`: Dictionary of event adapter type bindings
  - `Adapters`: Dictionary of event adapter types
  - `HealthCheckRegistrations`: HashSet<AkkaHealthCheckRegistration> (NEW)

- Methods:
  - `WithHealthCheck(HealthStatus, string?, IEnumerable<string>?)`: Register built-in health check
  - `WithCustomHealthCheck(AkkaHealthCheckRegistration)`: Register custom health check (supports #678)
  - `AddEventAdapter<TAdapter>()`: Add event adapter
  - `AddReadEventAdapter<TAdapter>()`: Add read event adapter
  - `AddWriteEventAdapter<TAdapter>()`: Add write event adapter
  - `Build()`: Internal method that applies all health checks and adapters

**AkkaPersistenceSnapshotBuilder.cs**:
```csharp
public sealed class AkkaPersistenceSnapshotBuilder
```
- Properties:
  - `SnapshotStoreId`: string identifier
  - `Builder`: AkkaConfigurationBuilder reference
  - `HealthCheckRegistrations`: HashSet<AkkaHealthCheckRegistration>

- Methods:
  - `WithHealthCheck(HealthStatus, string?, IEnumerable<string>?)`: Register built-in health check
  - `WithCustomHealthCheck(AkkaHealthCheckRegistration)`: Register custom health check
  - `Build()`: Internal method that applies health checks

#### 2.3 Persistence Hosting Extensions
**AkkaPersistenceHostingExtensions.cs**:
- Main extension methods that create builder instances:
  - `WithJournalAndSnapshot()`: 4 overloads for configuring both
  - `WithJournal()`: 2 overloads for journal only
  - `WithSnapshot()`: 2 overloads for snapshot store only

**Health Check Extension in Akka.Hosting**:
Located in: `/home/aaronontheweb/repositories/olympus/Akka.Hosting/src/Akka.Hosting/HealthChecks/AkkaHealthCheckExtensions.cs`
```csharp
public static HealthCheckRegistration ToHealthCheckRegistration(
    this AkkaHealthCheckRegistration registration)
```

---

## 3. TODO Comments and Incomplete Implementations

### TODOs found (NOT related to connectivity checks):
All TODO comments found are in non-hosting files:

1. **Snapshot/ByteArrayDateTimeSnapshotSerializer.cs** - Hack reference to Akka.NET issue #3811
2. **Snapshot/ByteArrayLongSnapshotSerializer.cs** - Hack reference to Akka.NET issue #3811
3. **Db/AkkaPersistenceDataConnectionFactory.cs** - UseEventManifestColumn always false
4. **Config/JournalTableConfig.cs** - Settings not implemented
5. **ConfigKeys.cs** - Config key to be removed
6. **Journal/SqlWriteJournal.cs** - CurrentTimeMillis comment
7. **Journal/Dao/ByteArrayJournalSerializer.cs** - Multiple hack references
8. **Query/SqlReadJournal.cs** - Signal shutdown to query executor

### No TODOs Found in Connectivity Check Implementation
- The connectivity check implementation appears complete with no unfinished work marked

---

## 4. Implemented Connectivity Check Classes

### Summary of Implemented Classes

| Class | Location | Type | Purpose |
|-------|----------|------|---------|
| `SqlJournalConnectivityCheck` | Hosting/SqlJournalConnectivityCheck.cs | Health Check | Verifies journal database connectivity |
| `SqlSnapshotStoreConnectivityCheck` | Hosting/SqlSnapshotStoreConnectivityCheck.cs | Health Check | Verifies snapshot database connectivity |
| `SqlConnectivityCheckExtensions` | Hosting/HostingExtensions.cs | Extensions | Provides WithConnectivityCheck() methods |

### Class Details

**SqlJournalConnectivityCheck**:
```csharp
public sealed class SqlJournalConnectivityCheck : IAkkaHealthCheck
{
    private readonly string _connectionString;
    private readonly string _providerName;
    private readonly string _journalId;

    public SqlJournalConnectivityCheck(string connectionString, string providerName, string journalId)
    public async Task<HealthCheckResult> CheckHealthAsync(AkkaHealthCheckContext context, CancellationToken cancellationToken = default)
}
```

**SqlSnapshotStoreConnectivityCheck**:
```csharp
public sealed class SqlSnapshotStoreConnectivityCheck : IAkkaHealthCheck
{
    private readonly string _connectionString;
    private readonly string _providerName;
    private readonly string _snapshotStoreId;

    public SqlSnapshotStoreConnectivityCheck(string connectionString, string providerName, string snapshotStoreId)
    public async Task<HealthCheckResult> CheckHealthAsync(AkkaHealthCheckContext context, CancellationToken cancellationToken = default)
}
```

---

## 5. Hosting Extension Methods with WithConnectivityCheck()

Located in: `/home/aaronontheweb/repositories/olympus/Akka.Persistence.Sql/src/Akka.Persistence.Sql.Hosting/HostingExtensions.cs` (lines 582-667)

### New SqlConnectivityCheckExtensions Class

#### Method 1: Journal Connectivity Check
```csharp
public static AkkaPersistenceJournalBuilder WithConnectivityCheck(
    this AkkaPersistenceJournalBuilder builder,
    SqlJournalOptions journalOptions,
    HealthStatus unHealthyStatus = HealthStatus.Unhealthy,
    string? name = null)
```
- **Parameters**:
  - `builder`: The journal builder
  - `journalOptions`: Journal options containing connection details
  - `unHealthyStatus`: Status when check fails (default: Unhealthy)
  - `name`: Optional health check name

- **Implementation**:
  - Creates `AkkaHealthCheckRegistration` with `SqlJournalConnectivityCheck`
  - Validates connectionString and providerName are not null/whitespace
  - Uses reflection to access internal `Builder` property
  - Registers health check with tags: `["akka", "persistence", "sql", "journal", "connectivity"]`
  - Returns builder for chaining

#### Method 2: Snapshot Connectivity Check
```csharp
public static AkkaPersistenceSnapshotBuilder WithConnectivityCheck(
    this AkkaPersistenceSnapshotBuilder builder,
    SqlSnapshotOptions snapshotOptions,
    HealthStatus unHealthyStatus = HealthStatus.Unhealthy,
    string? name = null)
```
- **Parameters**:
  - `builder`: The snapshot builder
  - `snapshotOptions`: Snapshot options containing connection details
  - `unHealthyStatus`: Status when check fails (default: Unhealthy)
  - `name`: Optional health check name

- **Implementation**:
  - Creates `AkkaHealthCheckRegistration` with `SqlSnapshotStoreConnectivityCheck`
  - Validates connectionString and providerName are not null/whitespace
  - Uses reflection to access internal `Builder` property
  - Registers health check with tags: `["akka", "persistence", "sql", "snapshot-store", "connectivity"]`
  - Returns builder for chaining

### Reflection Usage
```csharp
private static readonly System.Reflection.PropertyInfo? JournalBuilderProperty =
    typeof(AkkaPersistenceJournalBuilder).GetProperty("Builder", 
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

private static readonly System.Reflection.PropertyInfo? SnapshotBuilderProperty =
    typeof(AkkaPersistenceSnapshotBuilder).GetProperty("Builder", 
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
```

---

## 6. Test Files Related to Connectivity Checks

Located in: `/home/aaronontheweb/repositories/olympus/Akka.Persistence.Sql/src/Akka.Persistence.Sql.Hosting.Tests/`

### Test Files

| File | Class | Focus |
|------|-------|-------|
| **SqlConnectivityCheckSpec.cs** | `SqliteConnectivityCheckSpec : IAsyncLifetime` | SQLite connectivity checks |
| **MySqlConnectivityCheckSpec.cs** | `MySqlConnectivityCheckSpec : IAsyncLifetime` | MySQL connectivity checks |
| **PostgreSqlConnectivityCheckSpec.cs** | `PostgreSqlConnectivityCheckSpec : IAsyncLifetime` | PostgreSQL connectivity checks |
| **SqlServerConnectivityCheckSpec.cs** | `SqlServerConnectivityCheckSpec : IAsyncLifetime` | SQL Server connectivity checks |
| **HealthCheckSpec.cs** | `HealthCheckSpec : TestKit, IClassFixture<SqliteContainer>` | Integration health checks |

### Test Coverage

Each database-specific test file includes:
- **Happy Path Tests**:
  - `Journal_Connectivity_Check_Should_Return_Healthy_When_Connected()`
  - `Snapshot_Connectivity_Check_Should_Return_Healthy_When_Connected()`
  
- **Failure Path Tests**:
  - `Journal_Connectivity_Check_Should_Return_Unhealthy_When_Disconnected()`
  - `Snapshot_Connectivity_Check_Should_Return_Unhealthy_When_Disconnected()`

- **Validation Tests**:
  - `Journal_Connectivity_Check_Should_Require_ConnectionString()`
  - `Journal_Connectivity_Check_Should_Require_ProviderName()`
  - `Journal_Connectivity_Check_Should_Require_JournalId()`
  - `Snapshot_Connectivity_Check_Should_Require_ConnectionString()`
  - `Snapshot_Connectivity_Check_Should_Require_ProviderName()`
  - `Snapshot_Connectivity_Check_Should_Require_SnapshotStoreId()`

### HealthCheckSpec.cs Integration Test

Tests the full integration:
- Registers health checks via `journalBuilder.WithHealthCheck()` and `snapshotBuilder.WithHealthCheck()`
- Verifies health checks appear in `HealthCheckService`
- Validates both journal and snapshot health checks are present
- Confirms overall system health is `Healthy`

#### Platform Compatibility
- Tests use `[SkipWindows]` attribute for Docker-based tests (MySQL, PostgreSQL, SQL Server)
- SQLite tests run on all platforms
- Custom xunit framework for platform detection reliability

---

## 7. Akka.Hosting Package References

### Package Resolution
Located in: `/home/aaronontheweb/repositories/olympus/Akka.Persistence.Sql/src/Akka.Persistence.Sql.Hosting/Akka.Persistence.Sql.Hosting.csproj`

```xml
<ItemGroup>
    <PackageReference Include="Akka.Persistence.Hosting" />
</ItemGroup>

<ItemGroup>
  <ProjectReference Include="..\Akka.Persistence.Sql\Akka.Persistence.Sql.csproj" />
</ItemGroup>
```

- **Version**: Not pinned in this file (uses Directory.Generated.props or transitive dependency)
- **Akka Version**: 1.5.55-beta1 (per latest dev branch)
- **Framework Targets**:
  - `$(NetStandardLibVersion)` = `netstandard2.0`
  - `$(NetLibVersion)` = `net6.0`

### Test Project References
Located in: `/home/aaronontheweb/repositories/olympus/Akka.Persistence.Sql/src/Akka.Persistence.Sql.Hosting.Tests/Akka.Persistence.Sql.Hosting.Tests.csproj`

```xml
<ItemGroup>
    <PackageReference Include="Akka" />
    <PackageReference Include="Akka.Hosting.TestKit" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
</ItemGroup>
```

- **Framework Target**: `net8.0` (`$(NetCoreTestVersion)`)

---

## 8. Recent Commit History (Feature Branch)

### Key Commits on feature/sql-connectivity-health-checks:

1. **ad2df4c** - Revert SqlFrameworkDiscoverer changes to fix test execution
2. **8c78e88** - Enhance platform detection reliability in test framework
3. **40d7cf2** - Register custom xunit test framework in Hosting.Tests assembly
4. **71a5338** - Fix xunit test discovery logic for platform-specific test skipping
5. **e9d4244** - Skip Docker-based connectivity check tests on Windows CI
6. **119a91e** - Add connectivity health checks to features documentation
7. **03e69cc** - Add documentation for customizing health check tags
8. **ec63b03** - Merge branch 'dev' into feature/sql-connectivity-health-checks
9. **46cd742** - Add SQL connectivity check tests for all database providers
10. **9d37fd7** - Simplify SQL connectivity check tests
11. **6b1c59c** - Implement SQL connectivity health checks for Akka.Persistence

---

## 9. File Structure Summary

### Source Directory
```
/src/Akka.Persistence.Sql.Hosting/
├── HostingExtensions.cs                    [MAIN FILE - Contains WithConnectivityCheck methods]
├── SqlJournalConnectivityCheck.cs          [Journal health check implementation]
├── SqlSnapshotStoreConnectivityCheck.cs    [Snapshot health check implementation]
├── SqlJournalOptions.cs
├── SqlSnapshotOptions.cs
├── JournalDatabaseOptions.cs
├── SnapshotDatabaseOptions.cs
├── JournalTableOptions.cs
├── SnapshotTableOptions.cs
├── MetadataTableOptions.cs
├── TagTableOptions.cs
├── DatabaseMapping.cs
├── Extensions.cs
└── Akka.Persistence.Sql.Hosting.csproj

/src/Akka.Persistence.Sql.Hosting.Tests/
├── HealthCheckSpec.cs                      [Integration test]
├── SqlConnectivityCheckSpec.cs             [SQLite connectivity tests]
├── MySqlConnectivityCheckSpec.cs           [MySQL connectivity tests]
├── PostgreSqlConnectivityCheckSpec.cs      [PostgreSQL connectivity tests]
├── SqlServerConnectivityCheckSpec.cs       [SQL Server connectivity tests]
└── Akka.Persistence.Sql.Hosting.Tests.csproj
```

---

## 10. Akka.Hosting Integration Points

### How SqlConnectivityCheckExtensions Integrates with Akka.Hosting:

1. **Extends Builder Classes**:
   - `AkkaPersistenceJournalBuilder` (from Akka.Persistence.Hosting)
   - `AkkaPersistenceSnapshotBuilder` (from Akka.Persistence.Hosting)

2. **Uses Akka.Hosting Interfaces**:
   - `IAkkaHealthCheck`: Implemented by `SqlJournalConnectivityCheck` and `SqlSnapshotStoreConnectivityCheck`
   - `AkkaHealthCheckContext`: Passed to health check methods
   - `AkkaHealthCheckRegistration`: Created and registered in builders

3. **Registration Flow**:
   ```
   WithConnectivityCheck()
      ↓
   Creates AkkaHealthCheckRegistration
      ↓
   Uses reflection to access AkkaPersistenceJournalBuilder.Builder
      ↓
   Calls builder.WithHealthCheck(registration)
      ↓
   Registered with Akka.Hosting health check system
   ```

4. **Health Check Tags Structure**:
   - Journal: `["akka", "persistence", "sql", "journal", "connectivity"]`
   - Snapshot: `["akka", "persistence", "sql", "snapshot-store", "connectivity"]`
   - Integration adds "akka" tag (via AkkaHealthCheckExtensions)

---

## 11. Database Provider Support

### Supported Providers (via LinqToDB)
- SQL Server (2019+)
- PostgreSQL
- MySQL/MariaDB
- SQLite
- Oracle Database (via Linq2Db support)
- Others via Linq2Db provider abstraction

### Test Coverage
- SQLite: Full coverage (all platforms)
- MySQL: Full coverage (Linux/Mac only, uses Docker)
- PostgreSQL: Full coverage (Linux/Mac only, uses Docker)
- SQL Server: Full coverage (Linux/Mac only, uses Docker)

---

## 12. Key Documentation Files

### In Akka.Persistence.Sql Repo:
- **README.md**: Health Checks section (lines 79-100+)
  - Describes health check feature
  - Shows example usage
  - Explains automatic verification

### Features Documentation:
- Located in: `docs/articles/features.md`
- Updated with connectivity health check documentation

---

## Summary of Implementation

### Status: COMPLETE

All required components for SQL connectivity health checks are implemented:

1. ✅ **Connectivity Check Classes**: 2 classes (Journal + Snapshot)
2. ✅ **Extension Methods**: 2 `WithConnectivityCheck()` methods
3. ✅ **Akka.Hosting Integration**: Uses latest Akka.Hosting 1.5.55-beta1
4. ✅ **Test Coverage**: Comprehensive tests for all database providers
5. ✅ **Documentation**: README.md updated with examples
6. ✅ **Builder Support**: Integrates with `AkkaPersistenceJournalBuilder` and `AkkaPersistenceSnapshotBuilder`
7. ✅ **Health Check Registration**: Proper registration with custom tags
8. ✅ **Error Handling**: Timeouts and connection failures handled appropriately

### No Outstanding TODOs
- No TODO comments in connectivity check implementation files
- All features appear complete and tested
