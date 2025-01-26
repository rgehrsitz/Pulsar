// File: Pulsar.Tests/CompilerTests/MSBuildIntegrationTests.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;  // For UTF8Encoding
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Pulsar.Compiler.Build;
using Pulsar.Compiler.Parsers;  // For DslParser
using Pulsar.MSBuild;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Pulsar.Tests.CompilerTests
{
    public class MSBuildIntegrationTests : IDisposable
    {
        private readonly string _testRulesDir;
        private readonly string _testOutputDir;
        private string _testConfigFile;
        private readonly Serilog.ILogger _logger;
        private readonly ITestOutputHelper _output;
        private readonly List<string> _logMessages = new();

        public MSBuildIntegrationTests(ITestOutputHelper output)
        {
            _output = output;

            // Create unique test directories for each run
            _testRulesDir = Path.Combine(Path.GetTempPath(), $"PulsarTestRules_{Guid.NewGuid()}");
            _testOutputDir = Path.Combine(Path.GetTempPath(), $"PulsarTestOutput_{Guid.NewGuid()}");
            _testConfigFile = Path.Combine(_testRulesDir, "config.yaml");

            Directory.CreateDirectory(_testRulesDir);
            Directory.CreateDirectory(_testOutputDir);

            // Configure shared logger
            _logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Sink(new ListSink(_logMessages))
                .WriteTo.TestOutput(_output)
                .CreateLogger();

            SetupTestEnvironment();
        }

        private void SetupTestEnvironment()
        {
            // Create basic config file
            var configContent = @"
version: 1
validSensors:
  - temperature
  - pressure
  - alert";
            File.WriteAllText(_testConfigFile, configContent);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testRulesDir))
                    Directory.Delete(_testRulesDir, true);
                if (Directory.Exists(_testOutputDir))
                    Directory.Delete(_testOutputDir, true);
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Warning: Cleanup failed: {ex.Message}");
            }
        }

        [Fact]
        public void BasicBuild_WithValidRule_Succeeds()
        {
            try
            {
                // Create rules subdirectory
                var rulesDir = Path.Combine(_testRulesDir, "rules");
                Directory.CreateDirectory(rulesDir);

                // Create rule file in rules subdirectory
                var ruleContent = @"rules:
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
          value: 1";

                var rulePath = Path.Combine(rulesDir, "test_rule.yaml");
                File.WriteAllText(rulePath, ruleContent, new UTF8Encoding(false));

                // Create config file in the root directory
                var systemConfigContent = @"version: 1
validSensors:
  - temperature
  - pressure
  - alert";

                _testConfigFile = Path.Combine(_testRulesDir, "config.yaml");
                File.WriteAllText(_testConfigFile, systemConfigContent);

                // Read back and verify content
                var verifyContent = File.ReadAllText(rulePath);
                _output.WriteLine($"File content verification:\n{verifyContent}");
                _output.WriteLine($"Content length: {verifyContent.Length}");
                _output.WriteLine($"First few bytes: {BitConverter.ToString(File.ReadAllBytes(rulePath).Take(10).ToArray())}");

                // Log directory structure for debugging
                _output.WriteLine($"\nDirectory structure:");
                foreach (var file in Directory.GetFiles(_testRulesDir, "*.*", SearchOption.AllDirectories))
                {
                    _output.WriteLine($"- {file}");
                }

                var mockEngine = new MockBuildEngine(_output);
                var buildTask = new PulsarRuleBuildTask
                {
                    RulesDirectory = rulesDir,  // Point to the rules subdirectory
                    OutputDirectory = _testOutputDir,
                    ConfigFile = _testConfigFile,
                    MaxRulesPerFile = 100,
                    GenerateDebugInfo = true,
                    BuildEngine = mockEngine
                };

                var result = buildTask.Execute();

                // Try parsing the rule directly for verification
                try
                {
                    var parser = new DslParser();
                    var validSensors = new List<string> { "temperature", "pressure", "alert" };
                    var testParse = parser.ParseRules(verifyContent, validSensors, rulePath);
                    _output.WriteLine($"Direct parse test succeeded with {testParse.Count} rules");
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"Direct parse test failed: {ex}");
                }

                Assert.True(result, "Build task failed. Check logs above for details.");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Test failed with exception: {ex}");
                _output.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        [Fact]
        public void Build_WithInvalidRuleSyntax_Fails()
        {
            try
            {
                var rulesDir = Path.Combine(_testRulesDir, "rules");
                Directory.CreateDirectory(rulesDir);

                // Create invalid rule file with incorrect YAML structure
                var invalidRuleContent = @"rules:
  - name: 'InvalidRule'
    conditions: [invalid]  # Invalid YAML structure for conditions
    actions: not-valid";  // Invalid YAML structure for actions

                var rulePath = Path.Combine(rulesDir, "invalid_rule.yaml");
                File.WriteAllText(rulePath, invalidRuleContent);

                var systemConfigContent = @"version: 1
validSensors:
  - temperature
  - pressure";

                _testConfigFile = Path.Combine(_testRulesDir, "config.yaml");
                File.WriteAllText(_testConfigFile, systemConfigContent);

                var mockEngine = new MockBuildEngine(_output);
                var buildTask = new PulsarRuleBuildTask
                {
                    RulesDirectory = rulesDir,
                    OutputDirectory = _testOutputDir,
                    ConfigFile = _testConfigFile,
                    BuildEngine = mockEngine
                };

                var result = buildTask.Execute();
                Assert.False(result, "Build should fail with invalid rule syntax");

                // Check for YAML deserialization error
                var errors = mockEngine.GetErrors();
                _output.WriteLine("Actual errors:");
                foreach (var error in errors)
                {
                    _output.WriteLine(error);
                }
                Assert.Contains(errors, error => error.Contains("deserializer") || error.Contains("deserialize"));
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Test failed with exception: {ex}");
                throw;
            }
        }

        [Fact]
        public void Build_WithMissingConfigFile_Fails()
        {
            try
            {
                var rulesDir = Path.Combine(_testRulesDir, "rules");
                Directory.CreateDirectory(rulesDir);

                // Create a simple rule
                var ruleContent = @"rules:
  - name: 'SimpleRule'
    conditions:
      all:
        - condition:
            type: expression
            expression: '1 > 0'
    actions:
      - set_value:
          key: 'test_output'
          value: 1";

                var rulePath = Path.Combine(rulesDir, "simple_rule.yaml");
                File.WriteAllText(rulePath, ruleContent);

                var mockEngine = new MockBuildEngine(_output);
                var buildTask = new PulsarRuleBuildTask
                {
                    RulesDirectory = rulesDir,
                    OutputDirectory = _testOutputDir,
                    ConfigFile = "nonexistent_config.yaml",
                    BuildEngine = mockEngine
                };

                // Execute should return false when config file is missing
                var result = buildTask.Execute();
                Assert.False(result, "Build should fail when config file is missing");

                // Verify we get an error about missing config
                var errors = mockEngine.GetErrors();
                Assert.Contains(errors, e => e.Contains("Configuration file not found"));
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Test failed with exception: {ex}");
                throw;
            }
        }

        [Fact]
        public void Build_WithEmptyRulesDirectory_Fails()
        {
            try
            {
                var rulesDir = Path.Combine(_testRulesDir, "empty_rules");
                Directory.CreateDirectory(rulesDir);

                var systemConfigContent = @"version: 1
validSensors:
  - temperature
  - pressure";

                _testConfigFile = Path.Combine(_testRulesDir, "config.yaml");
                File.WriteAllText(_testConfigFile, systemConfigContent);

                var mockEngine = new MockBuildEngine(_output);
                var buildTask = new PulsarRuleBuildTask
                {
                    RulesDirectory = rulesDir,
                    OutputDirectory = _testOutputDir,
                    ConfigFile = _testConfigFile,
                    BuildEngine = mockEngine
                };

                var result = buildTask.Execute();
                Assert.False(result, "Build should fail with empty rules directory");
                Assert.Contains(mockEngine.GetErrors(), error => error.Contains("No rule files found"));
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Test failed with exception: {ex}");
                throw;
            }
        }

        [Fact]
        public void Build_WithInvalidSensor_Fails()
        {
            try
            {
                var rulesDir = Path.Combine(_testRulesDir, "rules");
                Directory.CreateDirectory(rulesDir);

                // Create rule with invalid sensor
                var ruleContent = @"rules:
  - name: 'InvalidSensorRule'
    conditions:
      all:
        - condition:
            type: comparison
            sensor: 'nonexistent_sensor'
            operator: '>'
            value: 100
    actions:
      - set_value:
          key: 'alert'
          value: 1";

                var rulePath = Path.Combine(rulesDir, "invalid_sensor_rule.yaml");
                File.WriteAllText(rulePath, ruleContent);

                var systemConfigContent = @"version: 1
validSensors:
  - temperature
  - pressure";

                _testConfigFile = Path.Combine(_testRulesDir, "config.yaml");
                File.WriteAllText(_testConfigFile, systemConfigContent);

                var mockEngine = new MockBuildEngine(_output);
                var buildTask = new PulsarRuleBuildTask
                {
                    RulesDirectory = rulesDir,
                    OutputDirectory = _testOutputDir,
                    ConfigFile = _testConfigFile,
                    BuildEngine = mockEngine
                };

                var result = buildTask.Execute();
                Assert.False(result, "Build should fail with invalid sensor");
                Assert.Contains(mockEngine.GetErrors(), error => error.Contains("Invalid sensors"));
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Test failed with exception: {ex}");
                throw;
            }
        }

        [Fact]
        public void Build_WithMultipleRules_Succeeds()
        {
            try
            {
                var rulesDir = Path.Combine(_testRulesDir, "rules");
                Directory.CreateDirectory(rulesDir);

                // Create file with multiple rules
                var ruleContent = @"rules:
  - name: 'Rule1'
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
          value: 1
  - name: 'Rule2'
    conditions:
      all:
        - condition:
            type: comparison
            sensor: 'pressure'
            operator: '<'
            value: 50
    actions:
      - set_value:
          key: 'alert'
          value: 2";

                var rulePath = Path.Combine(rulesDir, "multiple_rules.yaml");
                File.WriteAllText(rulePath, ruleContent);

                var systemConfigContent = @"version: 1
validSensors:
  - temperature
  - pressure
  - alert";

                _testConfigFile = Path.Combine(_testRulesDir, "config.yaml");
                File.WriteAllText(_testConfigFile, systemConfigContent);

                var mockEngine = new MockBuildEngine(_output);
                var buildTask = new PulsarRuleBuildTask
                {
                    RulesDirectory = rulesDir,
                    OutputDirectory = _testOutputDir,
                    ConfigFile = _testConfigFile,
                    BuildEngine = mockEngine
                };

                var result = buildTask.Execute();
                Assert.True(result, "Build should succeed with multiple rules");

                // Verify both rules were processed
                var outputFiles = Directory.GetFiles(_testOutputDir, "*.cs", SearchOption.AllDirectories);
                Assert.True(outputFiles.Length > 0, "Should generate output files");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Test failed with exception: {ex}");
                throw;
            }
        }

        private class MockBuildEngine : IBuildEngine
        {
            private readonly ITestOutputHelper _output;
            private readonly List<string> _errors = new();
            private readonly List<string> _warnings = new();
            private readonly List<string> _messages = new();

            public MockBuildEngine(ITestOutputHelper output)
            {
                _output = output;
            }

            public void LogErrorEvent(BuildErrorEventArgs e)
            {
                var message = $"Error: {e.Message}";
                _errors.Add(message);
                _output.WriteLine(message);
            }

            public void LogWarningEvent(BuildWarningEventArgs e)
            {
                var message = $"Warning: {e.Message}";
                _warnings.Add(message);
                _output.WriteLine(message);
            }

            public void LogMessageEvent(BuildMessageEventArgs e)
            {
                var message = $"Message: {e.Message}";
                _messages.Add(message);
                _output.WriteLine(message);
            }

            public void LogCustomEvent(CustomBuildEventArgs e)
            {
                _output.WriteLine($"Custom: {e.Message}");
            }

            public bool BuildProjectFile(
                string projectFileName,
                string[] targetNames,
                System.Collections.IDictionary globalProperties,
                System.Collections.IDictionary targetOutputs)
            {
                return true;
            }

            public List<string> GetErrors() => _errors;
            public List<string> GetWarnings() => _warnings;
            public List<string> GetMessages() => _messages;

            public bool ContinueOnError => false;
            public int LineNumberOfTaskNode => 0;
            public int ColumnNumberOfTaskNode => 0;
            public string ProjectFileOfTaskNode => string.Empty;
        }
    }
}