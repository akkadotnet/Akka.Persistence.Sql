using System.Data;
using Akka.Actor;
using Akka.Event;
using Akka.Hosting;
using Akka.Persistence;
using Akka.Persistence.Sql.Hosting;
using LinqToDB;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TransactionTest;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

const string connectionString = "Server=localhost, 1433;Database=akka;User Id=sa;Password='Strong(!)Password';TrustServerCertificate=true;";
//const string connectionString = "Server=MY-COMPUTER\\SQLEXPRESS;Database=akka;User Id=sa;Password='Strong(!)Password';TrustServerCertificate=true;";

await Host.CreateDefaultBuilder(args)
    .ConfigureLogging(
        (context, logger) =>
        {
            logger.ClearProviders();
            logger.AddConsole();
            logger.SetMinimumLevel(LogLevel.Debug);
        })
    .ConfigureServices(
        (context, services) =>
        {
            services.AddHostedService<StressTestService>();
            services.AddAkka(
                "TestSystem",
                (builder, provider) =>
                {
                    builder
                        .ConfigureLoggers(
                            logger =>
                            {
                                logger.LogLevel = Akka.Event.LogLevel.DebugLevel;
                                logger.ClearLoggers();
                                logger.AddLoggerFactory();
                            })
                        .WithSqlPersistence(
                            options =>
                            {
                                options.ConnectionString = connectionString;
                                options.ProviderName = ProviderName.SqlServer2016;
                            },
                            options =>
                            {
                                options.ConnectionString = connectionString;
                                options.ProviderName = ProviderName.SqlServer2016;
                            }
                        )
                        .AddHocon(
                            """
                            akka.persistence {
                                journal {
                                    auto-start-journals = [ "akka.persistence.journal.sql" ]
                                    sql {
                                        recovery-event-timeout = 120s
                                        circuit-breaker {
                                            call-timeout = 160s
                                        }
                                    }
                                }
                                snapshot-store {
                                    auto-start-snapshot-stores = [ "akka.persistence.snapshot-store.sql" ]
                                    sql {
                                        circuit-breaker {
                                            call-timeout = 160s
                                        }
                                    }
                                }
                            }
                            """, HoconAddMode.Prepend)
                        .WithActors(
                            (system, registry) =>
                            {
                                var actor = system.ActorOf(Props.Create(() => new ErrorListenerActor()));
                                registry.Register<ErrorListenerActor>(actor);
                            });
                });
        })
    .UseConsoleLifetime()
    .RunConsoleAsync();
    