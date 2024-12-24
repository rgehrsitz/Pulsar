using StackExchange.Redis;
using System.Net;

namespace Pulsar.Runtime.Configuration
{
    public class RedisConnectionMultiplexer : IRedisConnectionMultiplexer
    {
        private readonly ConnectionMultiplexer _connectionMultiplexer;

        public RedisConnectionMultiplexer(ConnectionMultiplexer connectionMultiplexer)
        {
            _connectionMultiplexer = connectionMultiplexer;
        }

        public IServer GetServer(string host, int port, object? asyncState = null)
        {
            return _connectionMultiplexer.GetServer(host, port, asyncState);
        }

        public EndPoint[] GetEndPoints(bool configuredOnly = false)
        {
            return _connectionMultiplexer.GetEndPoints(configuredOnly);
        }

        public void Dispose()
        {
            _connectionMultiplexer.Dispose();
        }

        public bool IsConnected => _connectionMultiplexer.IsConnected;
    }
}
