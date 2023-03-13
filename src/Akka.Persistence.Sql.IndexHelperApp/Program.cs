// -----------------------------------------------------------------------
//  <copyright file="Program.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.IO;
using Akka.Configuration;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.HelperLib;
using CommandLine;
using FluentMigrator;
using FluentMigrator.Expressions;
using FluentMigrator.Runner.Generators;
using FluentMigrator.Runner.Generators.Generic;
using FluentMigrator.Runner.Generators.MySql;
using FluentMigrator.Runner.Generators.Oracle;
using FluentMigrator.Runner.Generators.Postgres;
using FluentMigrator.Runner.Generators.Postgres92;
using FluentMigrator.Runner.Generators.SQLite;
using FluentMigrator.Runner.Generators.SqlServer;
using FluentMigrator.Runner.Processors.Postgres;
using Microsoft.Extensions.Options;

namespace Akka.Persistence.Sql.IndexHelperApp
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(
                    opts =>
                    {
                        var configuration = ConfigurationFactory.ParseString(File.ReadAllText(opts.File));

                        var journalConfig = new JournalConfig(
                            configuration
                                .GetConfig(opts.HoconPath)
                                .WithFallback(Linq2DbPersistence.DefaultConfiguration));

                        var generator = GetGenerator(journalConfig.ProviderName);
                        var helper = new JournalIndexHelper();

                        GeneratePerOptions(opts, helper, journalConfig, generator);
                    });
        }

        private static void GeneratePerOptions(
            Options opts,
            JournalIndexHelper helper,
            JournalConfig journalConf,
            IMigrationGenerator generator)
        {
            if (opts.GeneratePidSeqNo)
            {
                var orderingExpr = new CreateIndexExpression
                {
                    Index = helper.JournalOrdering(
                        journalConf.TableConfig.EventJournalTable.Name,
                        journalConf.TableConfig.EventJournalTable.ColumnNames.Ordering,
                        journalConf.TableConfig.SchemaName)
                };

                GenerateWithHeaderAndFooter(generator, orderingExpr, "Ordering");

                var indexExpr = new CreateIndexExpression
                {
                    Index = helper.DefaultJournalIndex(
                        journalConf.TableConfig.EventJournalTable.Name,
                        journalConf.TableConfig.EventJournalTable.ColumnNames.PersistenceId,
                        journalConf.TableConfig.EventJournalTable.ColumnNames.SequenceNumber,
                        journalConf.TableConfig.SchemaName)
                };

                GenerateWithHeaderAndFooter(generator, indexExpr, "PidAndSequenceNo");
            }

            if (opts.GenerateTimestamp)
            {
                var timestampExpr = new CreateIndexExpression
                {
                    Index = helper.JournalTimestamp(
                        journalConf.TableConfig.EventJournalTable.Name,
                        journalConf.TableConfig.EventJournalTable.ColumnNames.Created,
                        journalConf.TableConfig.SchemaName)
                };

                GenerateWithHeaderAndFooter(generator, timestampExpr, "Timestamp");
            }
        }

        private static void GenerateWithHeaderAndFooter(
            IMigrationGenerator generator,
            CreateIndexExpression expr,
            string indexType)
        {
            Console.WriteLine("-------");
            Console.WriteLine($"----{indexType} Index Create Below");
            Console.WriteLine(generator.Generate(expr));
            Console.WriteLine($"----{indexType} Index Create Above");
            Console.WriteLine("-------");
        }

        private static GenericGenerator GetGenerator(string dbArg)
        {
            const StringComparison comp = StringComparison.InvariantCultureIgnoreCase;

            return dbArg switch
            {
                _ when dbArg.StartsWith("sqlserver", comp) => new SqlServer2008Generator(),
                _ when dbArg.Contains("sqlite", comp) => new SQLiteGenerator(),
                _ when dbArg.Contains("postgresql", comp) =>
                    new Postgres92Generator(
                        new PostgresQuoter(new PostgresOptions()),
                        new OptionsWrapper<GeneratorOptions>(new GeneratorOptions())),
                _ when dbArg.Contains("mysql", comp) => new MySql5Generator(),
                _ when dbArg.Contains("oracle", comp) => new OracleGenerator(),
                _ => throw new Exception("IDK what to do with this!")
            };
        }
    }
}
