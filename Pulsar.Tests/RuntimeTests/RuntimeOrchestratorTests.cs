// File: Pulsar.Tests/RuntimeTests/RuntimeOrchestratorTests.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Moq;
using Pulsar.Compiler.Models;
using Pulsar.Runtime;
using Pulsar.Runtime.Services;
using StackExchange.Redis;
using Serilog;
using Xunit;
using Xunit.Abstractions;
using Pulsar.Runtime.Rules;

namespace Pulsar.Tests.Runtime;

public class RuntimeOrchestratorTests : IDisposable
{
    private readonly Mock<IRedisService> _redisMock;
    private readonly Mock<ILogger> _loggerMock;
    private readonly RuntimeOrchestrator _orchestrator;
    private readonly string[] _testSensors;
    private readonly ITestOutputHelper _output;

    private class TestRuleCoordinator : IRuleCoordinator
    {
        private readonly ILogger _logger;
        private readonly Action<Dictionary<string, double>, Dictionary<string, double>> _evaluateAction;

        public TestRuleCoordinator(ILogger logger, Action<Dictionary<string, double>, Dictionary<string, double>>? evaluateAction = null)
        {
            _logger = logger;
            _evaluateAction = evaluateAction ?? DefaultEvaluateAction;
        }

        private void DefaultEvaluateAction(Dictionary<string, double> inputs, Dictionary<string, double> outputs)
        {
            if (inputs.TryGetValue("sensor1", out var value))
            {
                outputs["output1"] = value * 2;
            }
        }

        public void EvaluateRules(Dictionary<string, double> inputs, Dictionary<string, double> outputs)
        {
            try
            {
                _logger.Debug("Evaluating rules in TestRuleCoordinator");
                _evaluateAction(inputs, outputs);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error evaluating rules in TestRuleCoordinator");
                throw;
            }
        }
    }

    public RuntimeOrchestratorTests(ITestOutputHelper output)
    {
        _output = output;
        _redisMock = new Mock<IRedisService>();

        // Create logger mock for Serilog's fluent interface.
        _loggerMock = new Mock<ILogger>();
        _loggerMock
            .Setup(x => x.Information(It.IsAny<string>(), It.IsAny<object[]>()))
            .Callback<string, object[]>((msg, args) =>
            {
                // If you want to do anything special here, place it in the callback
            });

        _loggerMock
            .Setup(x => x.Warning(It.IsAny<string>(), It.IsAny<object[]>()))
            .Callback<string, object[]>((msg, args) =>
            {
                // Same idea as above
            });

        _loggerMock
            .Setup(x => x.Error(It.IsAny<Exception>(), It.IsAny<string>(), It.IsAny<object[]>()))
            .Callback<Exception, string, object[]>((ex, msg, args) =>
            {
                // Handle error logs here
            });

        _loggerMock
            .Setup(x => x.Fatal(It.IsAny<Exception>(), It.IsAny<string>(), It.IsAny<object[]>()))
            .Callback<Exception, string, object[]>((ex, msg, args) =>
            {
                // Handle fatal logs here
            });

        _testSensors = new[] { "sensor1", "sensor2" };

        _orchestrator = new RuntimeOrchestrator(
            _redisMock.Object,
            _loggerMock.Object,
            _testSensors,
            TimeSpan.FromMilliseconds(100));
    }

    private static List<GeneratedFileInfo> CreateSourceFiles(string code)
    {
        return new List<GeneratedFileInfo>
        {
            new GeneratedFileInfo
            {
                FileName = "CompiledRules.cs",
                FilePath = "Generated/CompiledRules.cs",
                Content = code,
                Namespace = "Pulsar.Generated"
            }
        };
    }

    public void Dispose()
    {
        // Update Dispose to clean up only what's needed
        try
        {
            _orchestrator?.Dispose();
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Warning: Cleanup failed: {ex.Message}");
        }
    }

    [Fact]
    public async Task ExecuteCycleAsync_ProcessesInputsAndOutputsCorrectly()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var inputs = new Dictionary<string, (double, DateTime)>
    {
        { "sensor1", (42.0, timestamp) },
        { "sensor2", (24.0, timestamp) }
    };

        _redisMock.Setup(x => x.GetSensorValuesAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(inputs);

        Dictionary<string, double>? capturedOutputs = null;
        _redisMock.Setup(x => x.SetOutputValuesAsync(It.IsAny<Dictionary<string, double>>()))
            .Callback<Dictionary<string, double>>(outputs => capturedOutputs = outputs)
            .Returns(Task.CompletedTask);

        // Create test rule coordinator instead of loading from string path
        var ruleCoordinator = new TestRuleCoordinator(_loggerMock.Object);

        // Act
        _orchestrator.LoadRules(ruleCoordinator);
        await _orchestrator.ExecuteCycleAsync();

        // Assert
        _redisMock.Verify(x => x.GetSensorValuesAsync(_testSensors), Times.Once);
        Assert.NotNull(capturedOutputs);
        Assert.Equal(84.0, capturedOutputs!["output1"]); // 42 * 2
    }

