using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using YamlDotNet.Serialization;
using Pulsar.Compiler;
using Pulsar.Compiler.Models;

namespace Pulsar.Tests.CLITests
{
    public class ProgramTests
    {
        private readonly Mock<ILogger> _loggerMock;
        private readonly TestLoggerSink _testSink;

        public ProgramTests()
        {
            _testSink = new TestLoggerSink();
            var logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.Sink(_testSink)
                .CreateLogger();

            _loggerMock = new Mock<ILogger>();
            Log.Logger = logger; // Set global logger for Program.Main
        }

        [Fact]
        public async Task ValidateCommand_WithValidArguments_ShouldLogSuccess()
        {
            // Arrange
            var tempRulesFile = CreateTempFile("rules.yaml", @"
rules:
  - name: 'TestRule'
    conditions:
      all:
        - condition:
            type: comparison
            sensor: 'temperature'
            operator: '>'
            value: 100
    actions:
      - set_value:
          key: 'alert'
          value: 1");

            var tempConfigFile = CreateTempFile("config.yaml", @"
version: 1
validSensors:
  - temperature
  - alert");

            var args = new[]
            {
                "validate",
                "--rules", tempRulesFile,
                "--config", tempConfigFile
            };

            // Act
            var result = await Program.Main(args);

            // Assert
            Assert.Equal(0, result);
            Assert.Contains("Successfully validated", _testSink.LogMessages);
        }

        [Fact]
        public async Task GenerateCommand_WithNonWritableOutputDirectory_ShouldThrowException()
        {
            // Arrange
            var tempRulesFile = CreateTempFile("rules.yaml", @"
rules:
  - name: 'TestRule'
    conditions:
      all:
        - condition:
            type: comparison
            sensor: 'temperature'
            operator: '>'
            value: 100
    actions:
      - set_value:
          key: 'alert'
          value: 1");

            var tempConfigFile = CreateTempFile("config.yaml", @"
version: 1
validSensors:
  - temperature
  - alert");

            var outputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(outputDir);
            File.SetAttributes(outputDir, FileAttributes.ReadOnly);

            var args = new[]
            {
                "generate",
                "--rules", tempRulesFile,
                "--config", tempConfigFile,
                "--output", outputDir
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Program.Main(args));
            Assert.Contains("Output directory", ex.Message);

            // Cleanup
            File.SetAttributes(outputDir, FileAttributes.Normal);
            Directory.Delete(outputDir);
        }

        [Fact]
        public async Task InvalidCommand_ShouldLogError()
        {
            // Arrange
            var args = new[] { "invalid-command" };

            // Act
            var result = await Program.Main(args);

            // Assert
            Assert.Equal(1, result);
            Assert.Contains("Unknown command", _testSink.LogMessages);
        }

        private static string CreateTempFile(string fileName, string content)
        {
            var tempFilePath = Path.Combine(Path.GetTempPath(), fileName);
            File.WriteAllText(tempFilePath, content);
            return tempFilePath;
        }

        private class TestLoggerSink : ILogEventSink
        {
            public List<string> LogMessages { get; } = new();

            public void Emit(LogEvent logEvent)
            {
                LogMessages.Add(logEvent.RenderMessage());
            }
        }
    }
}
