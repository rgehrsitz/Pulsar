// File: Pulsar.Tests/RuntimeExecution/RuntimeExecutionTests.cs

using System;
using System.Collections.Generic;
using Xunit;
using Pulsar.Tests.TestUtilities; // Updated namespace

namespace Pulsar.Tests.RuntimeExecution
{
    public class RuntimeExecutionTests
    {
        [Fact]
        public void RuntimeExecution_ExecutesValidRuleSuccessfully()
        {
            // Arrange: Provide a valid rule input for execution
            string ruleContent = "// valid rule execution script";

            // Act: Execute the rule
            var result = RuleRuntime.Execute(ruleContent);

            // Assert: Expect successful execution with expected output
            Assert.True(result.IsSuccess, "Expected the rule to execute successfully.");
            Assert.Equal("Execution complete", result.Output);
        }

        [Fact]
        public void RuntimeExecution_FailsForInvalidRule()
        {
            // Arrange: Provide an invalid rule input that should fail during execution
            string ruleContent = "// invalid rule that fails at runtime";

            // Act: Execute the rule
            var result = RuleRuntime.Execute(ruleContent);

            // Assert: Expect failure and detailed error message
            Assert.False(result.IsSuccess, "Expected the rule execution to fail.");
            Assert.NotNull(result.Errors);
            Assert.NotEmpty(result.Errors);
            Assert.Contains("runtime error", result.Errors[0], StringComparison.OrdinalIgnoreCase);
        }
    }
}
