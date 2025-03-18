// File: Pulsar.Tests/RuntimeExecution/RuntimeExecutionTests.cs



using System;
using System.Collections.Generic;
using Pulsar.Compiler.Models;
using Pulsar.Tests.TestUtilities;
using Serilog;
using Serilog.Core;
using Xunit;

namespace Pulsar.Tests.RuntimeExecution
{
    public class RuntimeExecutionTests
    {
        private readonly ILogger _logger;

        public RuntimeExecutionTests()
        {
            _logger = Pulsar.Tests.TestUtilities.LoggingConfig.ToSerilogLogger(
                Pulsar.Tests.TestUtilities.LoggingConfig.GetLogger()
            );
        }

        [Fact]
        public void RuntimeExecution_ExecutesValidRuleSuccessfully()
        {
            _logger.Debug("Starting valid rule execution test");

            // Arrange: Provide a valid rule input for execution

            string ruleContent = "// valid rule execution script";

            // Act: Execute the rule

            var result = RuleRuntime.Execute(ruleContent);

            // Assert: Expect successful execution with expected output

            Assert.True(result.IsSuccess, "Expected the rule to execute successfully.");

            Assert.Equal("Execution complete", result.Output);

            _logger.Debug("Valid rule execution test completed successfully");
        }

        [Fact]
        public void RuntimeExecution_FailsForInvalidRule()
        {
            _logger.Debug("Starting invalid rule execution test");

            // Arrange: Provide an invalid rule input that should fail during execution

            string ruleContent = "// invalid rule that fails at runtime";

            // Act: Execute the rule

            var result = RuleRuntime.Execute(ruleContent);

            // Assert: Expect failure and detailed error message

            Assert.False(result.IsSuccess, "Expected the rule execution to fail.");

            Assert.NotNull(result.Errors);

            Assert.NotEmpty(result.Errors);

            Assert.Contains("runtime error", result.Errors[0], StringComparison.OrdinalIgnoreCase);

            _logger.Debug("Invalid rule execution test completed successfully");
        }
    }
}
