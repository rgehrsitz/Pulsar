using System;
using System.Threading.Tasks;
using Beacon.Runtime.Services;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Generated
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .CreateLogger();

            var redisConfig = new RedisConfiguration();
            var redis = new RedisService(redisConfig, logger);
            var coordinator = new RuleCoordinator(redis, logger);

            while (true)
            {
                try
                {
                    await coordinator.EvaluateAllRulesAsync();
                    await Task.Delay(100); // 100ms cycle time
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error in rule evaluation cycle");
                }
            }
        }
    }
}
