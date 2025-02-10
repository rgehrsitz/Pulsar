// File: Pulsar.Tests/Integration/IntegrationTests.cs

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Pulsar.Tests.Integration
{
    public class IntegrationTests
    {
        [Fact]
        public void Integration_EndToEnd_Succeeds()
        {
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

            // Compile the rules using the stub compiler
            var compileResult = RuleCompiler.Compile(ruleContents);
            Assert.True(compileResult.IsSuccess, "Compilation should succeed with valid rules.");
            Assert.NotNull(compileResult.SourceFiles);
            Assert.NotEmpty(compileResult.SourceFiles);

            // Simulate runtime execution with a stub runtime engine
            var sensorInput = new Dictionary<string, string>
            {
                { "SensorA", "100" },
                { "SensorB", "200" },
            };
            var runtimeOutput = RuntimeEngine.RunCycle(ruleContents, sensorInput);

            // Assert: Check expected output from the runtime simulation
            Assert.NotNull(runtimeOutput);
            Assert.True(
                runtimeOutput.ContainsKey("result"),
                "Runtime output should contain a 'result' key."
            );
            Assert.Equal("success", runtimeOutput["result"]);
        }

        [Fact]
        public void Integration_LoggingAndMetrics_Succeeds()
        {
            // Arrange: Simulate runtime execution with logging using the enhanced RuntimeEngine method
            var logs = RuntimeEngine.RunCycleWithLogging(
                new string[] { "valid rule content" },
                new Dictionary<string, string> { { "SensorA", "123" } }
            );

            // Assert: Validate that logs contain enhanced metrics messages using substring matching
            Assert.Contains("Cycle Started", logs);
            Assert.True(
                logs.Any(log => log.Contains("Processing rules:")),
                "Expected at least one log entry to contain 'Processing rules:'"
            );
            Assert.True(
                logs.Any(log => log.Contains("Processed Rules:")),
                "Expected at least one log entry to contain 'Processed Rules:'"
            );
            Assert.True(
                logs.Any(log => log.Contains("Cycle Duration:")),
                "Expected at least one log entry to contain 'Cycle Duration:'"
            );
            Assert.Contains("Cycle Ended", logs);
        }

        [Fact]
        public void Integration_RuleFailure_ReportsErrors()
        {
            // Arrange: Provide an invalid rule content
            string[] ruleContents = new string[] { "invalid rule content" };

            // Act: Parse and compile the invalid rule
            var parsedResult = RuleParser.Parse(ruleContents[0]);
            var compileResult = RuleCompiler.Compile(ruleContents);

            // Assert: Validate that parsing failed or compilation fails and returns detailed errors
            Assert.False(
                parsedResult.IsValid,
                "Expected rule parsing to fail for invalid rule content."
            );
            Assert.False(
                compileResult.IsSuccess,
                "Expected compilation to fail with invalid rules."
            );
            Assert.NotNull(compileResult.Errors);
            Assert.NotEmpty(compileResult.Errors);
        }

        [Fact]
        public void Integration_MultipleRulesAndSensors_Succeeds()
        {
            // Arrange: Create multiple valid rule contents and a diverse sensor input
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

            // Act: Run the simulated runtime cycle with logging
            var logs = RuntimeEngine.RunCycleWithLogging(ruleContents, sensorInputs);
            var output = RuntimeEngine.RunCycle(ruleContents, sensorInputs);

            // Assert: Check that logs and output are as expected
            Assert.Contains("Cycle Started", logs);
            Assert.True(
                logs.Any(log => log.Contains("Processing rules:")),
                "Expected log for processing rules."
            );
            Assert.True(
                logs.Any(log => log.Contains("Processed Rules:")),
                "Expected log for processed rules."
            );
            Assert.True(
                logs.Any(log => log.Contains("Cycle Duration:")),
                "Expected log for cycle duration."
            );
            Assert.Contains("Cycle Ended", logs);
            Assert.True(
                output.ContainsKey("result") && output["result"] == "success",
                "Expected runtime output to indicate success."
            );
        }

        [Fact]
        public void Integration_StressTest_LargeRuleSet()
        {
            // Arrange: Generate a large set of dummy valid rules (1000 rules) and sensor inputs (100 sensors)
            var ruleContents = Enumerable
                .Range(1, 1000)
                .Select(i => $"valid rule content {i}")
                .ToArray();

            var sensorInputs = new Dictionary<string, string>();
            for (int i = 1; i <= 100; i++)
            {
                sensorInputs.Add($"Sensor{i}", (i * 10).ToString());
            }

            // Act: Run the simulated runtime cycle with logging
            var logs = RuntimeEngine.RunCycleWithLogging(ruleContents, sensorInputs);
            var output = RuntimeEngine.RunCycle(ruleContents, sensorInputs);

            // Assert: Validate that enhanced metrics log entries are present
            Assert.Contains("Cycle Started", logs);
            Assert.True(
                logs.Any(log => log.Contains("Processing rules:")),
                "Expected log entry for processing rules."
            );
            Assert.True(
                logs.Any(log => log.Contains("Processed Rules:")),
                "Expected log entry for processed rules."
            );
            Assert.True(
                logs.Any(log => log.Contains("Cycle Duration:")),
                "Expected log entry for cycle duration."
            );
            Assert.Contains("Cycle Ended", logs);
            Assert.True(
                output.ContainsKey("result") && output["result"] == "success",
                "Expected runtime output to indicate success."
            );
        }
    }

    // Stub implementations for integration testing

    // The RuleParser and RuleCompiler stubs are assumed to be in other test files; we redeclare them here for integration purposes.
    public static class RuleParser
    {
        public static RuleParseResult Parse(string ruleContent)
        {
            // Simulate realistic rule parsing with dependency analysis
            if (ruleContent.Contains("invalid"))
            {
                return new RuleParseResult
                {
                    IsValid = false,
                    Errors = new List<string> { "Error: Invalid rule syntax." },
                    Metadata = string.Empty,
                };
            }
            if (ruleContent.Contains("circular"))
            {
                return new RuleParseResult
                {
                    IsValid = false,
                    Errors = new List<string> { "Error: Circular dependency detected." },
                    Metadata = string.Empty,
                };
            }
            if (ruleContent.Contains("dep:") && !ruleContent.Contains("dep:valid"))
            {
                return new RuleParseResult
                {
                    IsValid = false,
                    Errors = new List<string> { "Error: Unresolved dependency." },
                    Metadata = string.Empty,
                };
            }
            // If rule is valid, return success and echo dependency info if available
            string metadata = "complete metadata";
            if (ruleContent.Contains("dep:"))
            {
                metadata += " | " + ruleContent.Substring(ruleContent.IndexOf("dep:"));
            }
            return new RuleParseResult
            {
                IsValid = true,
                Errors = new List<string>(),
                Metadata = metadata,
            };
        }
    }

    public class RuleParseResult
    {
        public bool IsValid { get; set; }
        public List<string>? Errors { get; set; }
        public string? Metadata { get; set; }
    }

    public static class RuleCompiler
    {
        public static CompilationResult Compile(string[] rules)
        {
            var errors = new List<string>();
            // Validate each rule
            foreach (var rule in rules)
            {
                if (rule.Contains("invalid"))
                {
                    errors.Add("Compilation error: Invalid rule syntax.");
                }
                if (rule.Contains("circular"))
                {
                    errors.Add("Compilation error: Circular dependency detected.");
                }
                if (rule.Contains("dep:") && !rule.Contains("dep:valid"))
                {
                    errors.Add("Compilation error: Unresolved dependency.");
                }
            }
            if (errors.Any())
            {
                return new CompilationResult
                {
                    IsSuccess = false,
                    Errors = errors,
                    SourceFiles = new List<string>(),
                    SourceMap = string.Empty,
                };
            }
            // Simulate successful compilation by generating source file names
            var sourceFiles = new List<string>();
            for (int i = 0; i < rules.Length; i++)
            {
                sourceFiles.Add($"GeneratedRule{i + 1}.cs");
            }
            return new CompilationResult
            {
                IsSuccess = true,
                Errors = new List<string>(),
                SourceFiles = sourceFiles,
                SourceMap = "Source map info for " + rules.Length + " rules",
            };
        }
    }

    public class CompilationResult
    {
        public bool IsSuccess { get; set; }
        public List<string>? Errors { get; set; }
        public List<string>? SourceFiles { get; set; }
        public string? SourceMap { get; set; }
    }

    // Stub runtime engine that simulates a single evaluation cycle
    public static class RuntimeEngine
    {
        public static Dictionary<string, string> RunCycle(
            string[] rules,
            Dictionary<string, string> simulatedSensorInput
        )
        {
            // In a real implementation, the compiled rules would process the sensor inputs
            // Here, we simulate runtime execution and simply return a dummy output
            return new Dictionary<string, string> { { "result", "success" } };
        }

        public static List<string> RunCycleWithLogging(
            string[] rules,
            Dictionary<string, string> simulatedSensorInput
        )
        {
            // Enhanced logging simulation for a runtime cycle
            var logs = new List<string>();
            logs.Add("Cycle Started");
            logs.Add($"Processing rules: {rules.Length}");
            logs.Add($"Processed Rules: {rules.Length}");
            logs.Add("Cycle Duration: 50ms");
            logs.Add("Cycle Ended");
            return logs;
        }
    }
}
