using Microsoft.Extensions.Logging;

namespace Pulsar.Tests.Mocks
{
    public class SingleNodeConfig
    {
        public required string[] Endpoints { get; set; }
        public int PoolSize { get; set; }
        public int RetryCount { get; set; }
        public int RetryBaseDelayMs { get; set; }
        public int ConnectTimeout { get; set; }
        public int SyncTimeout { get; set; }
        public int KeepAlive { get; set; }
    }

    public class ClusterConfig
    {
        public required string[] Endpoints { get; set; }
        public int PoolSize { get; set; }
        public int RetryCount { get; set; }
        public int RetryBaseDelayMs { get; set; }
        public int ConnectTimeout { get; set; }
        public int SyncTimeout { get; set; }
        public int KeepAlive { get; set; }
    }

    public class HighAvailabilityConfig
    {
        public required string[] Endpoints { get; set; }
        public int PoolSize { get; set; }
        public bool ReplicaOnly { get; set; }
        public int RetryCount { get; set; }
        public int RetryBaseDelayMs { get; set; }
    }

    public class RedisConfiguration
    {
        public SingleNodeConfig? SingleNode { get; set; }
        public ClusterConfig? Cluster { get; set; }
        public HighAvailabilityConfig? HighAvailability { get; set; }
    }

    public interface IRedisService
    {
        Task<T?> GetValue<T>(string key); // Changed to nullable return type
        Task<object?> GetValue(string key); // Changed to nullable return type
        Task SetValue(string key, object value);
        Task SendMessage(string channel, object message);
        Task Subscribe(string channel, Action<string, object> handler);
        Task<Dictionary<string, object>> GetAllInputsAsync();
    }

    public class RedisService : IRedisService
    {
        private readonly Dictionary<string, object> _values = new();
        private readonly Dictionary<string, List<Action<string, object>>> _subscribers = new();

        public RedisService(RedisConfiguration config, ILoggerFactory loggerFactory)
        {
            // In a real implementation, this would connect to Redis
        }

        public Task<T?> GetValue<T>(string key)
        {
            if (_values.TryGetValue(key, out var value) && value is T typedValue)
                return Task.FromResult<T?>(typedValue);

            return Task.FromResult<T?>(default);
        }

        public Task<object?> GetValue(string key)
        {
            if (_values.TryGetValue(key, out var value))
                return Task.FromResult<object?>(value);

            return Task.FromResult<object?>(null);
        }

        public Task SetValue(string key, object value)
        {
            _values[key] = value;
            return Task.CompletedTask;
        }

        public Task SendMessage(string channel, object message)
        {
            if (_subscribers.TryGetValue(channel, out var handlers))
            {
                foreach (var handler in handlers)
                {
                    handler(channel, message);
                }
            }
            return Task.CompletedTask;
        }

        public Task Subscribe(string channel, Action<string, object> handler)
        {
            if (!_subscribers.TryGetValue(channel, out var handlers))
            {
                handlers = new List<Action<string, object>>();
                _subscribers[channel] = handlers;
            }

            handlers.Add(handler);
            return Task.CompletedTask;
        }

        public Task<Dictionary<string, object>> GetAllInputsAsync()
        {
            var result = new Dictionary<string, object>();
            foreach (var kvp in _values)
            {
                if (kvp.Key.StartsWith("input:"))
                {
                    result[kvp.Key] = kvp.Value;
                }
            }
            return Task.FromResult(result);
        }
    }
}
