using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pulsar.Runtime.Engine;
using Pulsar.Runtime.Storage;
using Pulsar.Models.Actions;
using Serilog;
using StackExchange.Redis;

namespace Pulsar.IntegrationTests.Helpers
{
    public class TestRuleEngine : IRuleEngine
    {
        private readonly IDataStore _dataStore;
        private readonly IActionExecutor _actionExecutor;
        private readonly ILogger _logger;
        private readonly IDatabase _db;

        public TestRuleEngine(
            IDataStore dataStore,
            IActionExecutor actionExecutor,
            ILogger logger,
            RedisTestContainer container)
        {
            _dataStore = dataStore;
            _actionExecutor = actionExecutor;
            _logger = logger.ForContext<TestRuleEngine>();
            _db = container.GetDatabase();
        }

        public async Task ExecuteCycleAsync()
        {
            // Check alerts
            var tempAlert = await _dataStore.GetValueAsync("alerts:temperature");
            var humidityAlert = await _dataStore.GetValueAsync("alerts:humidity");
            var pressureAlert = await _dataStore.GetValueAsync("alerts:pressure");

            _logger.Information(
                "Alert values - Temperature: {TempAlert}, Humidity: {HumidityAlert}, Pressure: {PressureAlert}",
                tempAlert,
                humidityAlert,
                pressureAlert
            );

            if ((tempAlert?.ToString() == "1") || 
                (humidityAlert?.ToString() == "1") || 
                (pressureAlert?.ToString() == "1"))
            {
                _logger.Information("Setting system status to alert (1.0)");
                await _dataStore.SetValueAsync("system:status", 1.0);
            }
            else
            {
                _logger.Information("Setting system status to normal (0.0)");
                await _dataStore.SetValueAsync("system:status", 0.0);
            }
        }
    }
}
