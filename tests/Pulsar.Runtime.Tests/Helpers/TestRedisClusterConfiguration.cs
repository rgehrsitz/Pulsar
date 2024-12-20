using System;
using System.Threading.Tasks;
using StackExchange.Redis;
using Serilog;
using Pulsar.Runtime.Configuration;

namespace Pulsar.Runtime.Tests.Helpers;

public class TestRedisClusterConfiguration : RedisClusterConfiguration
{
    private readonly TestRedisServer _testServer;
    private readonly IConnectionMultiplexer _mockMultiplexer;

    public TestRedisClusterConfiguration(
        ILogger logger,
        string masterName,
        string[] sentinelHosts,
        string currentHostname,
        TestRedisServer testServer,
        IConnectionMultiplexer mockMultiplexer)
        : base(logger, masterName, sentinelHosts, currentHostname)
    {
        _testServer = testServer;
        _mockMultiplexer = mockMultiplexer;
    }

    public override IConnectionMultiplexer GetConnection()
    {
        return _mockMultiplexer;
    }

    public override string GetCurrentMaster()
    {
        return _testServer.IsMaster ? _testServer.Endpoint : "otherslave:6379";
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
