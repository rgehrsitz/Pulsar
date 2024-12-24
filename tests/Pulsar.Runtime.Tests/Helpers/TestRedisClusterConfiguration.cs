using System;
using NRedisStack;
using Pulsar.Runtime.Configuration;
using Serilog;
using StackExchange.Redis;
using Moq;

namespace Pulsar.Runtime.Tests.Helpers
{
    public class TestRedisClusterConfiguration : RedisClusterConfiguration
    {
        private readonly ILogger _logger;
        private readonly Mock<IRedisConnectionMultiplexer> _mockConnectionMultiplexer;
        private readonly TestRedisServer _testServer;

        public TestRedisClusterConfiguration(
            ILogger logger,
            string masterName,
            string[] sentinelHosts,
            string currentHostname,
            Mock<IRedisConnectionMultiplexer> mockConnectionMultiplexer
        )
            : base(logger, masterName, sentinelHosts, currentHostname)
        {
            _logger = logger;
            _mockConnectionMultiplexer = mockConnectionMultiplexer;
            _testServer = new TestRedisServer();
        }

        public override IRedisConnectionMultiplexer GetConnection()
        {
            return _mockConnectionMultiplexer.Object;
        }

        public override string GetCurrentMaster()
        {
            try
            {
                // Ensure we never return null
                return base.GetCurrentMaster() ?? "localhost:6379";
            }
            catch
            {
                return "localhost:6379";
            }
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
