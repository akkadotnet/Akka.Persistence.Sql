using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Runtime.InteropServices;
using Akka.Actor;
using Akka.Configuration;
using Akka.Persistence;
using Akka.Persistence.Journal;
using Akka.Persistence.Sql;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace Akka.Persistence.Sql.ForeignKey.Benchmarks;

internal static class Program
{
    private static readonly TimeSpan AskTimeout = TimeSpan.FromMinutes(2);
    private static readonly int[] BatchSizes = [1, 50];
    private static readonly int[] TagCardinalities = [2, 4];
    private const int EventsPerWriteCase = 2000;
    private const int Repetitions = 5;
    private const int DeleteActors = 50;
    private const int DeleteEventsPerActor = 100;
    private static bool _preferParametersOnMultiRowInsert;
    private static string _parentInsertMode = "off";

    public static async Task<int> Main(string[] args)
    {
        var options = Options.Parse(args);
        if (options.Help)
        {
            Console.WriteLine(Options.Usage);
            return 0;
        }

        _preferParametersOnMultiRowInsert = options.PreferParametersOnMultiRowInsert;
        _parentInsertMode = options.ParentInsertMode;
        if (_parentInsertMode != "off" &&
            typeof(Akka.Persistence.Sql.Config.BaseByteArrayJournalDaoConfig)
                .GetProperty("TagTableQueryableInsertMode") is null)
            throw new ArgumentException("Parent insert modes require a library containing the #580 batching implementation.");

        var engine = Engine.Create(options.Provider, options.AdminConnectionString, options.Image, options.ImageDigest);
        var databaseNames = DatabaseNames.Create();
        try
        {
            await engine.CreateDatabases(databaseNames);
            return await RunBenchmark(engine, databaseNames, options);
        }
        finally
        {
            await engine.DropDatabases(databaseNames);
        }
    }

    private static async Task<int> RunBenchmark(Engine engine, DatabaseNames databaseNames, Options options)
    {
        var baselineConnection = engine.ConnectionFor(databaseNames.Baseline);
        var foreignKeyConnection = engine.ConnectionFor(databaseNames.ForeignKey);

        await InitializeDatabase(baselineConnection, engine.ProviderName, tagCardinality: 2);
        await InitializeDatabase(foreignKeyConnection, engine.ProviderName, tagCardinality: 2);
        await engine.AddOrderingForeignKey(databaseNames.ForeignKey);
        await VerifyForeignKey(engine, databaseNames.ForeignKey);

        var version = await engine.ServerVersion();
        var results = new BenchmarkResults(
            SchemaVersion: 2,
            Engine: engine.Name,
            EngineVersion: version,
            Runtime: Environment.Version.ToString(),
            OperatingSystem: RuntimeInformationForResults(),
            ProviderName: engine.ProviderName,
            Image: engine.Image,
            ImageDigest: engine.ImageDigest,
            BenchmarkSourceSha256: options.BenchmarkSourceSha256,
            PreferParametersOnMultiRowInsert: options.PreferParametersOnMultiRowInsert,
            ParentInsertMode: options.ParentInsertMode,
            Repetitions: Repetitions,
            EventsPerWriteCase: EventsPerWriteCase,
            BatchSizes: BatchSizes,
            TagCardinalities: TagCardinalities,
            DeleteActors: DeleteActors,
            DeleteEventsPerActor: DeleteEventsPerActor,
            Scenarios: []);

        foreach (var tagCount in TagCardinalities)
        {
            foreach (var batchSize in BatchSizes)
            {
                var measurements = new List<PairedMeasurement>();
                for (var repetition = 0; repetition < Repetitions; repetition++)
                {
                    var order = repetition % 2 == 0
                        ? new[] { SchemaVariant.Baseline, SchemaVariant.ForeignKey }
                        : new[] { SchemaVariant.ForeignKey, SchemaVariant.Baseline };
                    var pair = new Dictionary<SchemaVariant, Measurement>();
                    foreach (var variant in order)
                    {
                        var connectionString = variant == SchemaVariant.Baseline
                            ? baselineConnection
                            : foreignKeyConnection;
                        var measurement = await MeasureWrites(
                            connectionString,
                            engine.ProviderName,
                            tagCount,
                            batchSize,
                            repetition,
                            variant);
                        pair.Add(variant, measurement);
                    }

                    measurements.Add(new PairedMeasurement(
                        repetition + 1,
                        pair[SchemaVariant.Baseline],
                        pair[SchemaVariant.ForeignKey]));
                }

                results = results with
                {
                    Scenarios = results.Scenarios.Append(new ScenarioResult(
                        "tagged-insert",
                        tagCount,
                        batchSize,
                        PairedSummary.Create(measurements),
                        measurements)).ToArray(),
                };
            }
        }

        var deletionPairs = new List<PairedMeasurement>();
        for (var repetition = 0; repetition < Repetitions; repetition++)
        {
            var order = repetition % 2 == 0
                ? new[] { SchemaVariant.Baseline, SchemaVariant.ForeignKey }
                : new[] { SchemaVariant.ForeignKey, SchemaVariant.Baseline };
            var pair = new Dictionary<SchemaVariant, Measurement>();
            foreach (var variant in order)
            {
                var connectionString = variant == SchemaVariant.Baseline
                    ? baselineConnection
                    : foreignKeyConnection;
                pair.Add(variant, await MeasureDeletes(connectionString, engine.ProviderName, repetition, variant));
            }

            deletionPairs.Add(new PairedMeasurement(
                repetition + 1,
                pair[SchemaVariant.Baseline],
                pair[SchemaVariant.ForeignKey]));
        }

        results = results with
        {
            Scenarios = results.Scenarios.Append(new ScenarioResult(
                "delete-range",
                TagCardinality: 4,
                BatchSize: DeleteEventsPerActor,
                PairedSummary.Create(deletionPairs),
                deletionPairs)).ToArray(),
        };

        var json = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(options.OutputPath, json);
        Console.WriteLine(json);
        return 0;
    }

