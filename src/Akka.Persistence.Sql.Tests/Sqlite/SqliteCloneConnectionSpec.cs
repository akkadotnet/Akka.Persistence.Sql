using System.Data.Common;
using System.Threading;
using Akka.Configuration;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Db;
using Akka.Persistence.Sql.Journal.Types;
using FluentAssertions;
using LinqToDB;
using LinqToDB.Data;
using LinqToDB.Data.RetryPolicy;
using Microsoft.Data.SqlClient;
using Npgsql;
using Xunit;

namespace Akka.Persistence.Sql.Tests.Sqlite;

/// <summary>
/// LinqToDB 6 removed DataConnection.Clone; these checks pin the LinqToDB 5 behavior we rely on:
/// a clone reuses the source connection's retry-policy instance instead of invoking the
/// configured retry-policy factory again.
/// </summary>
public class SqliteCloneConnectionSpec
{
    [Theory]
    [InlineData(ProviderName.PostgreSQL15)]
    [InlineData(ProviderName.SqlServer2022)]
    public void Clone_owns_an_independent_clone_of_a_supplied_connection(string providerName)
    {
        using DbConnection connection = providerName == ProviderName.PostgreSQL15
            ? new NpgsqlConnection("Host=localhost;Database=clone_test;Username=clone_test")
            : new SqlConnection("Server=localhost;Database=clone_test;Integrated Security=True");
        using var providerConnection = new DataConnection(new DataOptions()
            .UseConnectionString(providerName, connection.ConnectionString));
        var options = new DataOptions().UseConnection(providerConnection.DataProvider, connection);
        using var dataConnection = new DataConnection(options);
        using var source = new AkkaDataConnection(providerName, dataConnection);
        using var clone = source.Clone();
        var clonedDataConnection = (DataConnection)clone.GetTable<JournalRow>().DataContext;

        clonedDataConnection.TryGetDbConnection().Should().NotBeNull().And.NotBeSameAs(connection);
        clonedDataConnection.TryGetDbConnection()!.ConnectionString.Should().Be(connection.ConnectionString);
        dataConnection.TryGetDbConnection().Should().BeSameAs(connection);
    }

    [Fact]
    public void Clone_preserves_runtime_query_settings()
    {
        var options = new DataOptions().UseConnectionString(ProviderName.SQLiteMS, "Data Source=:memory:");
        using var dataConnection = new DataConnection(options) { CommandTimeout = 7, InlineParameters = true };
        dataConnection.QueryHints.Add("clone-query-hint");
        using var source = new AkkaDataConnection(ProviderName.SQLiteMS, dataConnection);
        using var clone = source.Clone();
        var clonedDataConnection = (DataConnection)clone.GetTable<JournalRow>().DataContext;

        clonedDataConnection.CommandTimeout.Should().Be(7);
        clonedDataConnection.InlineParameters.Should().BeTrue();
        clonedDataConnection.QueryHints.Should().Equal("clone-query-hint");
    }

    [Fact]
    public void Clone_does_not_share_an_external_transaction()
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var transaction = connection.BeginTransaction();
        using var providerConnection = new DataConnection(new DataOptions()
            .UseConnectionString(ProviderName.SQLiteMS, connection.ConnectionString));
        using var dataConnection = new DataConnection(new DataOptions()
            .UseTransaction(providerConnection.DataProvider, transaction));
        using var source = new AkkaDataConnection(ProviderName.SQLiteMS, dataConnection);
        using var clone = source.Clone();
        var clonedDataConnection = (DataConnection)clone.GetTable<JournalRow>().DataContext;

