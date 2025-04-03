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

const string connectionString = "Server=MyComputer\\SQLEXPRESS;Database=akka;User Id=sa;Password='Strong(!)Password';";

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
                        );
                });
        })
    .UseConsoleLifetime()
    .RunConsoleAsync();
    