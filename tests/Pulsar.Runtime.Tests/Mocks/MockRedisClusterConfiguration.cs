using System;
using System.Threading;
using Pulsar.Runtime.Configuration;
using Serilog;
using StackExchange.Redis;

namespace Pulsar.Runtime.Tests.Mocks;

/// <summary>
/// Mock Redis configuration for testing state management and failover scenarios
/// </summary>
public class MockRedisClusterConfiguration : RedisClusterConfiguration
{
    private readonly ILogger _logger;
    private string _currentMasterHost;
    private bool _isConnected;
    private readonly string _currentHostname;

    public MockRedisClusterConfiguration(
        ILogger logger,
        string currentHostname,
        string initialMasterHost = "other-host") 
        : base(logger, "test-master", new[] { "localhost:26379" }, currentHostname)
    {
        _logger = logger;
        _currentHostname = currentHostname;
        _currentMasterHost = initialMasterHost;
        _isConnected = true;
    }

    public override bool ShouldPulsarBeActive()
    {
        if (!_isConnected)
        {
            _logger.Information("Redis connection is down, Pulsar should not be active");
            return false;
        }

        var shouldBeActive = _currentMasterHost.Equals(_currentHostname, StringComparison.OrdinalIgnoreCase);
        _logger.Information(
            "Checking if Pulsar should be active. Master: {Master}, Current: {Current}, Result: {Result}", 
            _currentMasterHost, 
            _currentHostname, 
            shouldBeActive);
            
        return shouldBeActive;
    }

    /// <summary>
    /// Simulates a Redis master failover to a new host
    /// </summary>
    public void SimulateFailover(string newMasterHost)
    {
        _logger.Information("Simulating failover from {OldMaster} to {NewMaster}", _currentMasterHost, newMasterHost);
        _currentMasterHost = newMasterHost;
    }

    /// <summary>
    /// Simulates a Redis connection failure
    /// </summary>
    public void SimulateConnectionFailure()
    {
        _logger.Information("Simulating Redis connection failure");
        _isConnected = false;
    }

    /// <summary>
    /// Simulates a Redis connection restoration
    /// </summary>
    public void SimulateConnectionRestoration()
    {
        _logger.Information("Simulating Redis connection restoration");
        _isConnected = true;
    }
}
