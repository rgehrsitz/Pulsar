using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Pulsar.Core.Services;
using Pulsar.Runtime.Services;
using Serilog;
using StackExchange.Redis;
using Testcontainers.Redis;
using Xunit.Abstractions;

namespace Pulsar.IntegrationTests.Helpers;

public class RedisTestContainer : IAsyncDisposable
{
    private readonly RedisContainer _container;
    private readonly ITestOutputHelper _output;
    private IConnectionMultiplexer? _connection;
    private IServiceProvider? _services;
    public TestMetricsService Metrics { get; } = new();

    public RedisTestContainer(ITestOutputHelper output)
    {
        _output = output;
        // Use random port assignment
        _container = new RedisBuilder()
            .WithImage("redis:7.2-alpine")
            .WithPortBinding(6379, true) // This will map 6379 to a random available host port
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(6379))
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        _connection = await ConnectionMultiplexer.ConnectAsync(_container.GetConnectionString());

        var services = new ServiceCollection();
        var logger = new LoggerConfiguration()
            .WriteTo.TestOutput(_output)
            .CreateLogger();

        services.AddSingleton(_connection);
        services.AddSingleton<ILogger>(logger);
        services.AddSingleton<IMetricsService>(Metrics);
        services.AddSingleton<IDataStore, RedisDataStore>();
        services.AddSingleton<TimeSeriesService>();

        _services = services.BuildServiceProvider();
    }

    public T GetService<T>() where T : notnull
    {
        if (_services == null)
            throw new InvalidOperationException("Container not initialized");

        return _services.GetRequiredService<T>();
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
            await _connection.DisposeAsync();

        await _container.DisposeAsync();
    }
}
