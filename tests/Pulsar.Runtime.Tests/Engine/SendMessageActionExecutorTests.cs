using System.Threading.Tasks;
using Pulsar.RuleDefinition.Models;
using Pulsar.Runtime.Engine;
using Serilog;
using Xunit;

namespace Pulsar.Runtime.Tests.Engine;

public class SendMessageActionExecutorTests
{
    private readonly SendMessageActionExecutor _executor;
    private readonly ILogger _logger;

    public SendMessageActionExecutorTests()
    {
        _logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateLogger();
        _executor = new SendMessageActionExecutor(_logger);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidAction_SendsMessages()
    {
        // Arrange
        var action = new RuleAction
        {
            SendMessage = new Dictionary<string, string>
            {
                ["email"] = "High temperature warning",
                ["sms"] = "Temperature exceeded threshold"
            }
        };

        // Act
        var result = await _executor.ExecuteAsync(action);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullSendMessage_ReturnsTrue()
    {
        // Arrange
        var action = new RuleAction
        {
            SendMessage = null
        };

        // Act
        var result = await _executor.ExecuteAsync(action);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptySendMessage_ReturnsTrue()
    {
        // Arrange
        var action = new RuleAction
        {
            SendMessage = new Dictionary<string, string>()
        };

        // Act
        var result = await _executor.ExecuteAsync(action);

        // Assert
        Assert.True(result);
    }
}
