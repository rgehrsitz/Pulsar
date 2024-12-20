using System;
using System.Threading.Tasks;
using Moq;
using Xunit;

namespace Pulsar.Runtime.Tests.Integration;

public class RedisFailoverTests : RedisIntegrationTestBase
{
    [Fact]
    public async Task StartsInactive_WhenMasterIsOnDifferentHost()
    {
        // Arrange - MockRedisClusterConfiguration defaults to master on "other-host"
        
        // Act
        await StateManager.StartAsync(default);
        await Task.Delay(200); // Allow time for first state check

        // Assert
        Assert.False(StateManager.IsActive);
        RuleEngine.Verify(r => r.StartAsync(It.IsAny<CancellationToken>()), Times.Never);
        RuleEngine.Verify(r => r.StopAsync(It.IsAny<CancellationToken>()), Times.Never);

        await StateManager.StopAsync(default);
    }

    [Fact]
    public async Task BecomesActive_WhenFailoverToCurrentHost()
    {
        // Arrange
        await StateManager.StartAsync(default);
        await Task.Delay(200); // Allow time for first state check
        Assert.False(StateManager.IsActive);

        // Act
        RedisConfig.SimulateFailover(Environment.MachineName);
        await Task.Delay(200); // Allow time for state check

        // Assert
        Assert.True(StateManager.IsActive);
        RuleEngine.Verify(r => r.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
        RuleEngine.Verify(r => r.StopAsync(It.IsAny<CancellationToken>()), Times.Never);

        await StateManager.StopAsync(default);
    }

    [Fact]
    public async Task BecomesInactive_WhenConnectionFails()
    {
        // Arrange
        RedisConfig.SimulateFailover(Environment.MachineName);
        await StateManager.StartAsync(default);
        await Task.Delay(200); // Allow time for first state check
        Assert.True(StateManager.IsActive);

        // Act
        RedisConfig.SimulateConnectionFailure();
        await Task.Delay(200); // Allow time for state check

        // Assert
        Assert.False(StateManager.IsActive);
        RuleEngine.Verify(r => r.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
        RuleEngine.Verify(r => r.StopAsync(It.IsAny<CancellationToken>()), Times.Once);

        await StateManager.StopAsync(default);
    }

    [Fact]
    public async Task HandlesMultipleFailovers()
    {
        // Arrange
        await StateManager.StartAsync(default);
        await Task.Delay(200); // Initial state check
        Assert.False(StateManager.IsActive);

        // Act & Assert - First failover to current host
        RedisConfig.SimulateFailover(Environment.MachineName);
        await Task.Delay(200);
        Assert.True(StateManager.IsActive);
        RuleEngine.Verify(r => r.StartAsync(It.IsAny<CancellationToken>()), Times.Once);

        // Act & Assert - Failover to different host
        RedisConfig.SimulateFailover("other-host-2");
        await Task.Delay(200);
        Assert.False(StateManager.IsActive);
        RuleEngine.Verify(r => r.StopAsync(It.IsAny<CancellationToken>()), Times.Once);

        // Act & Assert - Failover back to current host
        RedisConfig.SimulateFailover(Environment.MachineName);
        await Task.Delay(200);
        Assert.True(StateManager.IsActive);
        RuleEngine.Verify(r => r.StartAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));

        await StateManager.StopAsync(default);
    }

    [Fact]
    public async Task RecoversFromConnectionFailure()
    {
        // Arrange
        RedisConfig.SimulateFailover(Environment.MachineName);
        await StateManager.StartAsync(default);
        await Task.Delay(200);
        Assert.True(StateManager.IsActive);

        // Act & Assert - Connection failure
        RedisConfig.SimulateConnectionFailure();
        await Task.Delay(200);
        Assert.False(StateManager.IsActive);
        RuleEngine.Verify(r => r.StopAsync(It.IsAny<CancellationToken>()), Times.Once);

        // Act & Assert - Connection restored
        RedisConfig.SimulateConnectionRestoration();
        await Task.Delay(200);
        Assert.True(StateManager.IsActive);
        RuleEngine.Verify(r => r.StartAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));

        await StateManager.StopAsync(default);
    }
}