    private static async Task InitializeDatabase(string connectionString, string providerName, int tagCardinality)
    {
        var system = CreateActorSystem(connectionString, providerName, tagCardinality, "schema-init");
        var actor = system.ActorOf(Props.Create(() => new BenchPersistentActor("schema-init")));
        await actor.Ask<WriteCompleted>(new WriteBatch([int.MinValue]), AskTimeout);
        await system.Terminate();
    }

    private static ActorSystem CreateActorSystem(
        string connectionString,
        string providerName,
        int tagCardinality,
        string name)
    {
        var adapterType = tagCardinality == 2 ? typeof(TwoTagAdapter) : typeof(FourTagAdapter);
        var config = ConfigurationFactory.ParseString($$"""
akka.persistence.journal {
    plugin = "akka.persistence.journal.sql"
    sql {
        connection-string = "{{connectionString}}"
        provider-name = "{{providerName}}"
        tag-write-mode = TagTable
        prefer-parameters-on-multirow-insert = {{_preferParametersOnMultiRowInsert.ToString().ToLowerInvariant()}}
        tagtable-asqueryable-insert-mode = {{_parentInsertMode}}
        auto-initialize = on
        event-adapters {
            benchmark-tags = "{{adapterType.AssemblyQualifiedName}}"
        }
        event-adapter-bindings {
            "System.Int32" = benchmark-tags
        }
    }
}
akka.persistence.query.journal.sql {
    connection-string = "{{connectionString}}"
    provider-name = "{{providerName}}"
}
""")
            .WithFallback(Persistence.DefaultConfig())
            .WithFallback(SqlPersistence.DefaultConfiguration);
        return ActorSystem.Create(name, config);
    }

