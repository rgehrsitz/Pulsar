using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Services;  // Ensure using Core's interface
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
    private readonly ILoggerFactory _loggerFactory;
    private IConnectionMultiplexer? _connection;
    private IServiceProvider? _services;
    public TestMetricsService Metrics { get; } = new();

    public RedisTestContainer(ITestOutputHelper output)
    {
        _output = output;
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddXUnit(output);
        });

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

        services.AddSingleton(_connection);
        services.AddSingleton(_loggerFactory);
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        services.AddSingleton<Core.Services.IMetricsService>(Metrics);  // Use Core's interface
        services.AddSingleton<IDataStore, RedisDataStore>();
        services.AddSingleton<TimeSeriesService>();

        // Remove this line to prevent conflicting IMetricsService registrations
        // services.AddSingleton<Pulsar.Runtime.Services.MetricsService>();

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