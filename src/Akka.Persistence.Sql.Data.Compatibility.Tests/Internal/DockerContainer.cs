﻿// -----------------------------------------------------------------------
//  <copyright file="DockerContainer.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Docker.DotNet;
using Docker.DotNet.Models;
using LanguageExt.UnitsOfMeasure;

namespace Akka.Persistence.Sql.Data.Compatibility.Tests.Internal
{
    public abstract class DockerContainer : ITestContainer
    {
        private readonly CancellationTokenSource _logsCts = new();

        private bool _disposing;
        private Task? _readDockerTask;
        
        protected DockerContainer(string imageName, string tag, string containerName)
        {
            ImageName = imageName;
            Tag = tag;
            ContainerName = containerName;
            Client = new DockerClientConfiguration().CreateClient();

            OnStdOut += (_, e) =>
            {
                Console.WriteLine($"[{ImageName}:{Tag}][stdout][{e.Output}");
            };
        }

        private string ImageName { get; }

        private string Tag { get; }

        private string FullImageName => $"{ImageName}:{Tag}";

        protected virtual string? ReadyMarker => null;

        protected virtual int ReadyCount => 1;

        protected virtual TimeSpan ReadyTimeout { get; } = TimeSpan.FromMinutes(1);

        public abstract string ConnectionString { get; }

        public virtual string DatabaseName => "akka_persistence_tests";

        public string ContainerName { get; }

        public DockerClient Client { get; }

        public event EventHandler<OutputReceivedArgs> OnStdOut;

        public async Task InitializeAsync()
        {
            var images = await Client.Images.ListImagesAsync(
                new ImagesListParameters
                {
                    Filters = new Dictionary<string, IDictionary<string, bool>>
                    {
                        {
                            "reference",
                            new Dictionary<string, bool>
                            {
                                { FullImageName, true },
                            }
                        },
                    },
                });

            if (images.Count == 0)
            {
                await Client.Images.CreateImageAsync(
                    new ImagesCreateParameters { FromImage = ImageName, Tag = Tag },
                    null,
                    new Progress<JSONMessage>(
                        message =>
                        {
                            Console.WriteLine(
                                !string.IsNullOrEmpty(message.ErrorMessage)
                                    ? message.ErrorMessage
                                    : $"{message.ID} {message.Status} {message.ProgressMessage}");
                        }));
            }

            // configure container parameters
            var options = new CreateContainerParameters();
            ConfigureContainer(options);
            options.Image = FullImageName;
            options.Name = ContainerName;
            options.Tty = true;

            // create the container
            await Client.Containers.CreateContainerAsync(options);
            
            // start the container
            var containerStarted = false;
            var retry = 3;
            do
            {
                try
                {
                    containerStarted = await Client.Containers.StartContainerAsync(ContainerName, new ContainerStartParameters());
                }
                catch (DockerApiException ex)
                {
                    Console.WriteLine($"Failed to start container {ContainerName}: {ex.StatusCode} {ex.ResponseBody}" );
                    if (--retry > 0)
                    {
                        await Task.Delay(5.Seconds());
                    }
                }
            } while (!containerStarted && retry > 0);
            
            // listen for container logs
            _readDockerTask = Client.Containers.GetContainerLogsAsync(
                id: ContainerName,
                parameters: new ContainerLogsParameters
                {
                    Follow = true,
                    ShowStdout = true,
                    ShowStderr = true,
                    Timestamps = true,
                },
                _logsCts.Token,
                new Progress<string>(
                    line => {
                        if (!string.IsNullOrEmpty(line))
                            OnStdOut(this, new OutputReceivedArgs(line));
                    }));

            // Wait until container is completely ready
            if (ReadyMarker is not null)
            {
                await AwaitUntilReadyAsync(ReadyMarker, ReadyTimeout);
            }
            else
            {
                await Task.Delay(20.Seconds());
            }

            await AfterContainerStartedAsync();
        }

        public async ValueTask DisposeAsync()
        {
            // Perform async cleanup.
            await DisposeAsyncCore().ConfigureAwait(false);

            Dispose(false);
            GC.SuppressFinalize(this);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected abstract void ConfigureContainer(CreateContainerParameters parameters);

        protected virtual Task AfterContainerStartedAsync()
            => Task.CompletedTask;

        private async Task AwaitUntilReadyAsync(string marker, TimeSpan timeout)
        {
            var tcs = new TaskCompletionSource<string>();
            var count = 0;

            void LineProcessor(object? sender, OutputReceivedArgs args)
            {
                if (!args.Output.Contains(marker))
                    return;

                count++;
                if (ReadyCount == count)
                    tcs.SetResult(args.Output);
            }

            OnStdOut += LineProcessor;

            using var cts = new CancellationTokenSource(timeout);
            try
            {
                var task = await Task.WhenAny(Task.Delay(Timeout.Infinite, cts.Token), tcs.Task);
                if (task == tcs.Task)
                    return;
                throw new Exception($"Docker image {ImageName}:{Tag} failed to run within {timeout}.");
            }
            finally
            {
                cts.Cancel();
                cts.Dispose();
                OnStdOut -= LineProcessor;
            }
        }

        protected virtual async ValueTask DisposeAsyncCore()
        {
            if (_disposing)
                return;

            _disposing = true;

            _logsCts.Cancel();
            _logsCts.Dispose();

            if (_readDockerTask is not null)
                try
                {
                    await _readDockerTask;
                }
                catch(TaskCanceledException _) { }

            try
            {
                await Client.Containers.StopContainerAsync(
                    id: ContainerName,
                    parameters: new ContainerStopParameters());

                await Client.Containers.RemoveContainerAsync(
                    id: ContainerName,
                    parameters: new ContainerRemoveParameters { Force = true });
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to stop and/or remove docker container. {e}");
            }
            finally
            {
                Client.Dispose();
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
                return;

            if (_disposing)
                return;

            _disposing = true;

            _logsCts.Cancel();
            _logsCts.Dispose();
            _readDockerTask?.GetAwaiter().GetResult();

            try
            {
                Client.Containers.StopContainerAsync(
                        id: ContainerName,
                        parameters: new ContainerStopParameters())
                    .GetAwaiter().GetResult();

                Client.Containers.RemoveContainerAsync(
                        id: ContainerName,
                        parameters: new ContainerRemoveParameters { Force = true })
                    .GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to stop and/or remove docker container. {e}");
            }
            finally
            {
                Client.Dispose();
            }
        }
    }
}