    private static async Task<Measurement> MeasureWrites(
        string connectionString,
        string providerName,
        int tagCardinality,
        int batchSize,
        int repetition,
        SchemaVariant variant)
    {
        var system = CreateActorSystem(connectionString, providerName, tagCardinality,
            $"write-{variant}-{tagCardinality}-{batchSize}-{repetition}");
        try
        {
            var persistenceId = $"bench-write-{tagCardinality}-{batchSize}-{repetition}";
            var actor = system.ActorOf(Props.Create(() => new BenchPersistentActor(persistenceId)));
            const int warmupEvents = 200;
            for (var start = 0; start < warmupEvents; start += 50)
                await actor.Ask<WriteCompleted>(new WriteBatch(Values(50, start)), AskTimeout);
            var batchCount = EventsPerWriteCase / batchSize;
            var batchLatencies = new List<double>(batchCount);
            var eventLatencies = new List<double>(batchCount);
            var watch = Stopwatch.StartNew();
            for (var batch = 0; batch < batchCount; batch++)
            {
                var batchWatch = Stopwatch.StartNew();
                await actor.Ask<WriteCompleted>(new WriteBatch(Values(batchSize, warmupEvents + batch * batchSize)), AskTimeout);
                batchWatch.Stop();
                var elapsed = batchWatch.Elapsed.TotalMilliseconds;
                batchLatencies.Add(elapsed);
                eventLatencies.Add(elapsed / batchSize);
            }
            watch.Stop();
            await system.Terminate();
            var inserted = await VerifyInsertedRows(
                connectionString,
                persistenceId,
                EventsPerWriteCase + warmupEvents,
                tagCardinality);

            return Measurement.Create(
                elapsedMilliseconds: watch.Elapsed.TotalMilliseconds,
                events: EventsPerWriteCase,
                operationLatencies: batchLatencies,
                perEventLatencies: eventLatencies,
                expectedDeletedEvents: 0,
                observedDeletedEvents: 0,
                expectedInsertedEvents: EventsPerWriteCase + warmupEvents,
                observedJournalRows: inserted.JournalRows,
                observedTagRows: inserted.TagRows);
        }
        finally
        {
            if (!system.WhenTerminated.IsCompleted)
                await system.Terminate();
        }
    }

    private static int[] Values(int count, int start)
        => Enumerable.Range(start, count).Select(value => value + 1).ToArray();

    private static async Task<Measurement> MeasureDeletes(
        string connectionString,
        string providerName,
        int repetition,
        SchemaVariant variant)
    {
        var system = CreateActorSystem(connectionString, providerName, 4,
            $"delete-{variant}-{repetition}");
        try
        {
            var warmupActor = system.ActorOf(Props.Create(() => new BenchPersistentActor(
                    $"bench-delete-warmup-{repetition}")));
            for (var start = 0; start < 200; start += 50)
                await warmupActor.Ask<WriteCompleted>(new WriteBatch(Values(50, start)), AskTimeout);
            var warmupDelete = await warmupActor.Ask<DeleteCompleted>(new DeleteTo(200), AskTimeout);
            if (warmupDelete.SequenceNumber != 200)
                throw new InvalidOperationException("The journal confirmed an unexpected warmup deletion boundary.");

            var actors = new List<IActorRef>(DeleteActors);
            for (var actorIndex = 0; actorIndex < DeleteActors; actorIndex++)
            {
                var actor = system.ActorOf(Props.Create(() => new BenchPersistentActor(
                    $"bench-delete-{repetition}-{actorIndex}")));
                actors.Add(actor);
                await actor.Ask<WriteCompleted>(new WriteBatch(Values(DeleteEventsPerActor, 0)), AskTimeout);
            }

            var deletionLatencies = new List<double>(actors.Count);
            var watch = Stopwatch.StartNew();
            for (var actorIndex = 0; actorIndex < actors.Count; actorIndex++)
            {
                var deleteWatch = Stopwatch.StartNew();
                var deleted = await actors[actorIndex].Ask<DeleteCompleted>(
                    new DeleteTo(DeleteEventsPerActor), AskTimeout);
                deleteWatch.Stop();
                deletionLatencies.Add(deleteWatch.Elapsed.TotalMilliseconds);
                if (deleted.SequenceNumber != DeleteEventsPerActor)
                    throw new InvalidOperationException("The journal confirmed an unexpected deletion boundary.");
            }
            watch.Stop();
            await system.Terminate();

            var expectedDeletedEvents = DeleteActors * (DeleteEventsPerActor - 1);
            var deletePrefix = $"bench-delete-{repetition}-";
            var observedDeletedEvents = await CountDeletedJournalRows(connectionString, deletePrefix, expectedDeletedEvents);
            return Measurement.Create(
                watch.Elapsed.TotalMilliseconds,
                expectedDeletedEvents,
                operationLatencies: deletionLatencies,
                perEventLatencies: deletionLatencies.Select(latency => latency / (DeleteEventsPerActor - 1)).ToArray(),
                expectedDeletedEvents,
                observedDeletedEvents,
                expectedInsertedEvents: 0,
                observedJournalRows: 0,
                observedTagRows: 0);
        }
        finally
        {
            if (!system.WhenTerminated.IsCompleted)
                await system.Terminate();
        }
    }

