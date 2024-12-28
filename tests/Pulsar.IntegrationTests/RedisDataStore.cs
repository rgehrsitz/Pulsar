using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pulsar.Runtime.Engine;
using Pulsar.Models.Actions;
using StackExchange.Redis;
using Serilog;

namespace Pulsar.IntegrationTests
{
    public class RedisDataStore : IDataStore
    {
        private readonly ILogger _logger;
        private readonly IConnectionMultiplexer _redis;
        private readonly IDatabase _db;
        private readonly Dictionary<string, Queue<double>> _historicalData;

        public RedisDataStore(ILogger logger, IConnectionMultiplexer redis)
        {
            _logger = logger;
            _redis = redis;
            _db = redis.GetDatabase();
            _historicalData = new Dictionary<string, Queue<double>>();
        }

        public async Task<IDictionary<string, double>> GetCurrentDataAsync()
        {
            var result = new Dictionary<string, double>();
            var keys = new[] { "temperature", "humidity", "pressure", "alerts:temperature", 
                "alerts:humidity", "alerts:pressure", "system:status", "converted_temp" };

            foreach (var key in keys)
            {
                var value = await _db.StringGetAsync(key);
                if (value.HasValue && double.TryParse(value.ToString(), out double numValue))
                {
                    result[key] = numValue;
                }
            }

            return result;
        }

        public async Task<bool> CheckThresholdOverTimeAsync(string sensor, double threshold, TimeSpan duration)
        {
            var value = await _db.StringGetAsync(sensor);
            if (!value.HasValue || !double.TryParse(value.ToString(), out double currentValue))
            {
                return false;
            }

            if (!_historicalData.ContainsKey(sensor))
            {
                _historicalData[sensor] = new Queue<double>();
            }

            var history = _historicalData[sensor];
            history.Enqueue(currentValue);

            // Keep only values within the duration window
            while (history.Count > 5) // Simplified for testing - assuming 100ms cycles
            {
                history.Dequeue();
            }

            // Check if all values in history exceed threshold
            return history.Count > 0 && history.All(v => v > threshold);
        }
    }

    public class ActionExecutor : IActionExecutor
    {
        private readonly ILogger _logger;
        private readonly IDatabase _db;

        public ActionExecutor(ILogger logger, IConnectionMultiplexer redis)
        {
            _logger = logger;
            _db = redis.GetDatabase();
        }

        public async Task<bool> ExecuteAsync(CompiledRuleAction action)
        {
            if (action.SetValue != null)
            {
                _logger.Information("Setting value {Key} to {Value}", action.SetValue.Key, action.SetValue.Value);
                await _db.StringSetAsync(action.SetValue.Key, action.SetValue.Value.ToString());
                return true;
            }
            else if (action.SendMessage != null)
            {
                _logger.Information("Sending message to {Channel}: {Message}", 
                    action.SendMessage.Channel, action.SendMessage.Message);
                return true;
            }

            return false;
        }

        public Task ExecutePendingActionsAsync()
        {
            return Task.CompletedTask;
        }
    }
}