        clonedDataConnection.Transaction.Should().BeNull();
        using var independentTransaction = clonedDataConnection.BeginTransaction();
        clonedDataConnection.TryGetDbConnection().Should().NotBeSameAs(connection);
        dataConnection.Transaction.Should().BeSameAs(transaction);
        connection.State.Should().Be(System.Data.ConnectionState.Open);
    }

    [Fact]
    public void Clone_of_a_clone_does_not_rerun_the_original_connection_factory()
    {
        const string connectionString = "Data Source=:memory:";
        using var providerConnection = new DataConnection(new DataOptions()
            .UseConnectionString(ProviderName.SQLiteMS, connectionString));
        var calls = 0;
        var options = new DataOptions().UseConnectionFactory(providerConnection.DataProvider, _ =>
        {
            Interlocked.Increment(ref calls);
            return new Microsoft.Data.Sqlite.SqliteConnection(connectionString);
        });
        using var source = new AkkaDataConnection(ProviderName.SQLiteMS, new DataConnection(options));
        var initialCalls = calls;
        using var clone = source.Clone();
        calls.Should().Be(initialCalls + 1);
        using var secondClone = clone.Clone();
        calls.Should().Be(initialCalls + 1);
    }

    [Fact]
    public void Clone_reuses_the_resolved_data_provider_without_rerunning_its_factory()
    {
        const string connectionString = "Data Source=:memory:";
        using var providerConnection = new DataConnection(new DataOptions()
            .UseConnectionString(ProviderName.SQLiteMS, connectionString));
        var calls = 0;
        var options = new DataOptions().UseConnectionString(ProviderName.SQLiteMS, connectionString)
            .UseDataProviderFactory(_ =>
            {
                Interlocked.Increment(ref calls);
                return providerConnection.DataProvider;
            });
        using var source = new AkkaDataConnection(ProviderName.SQLiteMS, new DataConnection(options));
        var initialCalls = calls;
        using var clone = source.Clone();

        calls.Should().Be(initialCalls);
        clone.DataProvider.Should().BeSameAs(source.DataProvider);
    }

    [Fact]
    public void Clone_reuses_the_retry_policy_instance_without_invoking_the_factory()
    {
        var calls = 0;
        var options = new DataOptions()
            .UseConnectionString(ProviderName.SQLiteMS, "Data Source=:memory:")
            .UseFactory(_ =>
            {
                Interlocked.Increment(ref calls);
                return new AkkaSqlServerRetryPolicy();
            });

        using var source = new AkkaDataConnection(ProviderName.SQLiteMS, new DataConnection(options));
        using var clone = source.Clone();

        calls.Should().Be(1);
        var clonePolicy = clone.DetachRetryPolicy();
        clonePolicy.Should().NotBeNull();
        clonePolicy.Should().BeSameAs(source.DetachRetryPolicy());
    }

    [Fact]
    public void Clone_of_a_connection_without_a_retry_policy_has_none()
    {
        var options = new DataOptions().UseConnectionString(ProviderName.SQLiteMS, "Data Source=:memory:");

        using var source = new AkkaDataConnection(ProviderName.SQLiteMS, new DataConnection(options));
        using var clone = source.Clone();

        clone.DetachRetryPolicy().Should().BeNull();
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 3)]
    public void Factory_honors_use_clone_connection(bool useClone, int expectedFactoryCalls)
    {
        var calls = 0;
        var config = ConfigurationFactory.ParseString($$"""
            provider-name = {{ProviderName.SQLiteMS}}
            connection-string = "Data Source=:memory:"
            use-clone-connection = {{(useClone ? "true" : "false")}}
            """).WithFallback(SqlPersistence.DefaultJournalConfiguration);
        var journalConfig = new JournalConfig(config);
        var options = new DataOptions()
            .UseConnectionString(journalConfig.ProviderName, journalConfig.ConnectionString)
            .UseFactory(_ =>
            {
                Interlocked.Increment(ref calls);
                return new AkkaSqlServerRetryPolicy();
            });
        var factory = new AkkaPersistenceDataConnectionFactory(journalConfig.WithDataOptions(options));

        for (var i = 0; i < 3; i++)
        {
            using var connection = factory.GetConnection();
            connection.DetachRetryPolicy().Should().NotBeNull();
        }

        calls.Should().Be(expectedFactoryCalls);
    }
}
