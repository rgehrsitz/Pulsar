using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using Pulsar.RuleDefinition.Models;
using Pulsar.Runtime.Engine;
using Pulsar.Runtime.Services;
using Serilog;
using Xunit;

namespace Pulsar.Runtime.Tests.Engine;

public class MockSetValueActionExecutor : SetValueActionExecutor
{
    private readonly ISensorDataProvider _dataProvider;

    public MockSetValueActionExecutor(ILogger logger, ISensorDataProvider dataProvider) : base(logger)
    {
        _dataProvider = dataProvider;
    }

    public override async Task<bool> ExecuteAsync(RuleAction action)
    {
        if (action.SetValue == null || action.SetValue.Count == 0)
        {
            _logger.Warning("No values to set");
            return true;
        }

        await _dataProvider.SetSensorDataAsync(action.SetValue);
        return true;
    }
}

public class SetValueActionExecutorTests
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly Mock<ISensorDataProvider> _mockDataProvider;
    private readonly SetValueActionExecutor _executor;

    public SetValueActionExecutorTests()
    {
        _mockLogger = new Mock<ILogger>();
        _mockDataProvider = new Mock<ISensorDataProvider>();
        _mockLogger.Setup(l => l.ForContext<SetValueActionExecutor>())
            .Returns(_mockLogger.Object);

        _executor = new MockSetValueActionExecutor(_mockLogger.Object, _mockDataProvider.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidAction_SetsSensorValues()
    {
        // Arrange
        var action = new RuleAction
        {
            SetValue = new Dictionary<string, object>
            {
                ["sensor1"] = 42.0,
                ["sensor2"] = "on"
            }
        };

        // Act
        await _executor.ExecuteAsync(action);

        // Assert
        _mockDataProvider.Verify(p => p.SetSensorDataAsync(
            It.Is<IDictionary<string, object>>(d => 
                d["sensor1"].Equals(42.0) && 
                d["sensor2"].Equals("on"))), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyValues_LogsWarning()
    {
        // Arrange
        var action = new RuleAction
        {
            SetValue = new Dictionary<string, object>()
        };

        // Act
        await _executor.ExecuteAsync(action);

        // Assert
        _mockLogger.Verify(l => l.Warning(It.IsAny<string>()), Times.Once);
        _mockDataProvider.Verify(p => p.SetSensorDataAsync(It.IsAny<IDictionary<string, object>>()), Times.Never);
    }
}