    private static async Task<int> CountDeletedJournalRows(string connectionString, string persistencePrefix, int expected)
    {
        await using var connection = await OpenDatabaseConnection(connectionString);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM journal WHERE persistence_id LIKE @prefix AND sequence_number < @boundary";
        AddParameter(command, "prefix", persistencePrefix + "%");
        AddParameter(command, "boundary", DeleteEventsPerActor);
        var value = await command.ExecuteScalarAsync();
        var count = Convert.ToInt32(value, CultureInfo.InvariantCulture);
        var retained = await CountRows(connection, "SELECT COUNT(*) FROM journal WHERE persistence_id LIKE @prefix", persistencePrefix + "%");
        var expectedRetained = DeleteActors;
        if (retained != expectedRetained)
            throw new InvalidOperationException($"Expected {expectedRetained} deletion-boundary journal rows; observed {retained}.");
        if (count != 0)
            throw new InvalidOperationException($"Expected zero deleted journal rows remaining; observed {count}.");

        await using var tagCountCommand = connection.CreateCommand();
        tagCountCommand.CommandText = "SELECT COUNT(*) FROM tags WHERE persistence_id LIKE @prefix AND sequence_nr < @boundary";
        AddParameter(tagCountCommand, "prefix", persistencePrefix + "%");
        AddParameter(tagCountCommand, "boundary", DeleteEventsPerActor);
        var tagCount = Convert.ToInt32(await tagCountCommand.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
        if (tagCount != 0)
            throw new InvalidOperationException($"Expected zero deleted tag rows remaining; observed {tagCount}.");
        var retainedTags = await CountRows(connection, "SELECT COUNT(*) FROM tags WHERE persistence_id LIKE @prefix", persistencePrefix + "%");
        if (retainedTags != DeleteActors * 4)
            throw new InvalidOperationException($"Expected {DeleteActors * 4} tags on deletion-boundary journal rows; observed {retainedTags}.");

        var observedDeleted = DeleteActors * DeleteEventsPerActor - retained;
        if (observedDeleted != expected)
            throw new InvalidOperationException($"Expected {expected} deleted events; observed {observedDeleted}.");
        return observedDeleted;
    }

    private static async Task<(int JournalRows, int TagRows)> VerifyInsertedRows(
        string connectionString,
        string persistenceId,
        int expectedEvents,
        int tagsPerEvent)
    {
        await using var connection = await OpenDatabaseConnection(connectionString);
        await using var journalCommand = connection.CreateCommand();
        journalCommand.CommandText = "SELECT COUNT(*) FROM journal WHERE persistence_id = @persistenceId";
        AddParameter(journalCommand, "persistenceId", persistenceId);
        var journalRows = Convert.ToInt32(await journalCommand.ExecuteScalarAsync(), CultureInfo.InvariantCulture);

        await using var tagsCommand = connection.CreateCommand();
        tagsCommand.CommandText = "SELECT COUNT(*) FROM tags WHERE persistence_id = @persistenceId";
        AddParameter(tagsCommand, "persistenceId", persistenceId);
        var tagRows = Convert.ToInt32(await tagsCommand.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
        var expectedTags = expectedEvents * tagsPerEvent;
        if (journalRows != expectedEvents || tagRows != expectedTags)
        {
            throw new InvalidOperationException(
                $"Insert correctness check failed: expected {expectedEvents} journal rows and {expectedTags} tag rows, found {journalRows} and {tagRows}.");
        }

        return (journalRows, tagRows);
    }

    private static async Task<int> CountRows(DbConnection connection, string sql, string? persistencePrefix = null)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        if (persistencePrefix is not null)
            AddParameter(command, "prefix", persistencePrefix);
        return Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private static async Task VerifyForeignKey(Engine engine, string databaseName)
    {
        var connectionString = engine.ConnectionFor(databaseName);
        await using var connection = await OpenDatabaseConnection(connectionString);
        await using var command = connection.CreateCommand();
        command.CommandText = engine.ForeignKeyExistsQuery;
        var exists = Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture) != 0;
        if (!exists)
            throw new InvalidOperationException("The foreign key was not installed in the FK database.");

        await using var violation = connection.CreateCommand();
        violation.CommandText = "INSERT INTO tags(ordering_id, tag, sequence_nr, persistence_id) VALUES (-1, 'probe', 1, 'probe')";
        var rejectedOrphan = false;
        try
        {
            await violation.ExecuteNonQueryAsync();
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            rejectedOrphan = true;
        }
        catch (SqlException exception) when (exception.Number == 547)
        {
            rejectedOrphan = true;
        }

        if (!rejectedOrphan)
            throw new InvalidOperationException("The database accepted an orphan tag row despite the foreign key.");
    }

    internal static async Task<DbConnection> OpenDatabaseConnection(string connectionString)
    {
        DbConnection connection = connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase)
            ? new NpgsqlConnection(connectionString)
            : new SqlConnection(connectionString);
        await connection.OpenAsync();
        return connection;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static string RuntimeInformationForResults()
        => $"{Environment.OSVersion.Platform} {Environment.OSVersion.Version}";
}

internal enum SchemaVariant
{
    Baseline,
    ForeignKey,
}

internal sealed record WriteBatch(int[] Events);
internal sealed record WriteCompleted(int Count);
internal sealed record DeleteTo(long SequenceNumber);
internal sealed record DeleteCompleted(long SequenceNumber);

internal sealed class BenchPersistentActor(string persistenceId) : UntypedPersistentActor
{
    private IActorRef? _deleteReplyTo;
    public override string PersistenceId { get; } = persistenceId;

