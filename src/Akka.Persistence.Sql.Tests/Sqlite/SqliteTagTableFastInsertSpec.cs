using System.IO;
using Xunit;

namespace Akka.Persistence.Sql.Tests.Sqlite;

public class SqliteTagTableFastInsertSpec : TagTableFastInsertSpecBase
{
    public SqliteTagTableFastInsertSpec(ITestOutputHelper output) : base(output)
    {
    }

    // LinqToDB SQLiteBulkCopy.MaxParameters
    protected override int ProviderMaxParameters => 998;

    // The bundled SQLite (3.32.0 or later) accepts up to 32766 parameters per statement.
    protected override int? RaisedMaxParameters => 4000;

    protected override string ProviderName => LinqToDB.ProviderName.SQLiteMS;

    protected override string ConnectionString(string databaseId)
        => $"Data Source={DatabasePath(databaseId).Replace('\\', '/')};Pooling=False";

    protected override void Cleanup(string databaseId) => File.Delete(DatabasePath(databaseId));

    private static string DatabasePath(string databaseId)
        => Path.Combine(Path.GetTempPath(), $"sql-fast-insert-{databaseId}.db");
}
