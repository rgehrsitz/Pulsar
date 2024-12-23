using System;
using Moq;
using Serilog;
using Pulsar.Runtime.Configuration;

namespace Pulsar.Runtime.Tests.Mocks;

/// <summary>
/// Mock Redis configuration for testing state management and failover scenarios
/// </summary>
public class MockRedisClusterConfiguration : RedisClusterConfiguration
{
    private string _currentMaster = "other-host:6379";
    private bool _isConnected = true;

    public MockRedisClusterConfiguration(ILogger? logger = null)
        : base(
            logger ?? new Mock<ILogger>().Object,
            "master",
            new[] { "localhost:26379" },
            Environment.MachineName
        )
    { }

    public MockRedisClusterConfiguration(ILogger logger, string currentHostname)
        : base(
            logger,
            "master",
            new[] { "localhost:26379" },
            currentHostname
        )
    { }

    public override string GetCurrentMaster()
    {
        if (!_isConnected)
            throw new InvalidOperationException("Connection failed");
        return _currentMaster;
    }

    public void SimulateFailover(string newMaster)
    {
        _currentMaster = $"{newMaster}:6379";
    }

    public void SimulateConnectionFailure()
    {
        _isConnected = false;
    }

    public void SimulateConnectionRestoration()
    {
        _isConnected = true;
    }
}
