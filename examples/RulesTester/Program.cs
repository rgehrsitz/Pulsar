using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Pulsar.Runtime.Engine;
using Pulsar.Runtime.Services;
using Pulsar.Runtime.Storage;
using Pulsar.CompiledRules;
using Serilog;
using ILogger = Serilog.ILogger;

namespace RulesTester;

class Program
{
    static async Task Main()
    {
        // Setup Redis connection
        var redis = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
        var db = redis.GetDatabase();
        var server = redis.GetServer(redis.GetEndPoints().First());

        // Setup logging
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateLogger();

        // Clean up any existing user-related keys
        await foreach (var key in server.KeysAsync(db.Database, "user:*"))
        {
            await db.KeyDeleteAsync(key);
        }

        // Setup services
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddSerilog(Log.Logger));
        services.AddSingleton(redis);  // Register the actual ConnectionMultiplexer instance
        services.AddSingleton<IConnectionMultiplexer>(redis);
        services.AddSingleton<ILogger>(Log.Logger);
        services.AddSingleton<ISensorTemporalBufferService, SensorTemporalBufferService>();
        services.AddSingleton<ISensorDataProvider>(sp => new RedisSensorDataProvider(
            sp.GetRequiredService<ConnectionMultiplexer>(),
            sp.GetRequiredService<ILogger>(),
            sp.GetRequiredService<ISensorTemporalBufferService>(),
            "" // Use empty prefix since our test data doesn't have a prefix
        ));
        services.AddSingleton<IDataStore, SensorDataStore>();
        services.AddSingleton<IActionExecutor, SetValueActionExecutor>();
        services.AddSingleton<IRuleEngine, CompiledRuleEngine>();

        var serviceProvider = services.BuildServiceProvider();
        var ruleEngine = serviceProvider.GetRequiredService<IRuleEngine>();
        var logger = Log.Logger;

        try
        {
            // Clear any existing data
            await db.KeyDeleteAsync("temperature");
            await db.KeyDeleteAsync("humidity");
            await db.KeyDeleteAsync("pressure");
            await db.KeyDeleteAsync("alerts:temperature");
            await db.KeyDeleteAsync("alerts:humidity");
            await db.KeyDeleteAsync("alerts:pressure");
            await db.KeyDeleteAsync("system:status");
            await db.KeyDeleteAsync("converted_temp");

            // Test 1: Temperature Conversion (32°F -> 0°C)
            logger.Information("Test 1: Temperature Conversion");
            await db.StringSetAsync("temperature", "32.0");
            await ruleEngine.ExecuteCycleAsync();
            var convertedTemp = await db.StringGetAsync("converted_temp");
            logger.Information($"32°F converted to {convertedTemp}°C");

            // Test 2: High Temperature Alert
            logger.Information("\nTest 2: High Temperature Alert");
            await db.StringSetAsync("temperature", "80.0");
            await Task.Delay(600); // Wait for temporal condition
            await ruleEngine.ExecuteCycleAsync();
            var tempAlert = await db.StringGetAsync("alerts:temperature");
            var sysStatus = await db.StringGetAsync("system:status");
            logger.Information($"Temperature Alert: {tempAlert}, System Status: {sysStatus}");

            // Test 3: Humidity/Pressure Check
            logger.Information("\nTest 3: Humidity/Pressure Check");
            await db.StringSetAsync("humidity", "85.0");
            await db.StringSetAsync("pressure", "975.0");
            await ruleEngine.ExecuteCycleAsync();
            var humidityAlert = await db.StringGetAsync("alerts:humidity");
            var pressureAlert = await db.StringGetAsync("alerts:pressure");
            sysStatus = await db.StringGetAsync("system:status");
            logger.Information($"Humidity Alert: {humidityAlert}, Pressure Alert: {pressureAlert}, System Status: {sysStatus}");
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error running tests");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
