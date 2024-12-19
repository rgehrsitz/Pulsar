using System.Threading.Tasks;
using Pulsar.RuleDefinition.Models;
using Pulsar.Runtime.Engine;
using Serilog;
using Xunit;

namespace Pulsar.Runtime.Tests.Engine;

public class SetValueActionExecutorTests
{
    private readonly SetValueActionExecutor _executor;
    private readonly ILogger _logger;

    public SetValueActionExecutorTests()
    {
        _logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateLogger();
        _executor = new SetValueActionExecutor(_logger);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidAction_QueuesPendingUpdates()
    {
        // Arrange
        var action = new RuleAction
        {
            SetValue = new Dictionary<string, object>
            {
                ["temperature_threshold"] = 25.0,
                ["humidity_warning"] = true
            }
        };

        // Act
        var result = await _executor.ExecuteAsync(action);
        var pendingUpdates = _executor.GetPendingUpdates();

        // Assert
        Assert.True(result);
        Assert.Equal(2, pendingUpdates.Count);
        Assert.Equal(25.0, pendingUpdates["temperature_threshold"]);
        Assert.True((bool)pendingUpdates["humidity_warning"]);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullSetValue_ReturnsTrue()
    {
        // Arrange
        var action = new RuleAction
        {
            SetValue = null
        };

        // Act
        var result = await _executor.ExecuteAsync(action);
        var pendingUpdates = _executor.GetPendingUpdates();

        // Assert
        Assert.True(result);
        Assert.Empty(pendingUpdates);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptySetValue_ReturnsTrue()
    {
        // Arrange
        var action = new RuleAction
        {
            SetValue = new Dictionary<string, object>()
        };

        // Act
        var result = await _executor.ExecuteAsync(action);
        var pendingUpdates = _executor.GetPendingUpdates();

        // Assert
        Assert.True(result);
        Assert.Empty(pendingUpdates);
    }

    [Fact]
    public async Task GetAndClearPendingUpdates_ClearsPendingUpdates()
    {
        // Arrange
        var action = new RuleAction
        {
            SetValue = new Dictionary<string, object>
            {
                ["temperature_threshold"] = 25.0
            }
        };
        await _executor.ExecuteAsync(action);

        // Act
        var updates = _executor.GetAndClearPendingUpdates();
        var remainingUpdates = _executor.GetPendingUpdates();

        // Assert
        Assert.Single(updates);
        Assert.Equal(25.0, updates["temperature_threshold"]);
        Assert.Empty(remainingUpdates);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleActions_AccumulatesUpdates()
    {
        // Arrange
        var action1 = new RuleAction
        {
            SetValue = new Dictionary<string, object>
            {
                ["temperature_threshold"] = 25.0
            }
        };
        var action2 = new RuleAction
        {
            SetValue = new Dictionary<string, object>
            {
                ["humidity_warning"] = true
            }
        };

        // Act
        await _executor.ExecuteAsync(action1);
        await _executor.ExecuteAsync(action2);
        var pendingUpdates = _executor.GetPendingUpdates();

        // Assert
        Assert.Equal(2, pendingUpdates.Count);
        Assert.Equal(25.0, pendingUpdates["temperature_threshold"]);
        Assert.True((bool)pendingUpdates["humidity_warning"]);
    }
}
