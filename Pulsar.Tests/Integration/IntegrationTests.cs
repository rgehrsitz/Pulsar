// File: Pulsar.Tests/Integration/IntegrationTests.cs

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Pulsar.Tests.TestUtilities;
using Serilog;
using Pulsar.Compiler;
using Pulsar.Compiler.Core;
using Pulsar.Compiler.Models;
using Pulsar.Compiler.Config;

namespace Pulsar.Tests.Integration
{
    public class IntegrationTests
    {
        private readonly ILogger _logger;

        public IntegrationTests()
        {
            _logger = LoggingConfig.GetLogger();
        }

        [Fact]
        public void Integration_EndToEnd_Succeeds()
        {
            _logger.Debug("Starting end-to-end integration test");

            // Arrange: Define a set of valid rule contents
            string[] ruleContents = new string[]
            {
                "valid rule content",
                "another valid rule content",
            };

            // Act: Parse each rule using the stub parser
            var parsedResults = ruleContents.Select(r => RuleParser.Parse(r)).ToArray();
            foreach (var result in parsedResults)
            {
                Assert.True(result.IsValid, "Each rule should be valid.");
            }

            // Compile the parsed rules using the AOT compiler with full project generation
            var compiler = new AOTRuleCompiler();
            var compileResult = compiler.Compile(
                parsedResults.Where(r => r.IsValid).Select(r => r.Rule).ToArray(),
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
                        GroupParallelRules = true
                    }
                }
            );

            // Assert compilation success and project structure
            Assert.True(compileResult.Success, "Compilation should succeed with valid rules.");
            Assert.NotNull(compileResult.GeneratedFiles);
            Assert.NotEmpty(compileResult.GeneratedFiles);
            
            // Verify essential project files are generated
            Assert.Contains(compileResult.GeneratedFiles, 
                f => f.FileName.EndsWith(".csproj"), 
                "Project file should be generated");
            Assert.Contains(compileResult.GeneratedFiles,
                f => f.FileName.EndsWith("RuleCoordinator.cs"),
                "Rule coordinator should be generated");
            Assert.Contains(compileResult.GeneratedFiles,
                f => f.FileName.EndsWith("Program.cs"),
                "Program entry point should be generated");

            // Simulate runtime execution with a stub runtime engine
            var sensorInput = new Dictionary<string, string>
            {
                { "SensorA", "100" },
                { "SensorB", "200" },
            };
            var runtimeOutput = RuntimeEngine.RunCycle(ruleContents, sensorInput);

            // Assert: Check expected output from the runtime simulation
            Assert.NotNull(runtimeOutput);
            Assert.True(runtimeOutput.ContainsKey("result"), "Runtime output should contain a 'result' key.");
            Assert.Equal("success", runtimeOutput["result"]);

            _logger.Debug("End-to-end integration test completed successfully");
        }

        [Fact]
        public void Integration_LoggingAndMetrics_Succeeds()
        {
            _logger.Debug("Starting logging and metrics integration test");

            var logs = RuntimeEngine.RunCycleWithLogging(
                new string[] { "valid rule content" },
                new Dictionary<string, string> { { "SensorA", "123" } }
            );

            Assert.Contains("Cycle Started", logs);
            Assert.True(logs.Any(log => log.Contains("Processing rules:")));
            Assert.True(logs.Any(log => log.Contains("Processed Rules:")));
            Assert.True(logs.Any(log => log.Contains("Cycle Duration:")));
            Assert.Contains("Cycle Ended", logs);

            _logger.Debug("Logging and metrics integration test completed successfully");
        }

        [Fact]
        public void Integration_RuleFailure_ReportsErrors()
        {
            _logger.Debug("Starting rule failure integration test");

            string[] ruleContents = new string[] { "invalid rule content" };
            var parsedResult = RuleParser.Parse(ruleContents[0]);
            var compiler = new AOTRuleCompiler();
            var compileResult = compiler.Compile(
                ruleContents.Select(r => new RuleDefinition()).ToArray(),
                new CompilerOptions
                {
                    BuildConfig = new BuildConfig
                    {
                        OutputPath = "test-output",
                        Target = "test-target",
                        ProjectName = "test-project",
                        TargetFramework = "net9.0",
                        RulesPath = "test-rules"
                    }
                }
            );

            Assert.False(parsedResult.IsValid, "Expected rule parsing to fail for invalid rule content.");
            Assert.False(compileResult.Success, "Expected compilation to fail with invalid rules.");
            Assert.NotNull(compileResult.Errors);
            Assert.NotEmpty(compileResult.Errors);

            _logger.Debug("Rule failure integration test completed with expected errors");
        }

        [Fact]
        public void Integration_MultipleRulesAndSensors_Succeeds()
        {
            _logger.Debug("Starting multiple rules and sensors integration test");

            string[] ruleContents = new string[]
            {
                "valid rule content 1",
                "valid rule content 2",
                "valid rule content 3",
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
            Assert.True(logs.Any(log => log.Contains("Processing rules:")));
            Assert.True(logs.Any(log => log.Contains("Processed Rules:")));
            Assert.True(logs.Any(log => log.Contains("Cycle Duration:")));
            Assert.Contains("Cycle Ended", logs);
            Assert.True(output.ContainsKey("result") && output["result"] == "success");

            _logger.Debug("Multiple rules and sensors integration test completed successfully");
        }

        [Fact]
        public void Integration_StressTest_LargeRuleSet()
        {
            _logger.Debug("Starting large rule set stress test");

            var ruleContents = Enumerable.Range(1, 1000)
                .Select(i => $"valid rule content {i}")
                .ToArray();

            var sensorInputs = new Dictionary<string, string>();
            for (int i = 1; i <= 100; i++)
            {
                sensorInputs.Add($"Sensor{i}", (i * 10).ToString());
            }

            var logs = RuntimeEngine.RunCycleWithLogging(ruleContents, sensorInputs);
            var output = RuntimeEngine.RunCycle(ruleContents, sensorInputs);

            Assert.Contains("Cycle Started", logs);
            Assert.True(logs.Any(log => log.Contains("Processing rules:")));
            Assert.True(logs.Any(log => log.Contains("Processed Rules:")));
            Assert.True(logs.Any(log => log.Contains("Cycle Duration:")));
            Assert.Contains("Cycle Ended", logs);
            Assert.True(output.ContainsKey("result") && output["result"] == "success");

            _logger.Debug("Large rule set stress test completed successfully");
        }
    }
}
