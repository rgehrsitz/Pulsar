using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Pulsar.Runtime.Configuration;
using Pulsar.Runtime.Engine;
using Pulsar.Runtime.Services;
using Pulsar.Runtime.Storage;
using Pulsar.Runtime.Tests.Mocks;
using Pulsar.Compiler.Models;
using Pulsar.RuleDefinition.Models;
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

        // Configure services
        RedisConfig = new MockRedisClusterConfiguration(Logger.Object, Environment.MachineName);

        var metricsService = new Mock<MetricsService>(Logger.Object);
        var sensorProvider = new Mock<ISensorDataProvider>();
        var actionExecutor = new Mock<IActionExecutor>();
        var ruleSet = new MockCompiledRuleSet();

        RuleEngine = new Mock<RuleEngine>(
            Logger.Object,
            metricsService.Object,
            sensorProvider.Object,
            actionExecutor.Object,
            ruleSet,
            TimeSpan.FromSeconds(1)
        );

        services.AddSingleton<RedisClusterConfiguration>(RedisConfig);
        services.AddSingleton(RuleEngine.Object);
        services.AddSingleton<ClusterHealthService>();
        services.AddSingleton<MetricsService>();
        services.AddSingleton<SensorTemporalBufferService>();
        services.AddSingleton<RedisSensorDataProvider>();

        var serviceProvider = services.BuildServiceProvider();

        // Initialize services
        var stateManager = new PulsarStateManager(
            Logger.Object,
            RedisConfig,
            RuleEngine.Object,
            TimeSpan.FromMilliseconds(100));

        var healthService = serviceProvider.GetRequiredService<ClusterHealthService>();
        var sensorDataProvider = serviceProvider.GetRequiredService<RedisSensorDataProvider>();
        var metricsServiceInstance = serviceProvider.GetRequiredService<MetricsService>();

        SetServices(
            serviceProvider,
            RedisConfig,
            stateManager,
            healthService,
            sensorDataProvider,
            metricsServiceInstance);

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
