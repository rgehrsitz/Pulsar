using System;
using NRedisStack;
using Pulsar.Runtime.Configuration;
using Serilog;
using StackExchange.Redis;

namespace Pulsar.Runtime.Tests.Helpers
{
    public class TestRedisClusterConfiguration : RedisClusterConfiguration
    {
        private readonly ILogger _logger;
        private readonly ConnectionMultiplexer _mockConnection;
        private readonly TestRedisServer _testServer;

        public TestRedisClusterConfiguration(
            ILogger logger,
            string masterName,
            string[] sentinelHosts,
            string currentHostname,
            ConnectionMultiplexer mockConnection
        )
            : base(logger, masterName, sentinelHosts, currentHostname)
        {
            _logger = logger;
            _mockConnection = mockConnection;
            _testServer = new TestRedisServer();
        }

        public override ConnectionMultiplexer GetConnection()
        {
            return _mockConnection;
        }

        public override string GetCurrentMaster()
        {
            return _testServer.IsMaster ? _testServer.Endpoint.ToString() : "otherslave:6379";
        }

        public void SimulateFailover()
        {
            _testServer.SetMaster(false);
        }

        public void SimulateRecovery()
        {
            _testServer.SetMaster(true);
        }
    }
}
