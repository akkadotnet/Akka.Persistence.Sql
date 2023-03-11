// -----------------------------------------------------------------------
//  <copyright file="Program.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.CommandLine;
using Akka.Persistence.Sql.HelperLib;

namespace Akka.Persistence.Sql.TagTableMigration;

public static class Program
{
    private static readonly Option<string> ConnectionString;
    private static readonly Option<DatabaseType> TableMapping;
    private static readonly Option<ProviderType> Provider;
    private static readonly Option<string> SchemaName;
    private static readonly Option<long> StartOffset;
    private static readonly Option<long?> EndOffset;
    private static readonly Option<int> BatchSize;

    static Program()
    {
        ConnectionString = new Option<string>(
            aliases: new[] { "--connection-string", "-c" },
            description: "Database connection string")
        {
            IsRequired = true
        };

        TableMapping = new Option<DatabaseType>(
            aliases: new[] { "--database", "-d" },
            description: "Target database being migrated")
        {
            IsRequired = true
        };

        Provider = new Option<ProviderType>(
            aliases: new[] { "--provider", "-p" },
            description: "Target database provider")
        {
            IsRequired = true
        };

        SchemaName = new Option<string>(
            aliases: new[] { "-s", "--schema-name" },
            description: "Optional. Database schema name");

        StartOffset = new Option<long>(
            aliases: new[] { "-o", "--offset" },
            description: "Optional. Starting database row offset",
            getDefaultValue: () => 0);

        EndOffset = new Option<long?>(
            aliases: new[] { "-e", "--end-offset" },
            description: "Optional. Ending database row offset",
            getDefaultValue: () => null);

        BatchSize = new Option<int>(
            aliases: new[] { "-b", "--batch-size" },
            description: "Optional. Batch size per migration transaction",
            getDefaultValue: () => 1000);
    }

    public static async Task<int> Main(params string[] args)
    {
        var root = new RootCommand("A helper application to migrate legacy Akka.Persistence.Sql database tables to Akka.Persistence.Sql");
        root.AddOption(ConnectionString);
        root.AddOption(TableMapping);
        root.AddOption(Provider);
        root.AddOption(SchemaName);
        root.AddOption(StartOffset);
        root.AddOption(EndOffset);
        root.AddOption(BatchSize);

        root.SetHandler(async (connectionString, tableMapping, provider, schema, offset, endOffset, batchSize) =>
        {
            var opt = new Options
            {
                ConnectionString = connectionString,
                TableMapping = tableMapping,
                Provider = provider,
                SchemaName = schema,
                StartOffset = offset,
                EndOffset = endOffset,
                BatchSize = batchSize
            };
            var migrator = new TagTableMigrator(opt.ToHocon());
            await migrator.Migrate(opt.StartOffset, opt.BatchSize, opt.EndOffset);
        }, ConnectionString, TableMapping, Provider, SchemaName, StartOffset, EndOffset, BatchSize);

        return await root.InvokeAsync(args);
    }
}

