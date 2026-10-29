using System.Threading.Tasks;
using Akka.Configuration;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Db;
using Akka.Persistence.Sql.Journal.Types;
using Akka.Persistence.Sql.Snapshot;
using Akka.Persistence.Sql.Tests.Common.Containers;
using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;
using Xunit;
#if !DEBUG
using Akka.Persistence.Sql.Tests.Common.Internal.Xunit;
#endif

namespace Akka.Persistence.Sql.Tests.MySql;

#if !DEBUG
[SkipWindows]
#endif
[Collection(nameof(MySqlPersistenceSpec))]
public class MySqlLegacyProviderSpec
{
    private readonly MySqlContainer _fixture;

    public MySqlLegacyProviderSpec(MySqlContainer fixture) => _fixture = fixture;

    [Theory]
    [InlineData("MySql.5.7", false)]
    [InlineData("MySql.5.7", true)]
    [InlineData("MySql.8.0", false)]
    [InlineData("MySql.8.0", true)]
    [InlineData("MySql.Official", false)]
    [InlineData("MySql.Official", true)]
    public async Task Legacy_provider_names_support_journal_and_snapshot_writes(string providerName, bool useClone)
    {
        await _fixture.InitializeDbAsync();
        var config = ConfigurationFactory.ParseString($$"""
            provider-name = "{{providerName}}"
            connection-string = "{{_fixture.ConnectionString}}"
            use-clone-connection = {{(useClone ? "true" : "false")}}
            """);
        var journalConfig = new JournalConfig(config.WithFallback(SqlPersistence.DefaultJournalConfiguration));
        var snapshotConfig = new SnapshotConfig(config.WithFallback(SqlPersistence.DefaultSnapshotConfiguration));
        var journalFactory = new AkkaPersistenceDataConnectionFactory(journalConfig);
        var snapshotFactory = new AkkaPersistenceDataConnectionFactory(snapshotConfig);

        await using var journal = journalFactory.GetConnection();
        await using var snapshot = snapshotFactory.GetConnection();
        var expectedPrefix = providerName == "MySql.Official" ? "MySql." : providerName + ".";
        Assert.StartsWith(expectedPrefix, journal.DataProvider.Name);
        Assert.StartsWith(expectedPrefix, snapshot.DataProvider.Name);
        if (providerName == "MySql.Official")
        {
            Assert.EndsWith(".MySql.Data", journal.DataProvider.Name);
            Assert.EndsWith(".MySql.Data", snapshot.DataProvider.Name);
        }
        await journal.CreateTableAsync<JournalRow>(TableOptions.CheckExistence);
        await snapshot.CreateTableAsync<LongSnapshotRow>(TableOptions.CheckExistence);

        var ordering = await journal.InsertWithInt64IdentityAsync(new JournalRow
        {
            PersistenceId = "legacy", SequenceNumber = 1, Message = new byte[] { 1, 2, 3 },
        });
        await snapshot.InsertOrReplaceAsync(new LongSnapshotRow
        {
            PersistenceId = "legacy", SequenceNumber = 1, Payload = new byte[] { 4, 5, 6 },
        });

        var row = await journal.GetTable<JournalRow>().SingleAsync();
        Assert.Equal(ordering, row.Ordering);
        Assert.Equal(new byte[] { 1, 2, 3 }, row.Message);
        var saved = await snapshot.GetTable<LongSnapshotRow>().SingleAsync();
        Assert.Equal("legacy", saved.PersistenceId);
        Assert.Equal(new byte[] { 4, 5, 6 }, saved.Payload);

        // Explicit DataOptions remain authoritative even when the HOCON name is a legacy alias.
        var options = new DataOptions().UseConnectionString(_fixture.ProviderName, _fixture.ConnectionString);
        await using var explicitJournal = new AkkaPersistenceDataConnectionFactory(journalConfig.WithDataOptions(options)).GetConnection();
        await using var explicitSnapshot = new AkkaPersistenceDataConnectionFactory(snapshotConfig.WithDataOptions(options)).GetConnection();
        Assert.Equal(_fixture.ProviderName, explicitJournal.DataProvider.Name);
        Assert.Equal(_fixture.ProviderName, explicitSnapshot.DataProvider.Name);
        Assert.Equal(ordering, (await explicitJournal.GetTable<JournalRow>().SingleAsync()).Ordering);
        Assert.Equal(saved.Payload, (await explicitSnapshot.GetTable<LongSnapshotRow>().SingleAsync()).Payload);
    }
}