    protected override void OnRecover(object message) { }

    protected override void OnCommand(object message)
    {
        switch (message)
        {
            case WriteBatch batch:
            {
                var replyTo = Sender;
                var remaining = batch.Events.Length;
                void OnPersisted(object _)
                {
                    remaining--;
                    if (remaining == 0)
                        replyTo.Tell(new WriteCompleted(batch.Events.Length));
                }

                if (batch.Events.Length == 1)
                    Persist(batch.Events[0], value => OnPersisted(value));
                else
                    PersistAll(batch.Events.Cast<object>(), OnPersisted);
                break;
            }
            case DeleteTo delete:
                _deleteReplyTo = Sender;
                DeleteMessages(delete.SequenceNumber);
                break;
            case DeleteMessagesSuccess success:
                _deleteReplyTo?.Tell(new DeleteCompleted(success.ToSequenceNr));
                _deleteReplyTo = null;
                break;
            case DeleteMessagesFailure failure:
                _deleteReplyTo?.Tell(new Status.Failure(failure.Cause));
                _deleteReplyTo = null;
                break;
        }
    }
}

internal sealed class TwoTagAdapter : IWriteEventAdapter
{
    private static readonly string[] Tags = ["bench-tag-0", "bench-tag-1"];
    public string Manifest(object evt) => string.Empty;
    public object ToJournal(object evt) => evt is int ? new Tagged(evt, Tags) : evt;
}

internal sealed class FourTagAdapter : IWriteEventAdapter
{
    private static readonly string[] Tags = ["bench-tag-0", "bench-tag-1", "bench-tag-2", "bench-tag-3"];
    public string Manifest(object evt) => string.Empty;
    public object ToJournal(object evt) => evt is int ? new Tagged(evt, Tags) : evt;
}

internal sealed class Engine
{
    private readonly string _adminConnectionString;
    private readonly bool _postgres;

    private Engine(string name, string providerName, string image, string imageDigest, string adminConnectionString, bool postgres)
    {
        Name = name;
        ProviderName = providerName;
        Image = image;
        ImageDigest = imageDigest;
        _adminConnectionString = adminConnectionString;
        _postgres = postgres;
    }

