// File: Pulsar.Tests/Integration/IntegrationTests.cs



using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Pulsar.Compiler.Config;
using Pulsar.Compiler.Core;
using Pulsar.Compiler.Exceptions;
using Pulsar.Compiler.Models;
using Pulsar.Compiler.Parsers;
using Pulsar.Tests.TestUtilities;
using Serilog;
using Xunit;

namespace Pulsar.Tests.Integration
{
    public class IntegrationTests
    {
        private readonly ILogger _logger;

        private readonly DslParser _parser;

        private readonly List<string> _validSensors;

        public IntegrationTests()
        {
            _logger = Pulsar.Tests.TestUtilities.LoggingConfig.ToSerilogLogger(
                Pulsar.Tests.TestUtilities.LoggingConfig.GetLogger()
            );

            _parser = new DslParser();

            _validSensors = new List<string>
            {
                "SensorA",
                "SensorB",
                "SensorC",
                "SensorD",
                "temp1",
                "temp2",
                "pressure",
                "humidity",
            };
        }

        [Fact]
        public void Integration_EndToEnd_Succeeds()
        {
            _logger.Debug("Starting end-to-end integration test");

            // Arrange: Define a set of valid rules

            var ruleContents = new[]
            {
                @"rules:

                  - name: Rule1

                    description: Temperature monitoring rule

                    conditions:

                      all:

                        - condition:

                            type: comparison

                            sensor: SensorA

                            operator: greater_than

                            value: 30

                    actions:

                      - set_value:

                          key: temp_alert

                          value: 1",
                @"rules:

                  - name: Rule2

                    description: Pressure monitoring rule

                    conditions:

                      all:

                        - condition:

                            type: comparison

                            sensor: SensorB

                            operator: less_than

                            value: 100

                    actions:

                      - set_value:

                          key: pressure_alert

                          value: 1",
            };

            // Act: Parse each rule

            var rules = new List<RuleDefinition>();

            foreach (var content in ruleContents)
            {
                var parsedRules = _parser.ParseRules(content, _validSensors, "test.yaml");

                rules.AddRange(parsedRules);
            }

            // Compile the parsed rules using the AOT compiler with full project generation

            var compiler = new AOTRuleCompiler();

            var compileResult = compiler.Compile(
                rules.ToArray(),
                new CompilerOptions
                {
                    BuildConfig = new BuildConfig
                    {
                        OutputPath = "test-output",

                        Target = "win-x64",

                        ProjectName = "TestProject",

                        TargetFramework = "net9.0",

                        RulesPath = "test-rules",

                        // Essential AOT and standalone project settings

                        StandaloneExecutable = true,

                        GenerateDebugInfo = true,

                        OptimizeOutput = true,

                        // Runtime configuration

                        RedisConnection = "localhost:6379",

                        CycleTime = 100,

                        BufferCapacity = 100,

                        // Project structure settings

                        MaxRulesPerFile = 50,

                        MaxLinesPerFile = 1000,

                        ComplexityThreshold = 10,

                        GroupParallelRules = true,
                    },
                }
            );

            // Assert compilation success and project structure

            Assert.True(compileResult.Success, "Compilation should succeed with valid rules.");

            Assert.NotNull(compileResult.GeneratedFiles);

            Assert.NotEmpty(compileResult.GeneratedFiles);

            // Verify essential generated files are present

            Assert.True(
                compileResult.GeneratedFiles.Any(f =>
                    f.FileName == "RuleCoordinator.cs" || f.FileName.EndsWith("RuleCoordinator.cs")
                ),
                "RuleCoordinator.cs not found in generated files"
            );

            // Simulate runtime execution

            var sensorInput = new Dictionary<string, string>
            {
                { "SensorA", "100" },
                { "SensorB", "200" },
            };

            var runtimeOutput = RuntimeEngine.RunCycle(ruleContents, sensorInput);

            // Assert: Check expected output from the runtime simulation

            Assert.NotNull(runtimeOutput);

            Assert.Contains("result", runtimeOutput.Keys);

            Assert.Equal("success", runtimeOutput["result"]);

            _logger.Debug("End-to-end integration test completed successfully");
        }

        [Fact]
        public void Integration_LoggingAndMetrics_Succeeds()
        {
            _logger.Debug("Starting logging and metrics integration test");

            var ruleContent =
                @"rules:

              - name: LoggingTestRule

                description: Rule for testing logging

                conditions:

                  all:

                    - condition:

                        type: comparison

                        sensor: SensorA

                        operator: greater_than

                        value: 100

                actions:

                  - set_value:

                      key: log_test

                      value: 1";

            var rules = _parser.ParseRules(ruleContent, _validSensors, "test.yaml");

            var logs = RuntimeEngine.RunCycleWithLogging(
                new[] { ruleContent },
                new Dictionary<string, string> { { "SensorA", "123" } }
            );

            Assert.Contains("Cycle Started", logs);

            Assert.Contains(logs, log => log.Contains("Processing rules:"));

            Assert.Contains(logs, log => log.Contains("Processed Rules:"));

            Assert.Contains(logs, log => log.Contains("Cycle Duration:"));

            Assert.Contains("Cycle Ended", logs);

            _logger.Debug("Logging and metrics integration test completed successfully");
        }

