using System;
using System.IO;
using Akka.Configuration;
using Akka.Persistence.Linq2Db.HelperLib;
using Akka.Persistence.Sql.Linq2Db;
using Akka.Persistence.Sql.Linq2Db.Config;
using Akka.Persistence.Sql.Linq2Db.Tests;
using CommandLine;
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
using LinqToDB;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Akka.Persistence.Linq2Db.IndexHelperApp
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(opts =>
                {
                    //var str = Linq2DbJournalDefaultSpecConfig.customConfig("testGen",
                    //    "journalTbl", "metadataTbl", ProviderName.SqlServer,
                    //    "connStr");
                    var conf = ConfigurationFactory.ParseString(File.ReadAllText(opts.File));

                    var journalConf = new JournalConfig(conf
                        .GetConfig(opts.HoconPath)
                        //.GetConfig("akka.persistence.journal.linq2db.testGen")
                        .WithFallback(Linq2DbPersistence.DefaultConfiguration));
                    var generator = GetGenerator(journalConf.ProviderName);
                    var helper = new JournalIndexHelper();
                    GeneratePerOptions(opts, helper, journalConf, generator);
                });
        }

        private static void GeneratePerOptions(
            Options opts, 
            JournalIndexHelper helper,
            JournalConfig journalConf, 
            GenericGenerator generator)
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
                var timestampExpr = new CreateIndexExpression()
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
            GenericGenerator generator,
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
                _ when dbArg.Contains("postgres", comp) =>
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