using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Services;
using Pulsar.Runtime.Engine;
using Pulsar.Runtime.Services;
using Pulsar.Runtime.Storage;
using Pulsar.CompiledRules;
using Serilog;
using Serilog.Extensions.Logging;
using StackExchange.Redis;
using Testcontainers.Redis;
using Xunit.Abstractions;

namespace Pulsar.IntegrationTests.Helpers;

public class RedisTestContainer : IAsyncDisposable
{
    private readonly RedisContainer _container;
    private readonly ILoggerFactory _loggerFactory;
    private readonly TestMetricsService _metrics;
    private ConnectionMultiplexer? _connection;
    private ServiceProvider? _services;

    public TestMetricsService Metrics => _metrics;

    public RedisTestContainer(ITestOutputHelper testOutput)
    {
        EnsureDockerIsRunning();

        _metrics = new TestMetricsService();

        var logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.TestOutput(testOutput)
            .CreateLogger();

        _loggerFactory = new SerilogLoggerFactory(logger);

        _container = new RedisBuilder()
            .WithImage("redis:7.2-alpine")
            .WithPortBinding(6379, true) // Use a random available port on the host
            .Build();
    }

    private void EnsureDockerIsRunning()
    {
        var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "info",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        process.Start();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "Docker is either not running or misconfigured. Please ensure that Docker is running and properly configured."
            );
        }
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var hostPort = _container.GetMappedPublicPort(6379); // Get the random port assigned
        _connection = await ConnectionMultiplexer.ConnectAsync($"localhost:{hostPort}");

        var services = new ServiceCollection();

        services.AddSingleton(_connection);
        services.AddSingleton(_loggerFactory);
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        services.AddSingleton<Serilog.ILogger>(sp =>
            new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Console().CreateLogger()
        );
        services.AddSingleton<Core.Services.IMetricsService>(_metrics);
        services.AddSingleton<TimeSeriesService>();
        services.AddSingleton<IDataStore, Runtime.Storage.RedisDataStore>();

        // Add rule engine registration
        services.AddSingleton<IRuleEngine>(sp =>
        {
            var dataStore = sp.GetRequiredService<IDataStore>();
            var actionExecutor = sp.GetRequiredService<IActionExecutor>();
            var logger = sp.GetRequiredService<Serilog.ILogger>();
            return new CompiledRuleEngine(dataStore, actionExecutor, logger);
        });

        // Add test rule engine registration
        services.AddSingleton<TestRuleEngine>(sp =>
        {
            var dataStore = sp.GetRequiredService<IDataStore>();
            var actionExecutor = sp.GetRequiredService<IActionExecutor>();
            var logger = sp.GetRequiredService<Serilog.ILogger>();
            return new TestRuleEngine(dataStore, actionExecutor, logger, this);
        });

        // Add action executor registrations
        services.AddSingleton<SendMessageActionExecutor>();
        services.AddSingleton<SetValueActionExecutor>();
        services.AddSingleton<IActionExecutor>(sp =>
        {
            var logger = sp.GetRequiredService<Serilog.ILogger>();
            var executors = new IActionExecutor[]
            {
                sp.GetRequiredService<SendMessageActionExecutor>(),
                sp.GetRequiredService<SetValueActionExecutor>(),
            };
            return new CompositeActionExecutor(logger, executors);
        });

        _services = services.BuildServiceProvider();
    }

    public T GetService<T>()
        where T : notnull
    {
        if (_services == null)
            throw new InvalidOperationException("Container not initialized");

        return _services.GetRequiredService<T>();
    }

    public IDatabase GetDatabase()
    {
        if (_connection == null)
            throw new InvalidOperationException("Redis connection not initialized");

        return _connection.GetDatabase();
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
            await _connection.DisposeAsync();

        await _container.DisposeAsync();
    }
}
