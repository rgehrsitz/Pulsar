using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Pulsar.Runtime.Engine;
using Pulsar.Runtime.Services;

class Program
{
    static async Task Main()
    {
        // Setup Redis connection
        var redis = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
        var db = redis.GetDatabase();

        // Setup services
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddSingleton<IConnectionMultiplexer>(redis);
        services.AddSingleton<IActionExecutor, RedisActionExecutor>();
        services.AddSingleton<IDataStore, RedisDataStore>();
        services.AddSingleton<IRuleEngine, CompiledRuleEngine>();

        var serviceProvider = services.BuildServiceProvider();
        var ruleEngine = serviceProvider.GetRequiredService<IRuleEngine>();
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

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
            logger.LogInformation("Test 1: Temperature Conversion");
            await db.StringSetAsync("temperature", "32.0");
            await ruleEngine.ExecuteCycleAsync();
            var convertedTemp = await db.StringGetAsync("converted_temp");
            logger.LogInformation($"32°F converted to {convertedTemp}°C");

            // Test 2: High Temperature Alert
            logger.LogInformation("\nTest 2: High Temperature Alert");
            await db.StringSetAsync("temperature", "80.0");
            await Task.Delay(600); // Wait for temporal condition
            await ruleEngine.ExecuteCycleAsync();
            var tempAlert = await db.StringGetAsync("alerts:temperature");
            var sysStatus = await db.StringGetAsync("system:status");
            logger.LogInformation($"Temperature Alert: {tempAlert}, System Status: {sysStatus}");

            // Test 3: Humidity/Pressure Check
            logger.LogInformation("\nTest 3: Humidity/Pressure Check");
            await db.StringSetAsync("humidity", "85.0");
            await db.StringSetAsync("pressure", "975.0");
            await ruleEngine.ExecuteCycleAsync();
            var humidityAlert = await db.StringGetAsync("alerts:humidity");
            var pressureAlert = await db.StringGetAsync("alerts:pressure");
            sysStatus = await db.StringGetAsync("system:status");
            logger.LogInformation($"Humidity Alert: {humidityAlert}, Pressure Alert: {pressureAlert}, System Status: {sysStatus}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error running tests");
        }
    }
}
