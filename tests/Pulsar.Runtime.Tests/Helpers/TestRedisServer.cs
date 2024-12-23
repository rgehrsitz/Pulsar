using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace Pulsar.Runtime.Tests.Helpers
{
    public class TestRedisServer
    {
        private readonly Dictionary<RedisKey, RedisValue> _data = new();
        private readonly object _lock = new();
        private bool _isConnected = true;
        private bool _isMaster = true;

        public bool IsConnected => _isConnected;
        public bool IsMaster => _isMaster;
        public EndPoint Endpoint { get; } = new System.Net.IPEndPoint(
            System.Net.IPAddress.Parse("127.0.0.1"),
            6379
        );

        public void SetMaster(bool isMaster) => _isMaster = isMaster;

        internal Task<RedisValue> StringGetAsync(RedisKey key)
        {
            lock (_lock)
            {
                return Task.FromResult(_data.TryGetValue(key, out var value) ? value : RedisValue.Null);
            }
        }

        internal Task StringSetAsync(RedisKey key, RedisValue value)
        {
            lock (_lock)
            {
                _data[key] = value;
            }
            return Task.CompletedTask;
        }

        public void Clear()
        {
            lock (_lock)
            {
                _data.Clear();
            }
        }
    }
}