        [Fact]
        public void Integration_RuleFailure_ReportsErrors()
        {
            _logger.Debug("Starting rule failure integration test");

            // Invalid rule with missing required fields

            var invalidRuleContent =
                @"rules:

              - name: InvalidRule

                # Missing conditions and actions";

            // This should throw a ValidationException

            var ex = Assert.Throws<ValidationException>(
                () => _parser.ParseRules(invalidRuleContent, _validSensors, "test.yaml")
            );

            Assert.Contains("must have at least one condition", ex.Message);

            _logger.Debug("Rule failure integration test completed with expected errors");
        }

        [Fact]
        public void Integration_MultipleRulesAndSensors_Succeeds()
        {
            _logger.Debug("Starting multiple rules and sensors integration test");

            var ruleContents = new[]
            {
                @"rules:

                  - name: Rule1

                    description: Multiple sensor test rule 1

                    conditions:

                      all:

                        - condition:

                            type: comparison

                            sensor: SensorA

                            operator: greater_than

                            value: 50

                    actions:

                      - set_value:

                          key: alert1

                          value: 1",
                @"rules:

                  - name: Rule2

                    description: Multiple sensor test rule 2

                    conditions:

                      all:

                        - condition:

                            type: comparison

                            sensor: SensorB

                            operator: less_than

                            value: 150

                    actions:

                      - set_value:

                          key: alert2

                          value: 1",
                @"rules:

                  - name: Rule3

                    description: Multiple sensor test rule 3

                    conditions:

                      all:

                        - condition:

                            type: expression

                            expression: SensorC + SensorD > 500

                    actions:

                      - set_value:

                          key: alert3

                          value: 1",
            };

            var sensorInputs = new Dictionary<string, string>
            {
                { "SensorA", "100" },
                { "SensorB", "200" },
                { "SensorC", "300" },
                { "SensorD", "400" },
            };

            var logs = RuntimeEngine.RunCycleWithLogging(ruleContents, sensorInputs);

            var output = RuntimeEngine.RunCycle(ruleContents, sensorInputs);

            Assert.Contains("Cycle Started", logs);

            Assert.Contains(logs, log => log.Contains("Processing rules:"));

            Assert.Contains(logs, log => log.Contains("Processed Rules:"));

            Assert.Contains(logs, log => log.Contains("Cycle Duration:"));

            Assert.Contains("Cycle Ended", logs);

            Assert.Contains("result", output.Keys);

            Assert.Equal("success", output["result"]);

            _logger.Debug("Multiple rules and sensors integration test completed successfully");
        }

        [Fact]
        public void Integration_StressTest_LargeRuleSet()
        {
            _logger.Debug("Starting stress test with large rule set");

            // Generate a large set of rules

            var ruleBuilder = new StringBuilder();

            ruleBuilder.AppendLine("rules:");

            for (int i = 1; i <= 100; i++)
            {
                ruleBuilder.AppendLine(
                    $@"  - name: StressRule{i}

    description: Stress test rule {i}

    conditions:

      all:

        - condition:

            type: comparison

            sensor: SensorA

            operator: greater_than

            value: {i}

    actions:

      - set_value:

          key: stress_alert_{i}

          value: 1"
                );
            }

            var rules = _parser.ParseRules(
                ruleBuilder.ToString(),
                _validSensors,
                "stress_test.yaml"
            );

            Assert.Equal(100, rules.Count());

            var compiler = new AOTRuleCompiler();

            var compileResult = compiler.Compile(
                rules.ToArray(),
                new CompilerOptions
                {
                    BuildConfig = new BuildConfig
                    {
                        OutputPath = "stress-test-output",

                        Target = "win-x64",

                        ProjectName = "StressTestProject",

                        TargetFramework = "net9.0",

                        RulesPath = "stress-test-rules",

                        StandaloneExecutable = true,

                        OptimizeOutput = true,

                        MaxRulesPerFile = 50,
                    },
                }
            );

            Assert.True(compileResult.Success);

            Assert.True(compileResult.GeneratedFiles.Count() > 2); // Should have multiple rule files due to MaxRulesPerFile

            _logger.Debug("Stress test completed successfully");
        }
    }
}
