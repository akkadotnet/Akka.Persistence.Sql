using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Akka.Util;
using Docker.DotNet;
using Docker.DotNet.Models;
using Xunit;

namespace Akka.Persistence.Sql.Linq2Db.Tests.Docker.Docker
{
    /// <summary>
    ///     Fixture used to run SQL Server
    /// </summary>
    public class SqlServerFixture : IAsyncLifetime
    {
        protected readonly string SqlContainerName = $"sqlserver-{Guid.NewGuid():N}";
        protected readonly DockerClient Client;

        public SqlServerFixture()
        {
            Client = new DockerClientConfiguration().CreateClient();
        }

        protected string ImageName => "mcr.microsoft.com/mssql/server";
        protected string Tag => "2017-latest";
        protected string SqlServerImageName => $"{ImageName}:{Tag}";

        public string ConnectionString { get; private set; }

        public async Task InitializeAsync()
        {
            var images = await Client.Images.ListImagesAsync(new ImagesListParameters
            {
                Filters = new Dictionary<string, IDictionary<string, bool>>
                {
                    ["reference"] = new Dictionary<string, bool>
                        {
                            [SqlServerImageName] = true
                        }
                }
            }); 

            if (images.Count == 0)
                await Client.Images.CreateImageAsync(
                    new ImagesCreateParameters {FromImage = ImageName, Tag = Tag}, null,
                    new Progress<JSONMessage>(message =>
                    {
                        Console.WriteLine(!string.IsNullOrEmpty(message.ErrorMessage)
                            ? message.ErrorMessage
                            : $"{message.ID} {message.Status} {message.ProgressMessage}");
                    }));

            var sqlServerHostPort = ThreadLocalRandom.Current.Next(9000, 10000);

            // create the container
            await Client.Containers.CreateContainerAsync(new CreateContainerParameters
            {
                Image = SqlServerImageName,
                Name = SqlContainerName,
                Tty = true,
                ExposedPorts = new Dictionary<string, EmptyStruct>
                {
                    ["1433/tcp"] = new EmptyStruct()
                },
                HostConfig = new HostConfig
                {
                    PortBindings = new Dictionary<string, IList<PortBinding>>
                    {
                        ["1433/tcp"] = new List<PortBinding>
                        {
                            new PortBinding { HostPort = $"{sqlServerHostPort}" }
                        }
                    }
                },
                Env = new[] {"ACCEPT_EULA=Y", "SA_PASSWORD=l0lTh1sIsOpenSource"}
            });

            // start the container
            await Client.Containers.StartContainerAsync(SqlContainerName, new ContainerStartParameters());

            // Provide a 30 second startup delay
            await Task.Delay(TimeSpan.FromSeconds(30));

            var connectionString = new DbConnectionStringBuilder
            {
                ConnectionString =
                    "data source=.;database=akka_persistence_tests;user id=sa;password=l0lTh1sIsOpenSource",
                ["Data Source"] = $"localhost,{sqlServerHostPort}"
            };

            ConnectionString = connectionString.ToString();
        }

        public async Task DisposeAsync()
        {
            try
            {
                await Client.Containers.StopContainerAsync(SqlContainerName, new ContainerStopParameters());
                await Client.Containers.RemoveContainerAsync(SqlContainerName, new ContainerRemoveParameters { Force = true });
            }
            catch
            {
                // no-op
            }
            Client.Dispose();
        }
    }
}