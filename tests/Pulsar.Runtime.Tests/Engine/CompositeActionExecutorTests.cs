using System.Collections.Concurrent;
using System.Threading.Tasks;
using Pulsar.RuleDefinition.Models;
using Pulsar.Runtime.Engine;
using Serilog;
using Xunit;

namespace Pulsar.Runtime.Tests.Engine;

public class CompositeActionExecutorTests
{
    private readonly CompositeActionExecutor _executor;
    private readonly ILogger _logger;

    public CompositeActionExecutorTests()
    {
        _logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateLogger();

        var executors = new Dictionary<string, IActionExecutor>
        {
            ["setValue"] = new SetValueActionExecutor(_logger),
            ["sendMessage"] = new SendMessageActionExecutor(_logger)
        };

        _executor = new CompositeActionExecutor(executors, _logger);
    }

    [Fact]
    public async Task ExecuteAsync_WithBothActionTypes_ExecutesBoth()
    {
        // Arrange
        var action = new RuleAction
        {
            SetValue = new Dictionary<string, object>
            {
                ["temperature_threshold"] = 25.0
            },
            SendMessage = new Dictionary<string, string>
            {
                ["email"] = "Temperature threshold updated"
            }
        };

        // Act
        var result = await _executor.ExecuteAsync(action);
        var pendingUpdates = _executor.GetPendingUpdates();

        // Assert
        Assert.True(result);
        Assert.Single(pendingUpdates);
        Assert.Equal(25.0, pendingUpdates["temperature_threshold"]);
    }

    [Fact]
    public async Task ExecuteAsync_WithSetValueOnly_ExecutesSetValue()
    {
        // Arrange
        var action = new RuleAction
        {
            SetValue = new Dictionary<string, object>
            {
                ["temperature_threshold"] = 25.0
            }
        };

        // Act
        var result = await _executor.ExecuteAsync(action);
        var pendingUpdates = _executor.GetPendingUpdates();

        // Assert
        Assert.True(result);
        Assert.Single(pendingUpdates);
        Assert.Equal(25.0, pendingUpdates["temperature_threshold"]);
    }

    [Fact]
    public async Task ExecuteAsync_WithSendMessageOnly_ExecutesSendMessage()
    {
        // Arrange
        var action = new RuleAction
        {
            SendMessage = new Dictionary<string, string>
            {
                ["email"] = "Test message"
            }
        };

        // Act
        var result = await _executor.ExecuteAsync(action);
        var pendingUpdates = _executor.GetPendingUpdates();

        // Assert
        Assert.True(result);
        Assert.Empty(pendingUpdates);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoActions_ReturnsFalse()
    {
        // Arrange
        var action = new RuleAction();

        // Act
        var result = await _executor.ExecuteAsync(action);
        var pendingUpdates = _executor.GetPendingUpdates();

        // Assert
        Assert.False(result);
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
