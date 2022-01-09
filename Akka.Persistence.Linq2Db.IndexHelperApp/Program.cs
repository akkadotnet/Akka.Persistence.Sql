using System;
using System.IO;
using Akka.Configuration;
using Akka.Persistence.Linq2Db.IndexHelperLib;
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
    class Program
    {
        
        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(opts =>
                {
                    //var str = Linq2DbJournalDefaultSpecConfig.customConfig("testGen",
                    //    "journalTbl", "metadataTbl", ProviderName.SqlServer,
                    //    "connStr");
                    var conf =
                        ConfigurationFactory.ParseString(
                            File.ReadAllText(opts.File));

                    var journalConf =
                        new Akka.Persistence.Sql.Linq2Db.Config.JournalConfig(
                            conf.GetConfig(
                                    opts.HoconPath
                                    //"akka.persistence.journal.linq2db.testGen"
                                    )
                                .WithFallback(Akka.Persistence.Sql.Linq2Db
                                    .Journal
                                    .Linq2DbWriteJournal.DefaultConfiguration));
                    var generator = getGenerator(journalConf.ProviderName);
                    var helper = new JournalIndexHelper();
                    CreateIndexExpression expr = null;
                    GeneratePerOptions(opts, helper, journalConf, generator);
                });
        }

        private static void GeneratePerOptions(Options opts, JournalIndexHelper helper,
            JournalConfig journalConf, GenericGenerator generator)
        {
            CreateIndexExpression expr;
            if (opts.GeneratePidSeqNo)
            {
                expr = new CreateIndexExpression()
                {
                    Index = helper.JournalOrdering(journalConf.TableConfig.TableName,
                        journalConf.TableConfig.ColumnNames.Ordering,
                        journalConf.TableConfig.SchemaName)
                };
                GenerateWithHeaderAndFooter(generator, expr, "Ordering");
            }

            if (opts.GeneratePidSeqNo)
            {
                expr = new CreateIndexExpression()
                {
                    Index = helper.DefaultJournalIndex(
                        journalConf.TableConfig.TableName,
                        journalConf.TableConfig.ColumnNames.PersistenceId,
                        journalConf.TableConfig.ColumnNames.SequenceNumber,
                        journalConf.TableConfig.SchemaName)
                };
                GenerateWithHeaderAndFooter(generator, expr, "PidAndSequenceNo");
            }

            if (opts.GenerateTimestamp)
            {
                expr = new CreateIndexExpression()
                {
                    Index = helper.JournalTimestamp(journalConf.TableConfig.TableName,
                        journalConf.TableConfig.ColumnNames.Created,
                        journalConf.TableConfig.SchemaName)
                };
                GenerateWithHeaderAndFooter(generator, expr, "Timestamp");
            }
        }

        private static void GenerateWithHeaderAndFooter(GenericGenerator generator,
            CreateIndexExpression expr, string indexType)
        {
            Console.WriteLine("-------");
            Console.WriteLine($"----{indexType} Index Create Below");
            Console.WriteLine(generator.Generate(expr));
            Console.WriteLine($"----{indexType} Index Create Above");
            Console.WriteLine("-------");
        }

        static GenericGenerator getGenerator(string dbArg)
        {
            if (dbArg.StartsWith("sqlserver",
                StringComparison.InvariantCultureIgnoreCase))
            {
                return new SqlServer2008Generator();
            }
            else if (dbArg.Contains("sqlite",
                StringComparison.InvariantCultureIgnoreCase))
            {
                return new SQLiteGenerator();
            }
            else if (dbArg.Contains("postgres",
                StringComparison.InvariantCultureIgnoreCase))
            {
                return new Postgres92Generator(
                    new PostgresQuoter(new PostgresOptions()),
                    new OptionsWrapper<GeneratorOptions>(
                        new GeneratorOptions()));
            }
            else if (dbArg.Contains("mysql",
                StringComparison.InvariantCultureIgnoreCase))
            {
                return new MySql5Generator();
            }
            else if (dbArg.Contains("oracle",
                StringComparison.InvariantCultureIgnoreCase))
            {
                return new OracleGenerator();
            }
            else
            {
                throw new Exception("IDK what to do with this!");
            }
        }
    }
}