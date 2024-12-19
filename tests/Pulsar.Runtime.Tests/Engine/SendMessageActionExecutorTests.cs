using System.Threading.Tasks;
using Moq;
using Pulsar.RuleDefinition.Models;
using Pulsar.Runtime.Engine;
using Pulsar.Runtime.Services;
using Serilog;
using Xunit;

namespace Pulsar.Runtime.Tests.Engine;

public class MockSendMessageActionExecutor : SendMessageActionExecutor
{
    private readonly IMessageSender _messageSender;

    public MockSendMessageActionExecutor(ILogger logger, IMessageSender messageSender) : base(logger)
    {
        _messageSender = messageSender;
    }

    public override async Task<bool> ExecuteAsync(RuleAction action)
    {
        if (action.SendMessage == null || action.SendMessage.Count == 0)
        {
            return true;
        }

        foreach (var (channel, message) in action.SendMessage)
        {
            await _messageSender.SendMessageAsync(channel, message);
        }

        return true;
    }
}

public class SendMessageActionExecutorTests
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly Mock<IMessageSender> _mockMessageSender;
    private readonly SendMessageActionExecutor _executor;

    public SendMessageActionExecutorTests()
    {
        _mockLogger = new Mock<ILogger>();
        _mockMessageSender = new Mock<IMessageSender>();
        _mockLogger.Setup(l => l.ForContext<SendMessageActionExecutor>())
            .Returns(_mockLogger.Object);

        _executor = new MockSendMessageActionExecutor(_mockLogger.Object, _mockMessageSender.Object);
    }

    [Fact]
    public async Task ExecuteAsync_SendsMessageWithCorrectContent()
    {
        // Arrange
        var action = new RuleAction
        {
            SendMessage = new Dictionary<string, string>
            {
                ["test-channel"] = "Test message"
            }
        };

        // Act
        await _executor.ExecuteAsync(action);

        // Assert
        _mockMessageSender.Verify(m => m.SendMessageAsync(
            It.Is<string>(s => s == "test-channel"),
            It.Is<string>(s => s == "Test message")), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullMessage_LogsWarning()
    {
        // Arrange
        var action = new RuleAction
        {
            SendMessage = new Dictionary<string, string>()
        };

        // Act
        await _executor.ExecuteAsync(action);

        // Assert
        _mockLogger.Verify(l => l.Warning(
            It.IsAny<string>(),
            It.Is<string>(s => s == "test-channel")), Times.Never);
        _mockMessageSender.Verify(m => m.SendMessageAsync(
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Never);
    }
}
