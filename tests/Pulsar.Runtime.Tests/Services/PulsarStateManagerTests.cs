using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Pulsar.Runtime.Engine;
using Pulsar.Runtime.Services;
using Pulsar.Runtime.Tests.Mocks;
using Serilog;
using Xunit;

namespace Pulsar.Runtime.Tests.Services;

public class PulsarStateManagerTests : IDisposable
{
    private readonly Mock<ILogger> _logger;
    private readonly Mock<RuleEngine> _ruleEngine;
    private readonly MockRedisClusterConfiguration _redisConfig;
    private readonly PulsarStateManager _stateManager;
    private readonly string _currentHostname;
    private readonly CancellationTokenSource _cts;

    public PulsarStateManagerTests()
    {
        _logger = new Mock<ILogger>();
        _logger.Setup(l => l.ForContext<It.IsAnyType>()).Returns(_logger.Object);

        _ruleEngine = new Mock<RuleEngine>(MockBehavior.Strict);
        _ruleEngine.Setup(r => r.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _ruleEngine.Setup(r => r.StopAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        _currentHostname = "test-host";
        _redisConfig = new MockRedisClusterConfiguration(_logger.Object, _currentHostname);
        
        // Use a short check interval for faster tests
        _stateManager = new PulsarStateManager(
            _logger.Object,
            _redisConfig,
            _ruleEngine.Object,
            TimeSpan.FromMilliseconds(100));

        _cts = new CancellationTokenSource();
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }

    [Fact]
    public async Task StartsInactive_WhenMasterIsOnDifferentHost()
    {
        // Arrange - MockRedisClusterConfiguration defaults to master on "other-host"
        
        // Act
        var task = _stateManager.StartAsync(_cts.Token);
        await Task.Delay(200); // Allow time for first state check

        // Assert
        Assert.False(_stateManager.IsActive);
        _ruleEngine.Verify(r => r.StartAsync(It.IsAny<CancellationToken>()), Times.Never);
        _ruleEngine.Verify(r => r.StopAsync(It.IsAny<CancellationToken>()), Times.Never);

        await _stateManager.StopAsync(_cts.Token);
    }

    [Fact]
    public async Task BecomesActive_WhenFailoverToCurrentHost()
    {
        // Arrange
        var task = _stateManager.StartAsync(_cts.Token);
        await Task.Delay(200); // Allow time for first state check
        Assert.False(_stateManager.IsActive);

        // Act
        _redisConfig.SimulateFailover(_currentHostname);
        await Task.Delay(200); // Allow time for state check

        // Assert
        Assert.True(_stateManager.IsActive);
        _ruleEngine.Verify(r => r.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
        _ruleEngine.Verify(r => r.StopAsync(It.IsAny<CancellationToken>()), Times.Never);

        await _stateManager.StopAsync(_cts.Token);
    }

    [Fact]
    public async Task BecomesInactive_WhenConnectionFails()
    {
        // Arrange
        _redisConfig.SimulateFailover(_currentHostname);
        var task = _stateManager.StartAsync(_cts.Token);
        await Task.Delay(200); // Allow time for first state check
        Assert.True(_stateManager.IsActive);

        // Act
        _redisConfig.SimulateConnectionFailure();
        await Task.Delay(200); // Allow time for state check

        // Assert
        Assert.False(_stateManager.IsActive);
        _ruleEngine.Verify(r => r.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
        _ruleEngine.Verify(r => r.StopAsync(It.IsAny<CancellationToken>()), Times.Once);

        await _stateManager.StopAsync(_cts.Token);
    }

    [Fact]
    public async Task HandlesMultipleFailovers()
    {
        // Arrange
        var task = _stateManager.StartAsync(_cts.Token);
        await Task.Delay(200); // Initial state check
        Assert.False(_stateManager.IsActive);

        // Act & Assert - First failover to current host
        _redisConfig.SimulateFailover(_currentHostname);
        await Task.Delay(200);
        Assert.True(_stateManager.IsActive);
        _ruleEngine.Verify(r => r.StartAsync(It.IsAny<CancellationToken>()), Times.Once);

        // Act & Assert - Failover to different host
        _redisConfig.SimulateFailover("other-host-2");
        await Task.Delay(200);
        Assert.False(_stateManager.IsActive);
        _ruleEngine.Verify(r => r.StopAsync(It.IsAny<CancellationToken>()), Times.Once);

        // Act & Assert - Failover back to current host
        _redisConfig.SimulateFailover(_currentHostname);
        await Task.Delay(200);
        Assert.True(_stateManager.IsActive);
        _ruleEngine.Verify(r => r.StartAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));

        await _stateManager.StopAsync(_cts.Token);
    }

    [Fact]
    public async Task RecoversFromConnectionFailure()
    {
        // Arrange
        _redisConfig.SimulateFailover(_currentHostname);
        var task = _stateManager.StartAsync(_cts.Token);
        await Task.Delay(200);
        Assert.True(_stateManager.IsActive);

        // Act & Assert - Connection failure
        _redisConfig.SimulateConnectionFailure();
        await Task.Delay(200);
        Assert.False(_stateManager.IsActive);
        _ruleEngine.Verify(r => r.StopAsync(It.IsAny<CancellationToken>()), Times.Once);

        // Act & Assert - Connection restored
        _redisConfig.SimulateConnectionRestoration();
        await Task.Delay(200);
        Assert.True(_stateManager.IsActive);
        _ruleEngine.Verify(r => r.StartAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));

        await _stateManager.StopAsync(_cts.Token);
    }
}
