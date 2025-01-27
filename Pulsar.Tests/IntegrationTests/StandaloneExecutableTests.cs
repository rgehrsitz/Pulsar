// File: Pulsar.Tests/IntegrationTests/StandaloneExecutableTests.cs

using System;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;  // For Task
using Xunit;
using Xunit.Abstractions;
using StackExchange.Redis;
using Serilog.Core;  // Specific Serilog classes
using Serilog;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Pulsar.Tests.IntegrationTests
{
    public class StandaloneExecutableTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly string _testDir;
        private readonly string _rulesFile;
        private readonly string _configFile;
        private readonly string _outputDir;
        private readonly string _exePath;
        private readonly Serilog.ILogger _logger;  // Specify Serilog.ILogger
        private readonly ConnectionMultiplexer _redis;
        private const string TestKeyPrefix = "pulsar_test_";

        public StandaloneExecutableTests(ITestOutputHelper output)
        {
            _output = output;

            // Create unique test directories
            _testDir = Path.Combine(Path.GetTempPath(), $"PulsarTest_{Guid.NewGuid()}");
            _rulesFile = Path.Combine(_testDir, "rules.yaml");
            _configFile = Path.Combine(_testDir, "config.yaml");
            _outputDir = Path.Combine(_testDir, "output");
            _exePath = Path.Combine(_outputDir, "PulsarGeneratedRules.exe");

            Directory.CreateDirectory(_testDir);
            Directory.CreateDirectory(_outputDir);

            // Setup logger
            _logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.TestOutput(output)
                .CreateLogger();

            // Connect to Redis test instance
            _redis = ConnectionMultiplexer.Connect("localhost:6379");

            SetupTestEnvironment();
        }

        private void SetupTestEnvironment()
        {
            // Create test config file
            var configContent = @"
version: 1
validSensors:
  - temperature
  - temperature_c
  - alert
  - alert_duration
cycleTime: 100  # ms
redisConnection: localhost:6379
bufferCapacity: 100";
            File.WriteAllText(_configFile, configContent);

            // Create test rule file that demonstrates core functionality
            var ruleContent = @"
rules:
  - name: 'TemperatureConversion'
    description: 'Converts Fahrenheit to Celsius'
    conditions:
      all:
        - condition:
            type: comparison
            sensor: 'temperature'
            operator: '>'
            value: -459.67  # Absolute zero check
    actions:
      - set_value:
          key: 'temperature_c'
          value_expression: '(temperature - 32) * 5/9'

  - name: 'HighTempAlert'
    description: 'Alerts when temperature stays high'
    conditions:
      all:
        - condition:
            type: threshold_over_time
            sensor: 'temperature_c'
            threshold: 50
            duration: 500  # Alert after 500ms above threshold
    actions:
      - set_value:
          key: 'alert'
          value: 1
      - set_value:
          key: 'alert_duration'
          value_expression: 'temperature_c'";

            File.WriteAllText(_rulesFile, ruleContent);
        }

        [Fact]
        public async System.Threading.Tasks.Task CompileAndRunStandaloneExecutable_WorksEndToEnd()
        {
            try
            {
                // Step 1: Compile rules to standalone executable
                var buildTask = new Pulsar.MSBuild.PulsarRuleBuildTask
                {
                    RulesDirectory = _testDir,
                    OutputDirectory = _outputDir,
                    ConfigFile = _configFile,
                    BuildEngine = new MockBuildEngine(_output),
                    // Property name changed to match what we'll add to PulsarRuleBuildTask
                    StandaloneExecutable = true
                };

                Assert.True(buildTask.Execute(), "Build task should succeed");
                Assert.True(File.Exists(_exePath), "Executable should be created");


                // Step 2: Start the standalone process
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _exePath,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };

                var processOutputLog = new System.Collections.Concurrent.ConcurrentQueue<string>();
                process.OutputDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        processOutputLog.Enqueue(e.Data);
                        _output.WriteLine($"Process: {e.Data}");
                    }
                };
                process.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        _output.WriteLine($"Process Error: {e.Data}");
                    }
                };

                _output.WriteLine("Starting standalone process...");
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                try
                {
                    // Step 3: Test rule evaluation
                    var db = _redis.GetDatabase();

                    // Test immediate conversion rule
                    await db.HashSetAsync($"{TestKeyPrefix}temperature", new HashEntry[]
                    {
                        new HashEntry("value", "98.6"),
                        new HashEntry("timestamp", DateTime.UtcNow.Ticks.ToString())
                    });

                    // Wait for one cycle
                    await System.Threading.Tasks.Task.Delay(150);

                    // Verify temperature conversion happened
                    var tempC = await db.HashGetAsync($"{TestKeyPrefix}temperature_c", "value");
                    Assert.True(tempC.HasValue, "Should have Celsius temperature");
                    Assert.Equal(37.0, double.Parse(tempC!), 1);

                    // Test temporal condition by keeping temperature high
                    for (int i = 0; i < 6; i++)
                    {
                        await db.HashSetAsync($"{TestKeyPrefix}temperature_c", new HashEntry[]
                        {
                            new HashEntry("value", "51.0"),
                            new HashEntry("timestamp", DateTime.UtcNow.Ticks.ToString())
                        });
                        await System.Threading.Tasks.Task.Delay(100);
                    }

                    // Verify alert was triggered after duration threshold
                    var alert = await db.HashGetAsync($"{TestKeyPrefix}alert", "value");
                    Assert.True(alert.HasValue, "Alert should be triggered");
                    Assert.Equal("1", alert.ToString());

                    // Verify alert temperature was recorded
                    var alertTemp = await db.HashGetAsync($"{TestKeyPrefix}alert_duration", "value");
                    Assert.True(alertTemp.HasValue, "Alert temperature should be recorded");
                    Assert.Equal("51.0", alertTemp.ToString());

                    // Verify process logs show healthy operation
                    Assert.Contains(processOutputLog, log => log.Contains("Started processing rules"));
                    Assert.Contains(processOutputLog, log => log.Contains("temperature conversion"));
                    Assert.DoesNotContain(processOutputLog, log => log.Contains("Error"));
                }
                finally
                {
                    // Cleanup process
                    if (!process.HasExited)
                    {
                        process.Kill(true);
                        process.WaitForExit();
                    }
                }
                // Add explicit return for Task
                await System.Threading.Tasks.Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Test failed: {ex}");
                throw;
            }
        }

        public void Dispose()
        {
            try
            {
                // Cleanup Redis test keys
                var server = _redis.GetServer("localhost:6379");
                var testKeys = server.Keys(pattern: $"{TestKeyPrefix}*");
                var db = _redis.GetDatabase();
                foreach (var key in testKeys)
                {
                    db.KeyDelete(key);
                }

                // Cleanup test files
                if (Directory.Exists(_testDir))
                {
                    Directory.Delete(_testDir, true);
                }

                _redis.Dispose();
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Cleanup failed: {ex.Message}");
            }
        }

        private class MockBuildEngine : IBuildEngine
        {
            private readonly ITestOutputHelper _output;

            public MockBuildEngine(ITestOutputHelper output)
            {
                _output = output;
            }

            public bool ContinueOnError => false;
            public int LineNumberOfTaskNode => 0;
            public int ColumnNumberOfTaskNode => 0;
            public string ProjectFileOfTaskNode => string.Empty;

            public void LogErrorEvent(BuildErrorEventArgs e)
                => _output.WriteLine($"Error: {e.Message}");

            public void LogWarningEvent(BuildWarningEventArgs e)
                => _output.WriteLine($"Warning: {e.Message}");

            public void LogMessageEvent(BuildMessageEventArgs e)
                => _output.WriteLine($"Message: {e.Message}");

            public void LogCustomEvent(CustomBuildEventArgs e)
                => _output.WriteLine($"Custom: {e.Message}");

            public bool BuildProjectFile(string projectFileName, string[] targetNames,
                System.Collections.IDictionary globalProperties,
                System.Collections.IDictionary targetOutputs)
                => true;
        }
    }
}