    public string Name { get; }
    public string ProviderName { get; }
    public string Image { get; }
    public string ImageDigest { get; }
    public string ForeignKeyExistsQuery => _postgres
        ? "SELECT COUNT(*) FROM pg_constraint WHERE conname = 'fk_tags_journal_ordering'"
        : "SELECT COUNT(*) FROM sys.foreign_keys WHERE name = 'fk_tags_journal_ordering'";

    public static Engine Create(string provider, string adminConnectionString, string image, string imageDigest)
    {
        if (string.IsNullOrWhiteSpace(image) || image.Contains('/', StringComparison.Ordinal))
            throw new ArgumentException("Provide a sanitized image label without a registry or host.", nameof(image));
        if (!imageDigest.StartsWith("sha256:", StringComparison.Ordinal) || imageDigest.Length != 71)
            throw new ArgumentException("Provide the image ID as a sha256 digest.", nameof(imageDigest));

        return provider.ToLowerInvariant() switch
        {
            "postgresql" => CreatePostgres(adminConnectionString, image, imageDigest),
            "sqlserver" => CreateSqlServer(adminConnectionString, image, imageDigest),
            _ => throw new ArgumentException("Provider must be postgresql or sqlserver.", nameof(provider)),
        };
    }

    private static Engine CreatePostgres(string adminConnectionString, string image, string imageDigest)
    {
        var builder = new NpgsqlConnectionStringBuilder(adminConnectionString);
        if (string.IsNullOrWhiteSpace(builder.Host) || !IsLoopback(builder.Host))
            throw new ArgumentException("Benchmark databases can only be created on a local PostgreSQL server.", nameof(adminConnectionString));
        return new Engine("PostgreSQL", "PostgreSQL.15", image, imageDigest, adminConnectionString, true);
    }

    private static Engine CreateSqlServer(string adminConnectionString, string image, string imageDigest)
    {
        var builder = new SqlConnectionStringBuilder(adminConnectionString);
        var host = builder.DataSource.Split(',')[0].Replace("tcp:", string.Empty, StringComparison.OrdinalIgnoreCase);
        if (!IsLoopback(host))
            throw new ArgumentException("Benchmark databases can only be created on a local SQL Server instance.", nameof(adminConnectionString));
        return new Engine("SQL Server", "SqlServer.2022", image, imageDigest, adminConnectionString, false);
    }

    private static bool IsLoopback(string host)
        => host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
           || host.Equals("127.0.0.1", StringComparison.Ordinal)
           || host.Equals("::1", StringComparison.Ordinal);

    public string ConnectionFor(string databaseName)
    {
        if (_postgres)
        {
            var builder = new NpgsqlConnectionStringBuilder(_adminConnectionString) { Database = databaseName };
            return builder.ConnectionString;
        }

        var sqlBuilder = new SqlConnectionStringBuilder(_adminConnectionString) { InitialCatalog = databaseName };
        return sqlBuilder.ConnectionString;
    }

    public async Task CreateDatabases(DatabaseNames names)
    {
        if (_postgres)
        {
            await using var connection = new NpgsqlConnection(_adminConnectionString);
            await connection.OpenAsync();
            foreach (var name in new[] { names.Baseline, names.ForeignKey })
            {
                await using var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", connection);
                await create.ExecuteNonQueryAsync();
            }
        }
        else
        {
            await using var connection = new SqlConnection(_adminConnectionString);
            await connection.OpenAsync();
            foreach (var name in new[] { names.Baseline, names.ForeignKey })
            {
                await using var create = new SqlCommand($"CREATE DATABASE [{name}]", connection);
                await create.ExecuteNonQueryAsync();
            }
        }
    }

    public async Task DropDatabases(DatabaseNames names)
    {
        if (_postgres)
        {
            NpgsqlConnection.ClearAllPools();
            await using var connection = new NpgsqlConnection(_adminConnectionString);
            await connection.OpenAsync();
            foreach (var name in new[] { names.Baseline, names.ForeignKey })
            {
                await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)", connection);
                await drop.ExecuteNonQueryAsync();
            }
            return;
        }

