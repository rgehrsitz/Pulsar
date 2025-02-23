// File: Pulsar.Compiler/Config/Templates/Program.cs
// Version: 1.0.0

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pulsar.Runtime.Buffers;
using Pulsar.Runtime.Services;
using Pulsar.Runtime.Rules;
using Pulsar.Runtime.Interfaces;

namespace Pulsar.Runtime
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();
            await host.RunAsync();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    // Configure Redis
                    var redisConfig = hostContext.Configuration.GetSection("Redis").Get<RedisConfiguration>();
                    if (redisConfig == null)
                    {
                        throw new InvalidOperationException("Redis configuration is missing");
                    }
                    services.AddSingleton(redisConfig);

                    // Configure logging
                    var loggingConfig = hostContext.Configuration.GetSection("Logging").Get<RedisLoggingConfiguration>();
                    if (loggingConfig != null)
                    {
                        services.AddSingleton(loggingConfig);
                    }

                    // Configure runtime components
                    services.AddSingleton<IRedisService, RedisService>();
                    services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
                    services.AddSingleton<RingBufferManager>();
                    services.AddSingleton<IRuleCoordinator, RuleCoordinator>();

                    // Add the runtime orchestrator as a hosted service
                    services.AddHostedService<RuntimeHostedService>();
                });

        private class RuntimeHostedService : IHostedService
        {
            private readonly RuntimeOrchestrator _orchestrator;

            public RuntimeHostedService(
                IRedisService redis,
                ILogger<RuntimeOrchestrator> logger,
                IRuleCoordinator coordinator)
            {
                _orchestrator = new RuntimeOrchestrator(
                    redis,
                    logger,
                    coordinator);
            }

            public async Task StartAsync(CancellationToken cancellationToken)
            {
                await _orchestrator.StartAsync();
            }

            public async Task StopAsync(CancellationToken cancellationToken)
            {
                await _orchestrator.StopAsync();
            }
        }
    }
}
