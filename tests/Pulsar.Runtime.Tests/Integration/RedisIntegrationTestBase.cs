using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Pulsar.Runtime.Configuration;
using Pulsar.Runtime.Engine;
using Pulsar.Runtime.Services;
using Pulsar.Runtime.Storage;
using Pulsar.Runtime.Tests.Mocks;
using Serilog;
using Xunit;

namespace Pulsar.Runtime.Tests.Integration;

public abstract class RedisIntegrationTestBase : IAsyncLifetime
{
    protected IServiceProvider ServiceProvider { get; private set; } = null!;
    protected MockRedisClusterConfiguration RedisConfig { get; private set; } = null!;
    protected PulsarStateManager StateManager { get; private set; } = null!;
    protected ClusterHealthService HealthService { get; private set; } = null!;
    protected RedisSensorDataProvider SensorProvider { get; private set; } = null!;
    protected MetricsService MetricsService { get; private set; } = null!;
    protected Mock<ILogger> Logger { get; private set; } = null!;
    protected Mock<RuleEngine> RuleEngine { get; private set; } = null!;

    protected virtual void SetServices(
        IServiceProvider serviceProvider,
        MockRedisClusterConfiguration redisConfig,
        PulsarStateManager stateManager,
        ClusterHealthService healthService,
        RedisSensorDataProvider sensorProvider,
        MetricsService metricsService)
    {
        ServiceProvider = serviceProvider;
        RedisConfig = redisConfig;
        StateManager = stateManager;
        HealthService = healthService;
        SensorProvider = sensorProvider;
        MetricsService = metricsService;
    }

    protected virtual async Task InitializeServicesAsync()
    {
        var services = new ServiceCollection();

        // Initialize mocks
        Logger = new Mock<ILogger>();
        Logger.Setup(l => l.ForContext<It.IsAnyType>()).Returns(Logger.Object);

        RuleEngine = new Mock<RuleEngine>(MockBehavior.Strict);
        RuleEngine.Setup(r => r.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        RuleEngine.Setup(r => r.StopAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Configure services
        RedisConfig = new MockRedisClusterConfiguration(Logger.Object, Environment.MachineName);
        services.AddSingleton<RedisClusterConfiguration>(RedisConfig);
        services.AddSingleton(sp => RuleEngine.Object);
        services.AddSingleton<ClusterHealthService>();
        services.AddSingleton<RedisSensorDataProvider>();
        services.AddSingleton<MetricsService>();

        var serviceProvider = services.BuildServiceProvider();

        // Initialize services
        var stateManager = new PulsarStateManager(
            Logger.Object,
            RedisConfig,
            RuleEngine.Object,
            TimeSpan.FromMilliseconds(100));

        var healthService = serviceProvider.GetRequiredService<ClusterHealthService>();
        var sensorProvider = serviceProvider.GetRequiredService<RedisSensorDataProvider>();
        var metricsService = serviceProvider.GetRequiredService<MetricsService>();

        SetServices(
            serviceProvider,
            RedisConfig,
            stateManager,
            healthService,
            sensorProvider,
            metricsService);

        await Task.CompletedTask;
    }

    async Task IAsyncLifetime.InitializeAsync()
    {
        await InitializeServicesAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        if (ServiceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
        await Task.CompletedTask;
    }
}