        SqlConnection.ClearAllPools();
        await using var sqlConnection = new SqlConnection(_adminConnectionString);
        await sqlConnection.OpenAsync();
        foreach (var name in new[] { names.Baseline, names.ForeignKey })
        {
            await using var drop = new SqlCommand($"IF DB_ID(N'{name}') IS NOT NULL BEGIN ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]; END", sqlConnection);
            await drop.ExecuteNonQueryAsync();
        }
    }

    public async Task AddOrderingForeignKey(string databaseName)
    {
        await using var connection = await Program.OpenDatabaseConnection(ConnectionFor(databaseName));
        await using var command = connection.CreateCommand();
        command.CommandText = "ALTER TABLE tags ADD CONSTRAINT fk_tags_journal_ordering FOREIGN KEY (ordering_id) REFERENCES journal(ordering) ON DELETE CASCADE";
        await command.ExecuteNonQueryAsync();
    }

    public async Task<string> ServerVersion()
    {
        await using var connection = await Program.OpenDatabaseConnection(_adminConnectionString);
        await using var command = connection.CreateCommand();
        command.CommandText = _postgres
            ? "SHOW server_version"
            : "SELECT CONVERT(varchar(30), SERVERPROPERTY('ProductVersion'))";
        return Convert.ToString(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture) ?? "unknown";
    }
}

internal sealed record DatabaseNames(string Baseline, string ForeignKey)
{
    public static DatabaseNames Create()
    {
        var suffix = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..16];
        return new DatabaseNames($"fkbench_b_{suffix}", $"fkbench_f_{suffix}");
    }
}
internal sealed record PairedMeasurement(int Repetition, Measurement Baseline, Measurement ForeignKey);
internal sealed record ScenarioResult(
    string Name,
    int TagCardinality,
    int BatchSize,
    PairedSummary Summary,
    IReadOnlyList<PairedMeasurement> Runs);
internal sealed record BenchmarkResults(
    int SchemaVersion,
    string Engine,
    string EngineVersion,
    string Runtime,
    string OperatingSystem,
    string ProviderName,
    string Image,
    string ImageDigest,
    string BenchmarkSourceSha256,
    bool PreferParametersOnMultiRowInsert,
    string ParentInsertMode,
    int Repetitions,
    int EventsPerWriteCase,
    IReadOnlyList<int> BatchSizes,
    IReadOnlyList<int> TagCardinalities,
    int DeleteActors,
    int DeleteEventsPerActor,
    IReadOnlyList<ScenarioResult> Scenarios);

internal sealed record Measurement(
    double ElapsedMilliseconds,
    int Events,
    double EventsPerSecond,
    double P95OperationLatencyMilliseconds,
    double P95PerEventNormalizedLatencyMilliseconds,
    int ExpectedDeletedEvents,
    int ObservedDeletedEvents,
    int ExpectedInsertedEvents,
    int ObservedJournalRows,
    int ObservedTagRows)
{
    public static Measurement Create(
        double elapsedMilliseconds,
        int events,
        IReadOnlyList<double> operationLatencies,
        IReadOnlyList<double> perEventLatencies,
        int expectedDeletedEvents,
        int observedDeletedEvents,
        int expectedInsertedEvents,
        int observedJournalRows,
        int observedTagRows)
        => new(
            elapsedMilliseconds,
            events,
            events / (elapsedMilliseconds / 1000),
            Percentile(operationLatencies, .95),
            Percentile(perEventLatencies, .95),
            expectedDeletedEvents,
            observedDeletedEvents,
            expectedInsertedEvents,
            observedJournalRows,
            observedTagRows);

    private static double Percentile(IReadOnlyList<double> values, double percentile)
    {
        if (values.Count == 0)
            return 0;
        return values.Order().ElementAt((int)Math.Ceiling(values.Count * percentile) - 1);
    }
}

