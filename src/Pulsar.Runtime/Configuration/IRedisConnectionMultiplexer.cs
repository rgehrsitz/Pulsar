using System.Net;
using StackExchange.Redis;

namespace Pulsar.Runtime.Configuration
{
    public interface IRedisConnectionMultiplexer
    {
        IServer GetServer(string host, int port, object? asyncState = null);
        EndPoint[] GetEndPoints(bool configuredOnly = false);
        void Dispose();
        bool IsConnected { get; }
    }
}