    [Fact]
    public async Task StartAsync_ThrowsIfRulesNotLoaded()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _orchestrator.StartAsync());
    }

    [Fact]
    public async Task ExecuteCycleAsync_HandlesRedisErrors()
    {
        // Arrange
        var testException = new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Test error");

        // Capture all Error method calls
        var errorCalls = new List<(Exception Ex, string Message)>();

        _loggerMock
            .Setup(x => x.Error(
                It.IsAny<Exception>(),
                It.Is<string>(s => s.Contains("Error during execution cycle"))
            ))
            .Callback<Exception, string>((ex, msg) =>
            {
                errorCalls.Add((ex, msg));
                _output.WriteLine($"Error Called: Ex={ex}, Msg={msg}");
            })
            .Verifiable();

        _redisMock.Setup(x => x.GetSensorValuesAsync(It.IsAny<IEnumerable<string>>()))
            .ThrowsAsync(testException);

        // Act & Assert
        _orchestrator.LoadRules(new TestRuleCoordinator(_loggerMock.Object));
        var ex = await Assert.ThrowsAsync<RedisConnectionException>(
            () => _orchestrator.ExecuteCycleAsync());

        // Verify error logging
        _loggerMock.Verify(
            x => x.Error(
                testException,
                It.Is<string>(s => s.Contains("Error during execution cycle"))),
            Times.Once,
            "Error logging was not called as expected");

        // Additional verification
        Assert.Single(errorCalls);
        var (capturedEx, capturedMsg) = errorCalls[0];
        Assert.Equal(testException, capturedEx);
        Assert.Contains("Error during execution cycle", capturedMsg);
    }

    [Fact]
    public async Task StartStop_WorksCorrectly()
    {
        // Arrange
        var inputs = new Dictionary<string, (double, DateTime)>
        {
            { "sensor1", (42.0, DateTime.UtcNow) },
            { "sensor2", (24.0, DateTime.UtcNow) }
        };

        _redisMock.Setup(x => x.GetSensorValuesAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(inputs);

        _orchestrator.LoadRules(new TestRuleCoordinator(_loggerMock.Object));

        // Act
        await _orchestrator.StartAsync();
        await Task.Delay(250); // Allow a few cycles
        await _orchestrator.StopAsync();

        // Assert - should have multiple cycles worth of calls
        _redisMock.Verify(
            x => x.GetSensorValuesAsync(_testSensors),
            Times.AtLeast(2));
    }

    [Fact]
    public async Task ExecuteCycleAsync_LogsWarningOnSlowCycle()
    {
        // Arrange
        var inputs = new Dictionary<string, (double, DateTime)>
        {
            { "sensor1", (42.0, DateTime.UtcNow) },
            { "sensor2", (24.0, DateTime.UtcNow) }
        };

        // Simulate slow Redis operation
        _redisMock.Setup(x => x.GetSensorValuesAsync(It.IsAny<IEnumerable<string>>()))
            .Returns(async () =>
            {
                await Task.Delay(150); // Longer than cycle time
                return inputs;
            });

        // Act
        _orchestrator.LoadRules(new TestRuleCoordinator(_loggerMock.Object));
        await _orchestrator.ExecuteCycleAsync();

        // Assert
        _loggerMock.Verify(
            x => x.Warning(
                It.Is<string>(s => s.Contains("Cycle time")),    // message template
                It.Is<double>(actual => actual > 100),          // first property
                It.Is<double>(target => target == 100)          // second property
            ),
            Times.Once);
    }

    [Fact]
    public void LoadRules_HandlesInvalidDll()
    {
        // Arrange
        var invalidDllPath = Path.Combine(Path.GetTempPath(), "Invalid.dll");
        File.WriteAllBytes(invalidDllPath, new byte[100]);

        try
        {
            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(
                () => _orchestrator.LoadRules(new TestRuleCoordinator(_loggerMock.Object)));

            // Now the *rethrown* exception includes "Failed to load rules..."
            Assert.Contains("Failed to load rules", ex.Message);

            _loggerMock.Verify(
                x => x.Fatal(
                    It.IsAny<Exception>(),
                    It.Is<string>(s => s.Contains("Failed to load rules")),
                    It.IsAny<string>()),
                Times.Once);
        }
        finally
        {
            try { File.Delete(invalidDllPath); } catch { /* Ignore cleanup */ }
        }
    }

    [Fact]
    public async Task Dispose_CleansUpResourcesCorrectly()
    {
        // Act
        _orchestrator.LoadRules(new TestRuleCoordinator(_loggerMock.Object));
        await _orchestrator.StartAsync();
        await Task.Delay(100);

        // Act
        _orchestrator.Dispose();

        // Assert - attempting to execute cycle after dispose should throw
        await Assert.ThrowsAnyAsync<ObjectDisposedException>(
            () => _orchestrator.ExecuteCycleAsync());
    }
}