internal sealed record PairedSummary(
    double BaselineMedianEventsPerSecond,
    double ForeignKeyMedianEventsPerSecond,
    double ThroughputChangePercent,
    double BaselineP95OperationLatencyMilliseconds,
    double ForeignKeyP95OperationLatencyMilliseconds,
    double BaselineP95PerEventNormalizedLatencyMilliseconds,
    double ForeignKeyP95PerEventNormalizedLatencyMilliseconds,
    double BaselineThroughputCoefficientOfVariationPercent,
    double ForeignKeyThroughputCoefficientOfVariationPercent)
{
    public static PairedSummary Create(IReadOnlyList<PairedMeasurement> runs)
    {
        var baselineRates = runs.Select(run => run.Baseline.EventsPerSecond).Order().ToArray();
        var foreignRates = runs.Select(run => run.ForeignKey.EventsPerSecond).Order().ToArray();
        var baselineMedian = Median(baselineRates);
        var foreignMedian = Median(foreignRates);
        return new PairedSummary(
            baselineMedian,
            foreignMedian,
            (foreignMedian / baselineMedian - 1) * 100,
            Median(runs.Select(run => run.Baseline.P95OperationLatencyMilliseconds).Order().ToArray()),
            Median(runs.Select(run => run.ForeignKey.P95OperationLatencyMilliseconds).Order().ToArray()),
            Median(runs.Select(run => run.Baseline.P95PerEventNormalizedLatencyMilliseconds).Order().ToArray()),
            Median(runs.Select(run => run.ForeignKey.P95PerEventNormalizedLatencyMilliseconds).Order().ToArray()),
            CoefficientOfVariation(baselineRates),
            CoefficientOfVariation(foreignRates));
    }

    private static double Median(IReadOnlyList<double> values)
        => values.Count % 2 == 0
            ? (values[values.Count / 2 - 1] + values[values.Count / 2]) / 2
            : values[values.Count / 2];

    private static double CoefficientOfVariation(IReadOnlyList<double> values)
    {
        var mean = values.Average();
        var variance = values.Sum(value => Math.Pow(value - mean, 2)) / values.Count;
        return Math.Sqrt(variance) / mean * 100;
    }
}

internal sealed class Options
{
    public required string Provider { get; init; }
    public required string AdminConnectionString { get; init; }
    public required string OutputPath { get; init; }
    public required string Image { get; init; }
    public required string ImageDigest { get; init; }
    public required string BenchmarkSourceSha256 { get; init; }
    public bool PreferParametersOnMultiRowInsert { get; init; }
    public string ParentInsertMode { get; init; } = "off";
    public bool Help { get; init; }

    public const string Usage = "Usage: dotnet run -c Release -- --provider postgresql|sqlserver --admin-connection-string <value> --image <sanitized-label> --image-digest sha256:<digest> --source-sha256 <sha256> [--prefer-parameters true|false] [--parent-insert-mode off|parameterized|inline] --output <path>";

    public static Options Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] is "--help" or "-h")
                return new Options { Help = true, Provider = string.Empty, AdminConnectionString = string.Empty, OutputPath = string.Empty, Image = string.Empty, ImageDigest = string.Empty, BenchmarkSourceSha256 = string.Empty };
            if (!args[i].StartsWith("--", StringComparison.Ordinal) || i + 1 >= args.Length)
                throw new ArgumentException(Usage);
            values[args[i][2..]] = args[++i];
        }

        var parentInsertMode = values.GetValueOrDefault("parent-insert-mode") ?? "off";
        if (parentInsertMode is not ("off" or "parameterized" or "inline"))
            throw new ArgumentException("Parent insert mode must be off, parameterized, or inline.");

        return new Options
        {
            Provider = values.GetValueOrDefault("provider") ?? throw new ArgumentException(Usage),
            AdminConnectionString = values.GetValueOrDefault("admin-connection-string") ?? throw new ArgumentException(Usage),
            OutputPath = values.GetValueOrDefault("output") ?? "benchmark-results.json",
            Image = values.GetValueOrDefault("image") ?? throw new ArgumentException(Usage),
            ImageDigest = values.GetValueOrDefault("image-digest") ?? throw new ArgumentException(Usage),
            BenchmarkSourceSha256 = values.GetValueOrDefault("source-sha256") ?? throw new ArgumentException(Usage),
            PreferParametersOnMultiRowInsert = bool.Parse(values.GetValueOrDefault("prefer-parameters") ?? "false"),
            ParentInsertMode = parentInsertMode,
        };
    }
}
