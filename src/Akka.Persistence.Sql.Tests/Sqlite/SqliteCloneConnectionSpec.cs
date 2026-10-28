using System.Threading;
using Akka.Configuration;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Db;
using FluentAssertions;
using LinqToDB;
using LinqToDB.Data;
using LinqToDB.Data.RetryPolicy;
using Xunit;

namespace Akka.Persistence.Sql.Tests.Sqlite;

/// <summary>
/// LinqToDB 6 removed DataConnection.Clone; these checks pin the LinqToDB 5 behavior we rely on:
/// a clone reuses the source connection's retry-policy instance instead of invoking the
/// configured retry-policy factory again.
/// </summary>
public class SqliteCloneConnectionSpec
{
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
