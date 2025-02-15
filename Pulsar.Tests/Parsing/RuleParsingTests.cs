// File: Pulsar.Tests/Parsing/RuleParsingTests.cs

using System;
using System.Collections.Generic;
using Xunit;
using Pulsar.Tests.TestUtilities; // Updated namespace

namespace Pulsar.Tests.Parsing
{
    public class RuleParsingTests
    {
        [Fact]
        public void ValidRuleParsing_SucceedsWithCompleteMetadata()
        {
            // Arrange: Provide a sample valid rule input
            string ruleContent = "sample valid rule content";

            // Act: Call the rule parser
            var result = RuleParser.Parse(ruleContent);

            // Assert: Validate the result
            Assert.NotNull(result);
            Assert.True(result.IsValid, "Expected rule parsing result to be valid.");
            Assert.Equal("complete metadata", result.Metadata);
        }

        [Fact]
        public void InvalidRuleParsing_ProducesDetailedErrors()
        {
            // Arrange: Provide a sample invalid rule input
            string ruleContent = "invalid rule content";

            // Act: Call the rule parser
            var result = RuleParser.Parse(ruleContent);

            // Assert: Validate that errors are detailed
            Assert.False(result.IsValid, "Expected rule parsing result to be invalid.");
            Assert.NotNull(result.Errors);
            Assert.NotEmpty(result.Errors);
        }

        [Fact]
        public void ComplexRuleParsing_HandlesNestedConditions()
        {
            // Arrange: Provide a sample rule with nested conditions
            string ruleContent = "complex rule with nested conditions";

            // Act: Parse the rule
            var result = RuleParser.Parse(ruleContent);

            // Assert: Validate that nested conditions are properly handled
            Assert.True(result.IsValid, "Expected complex rule parsing to succeed.");
            Assert.Contains("nested", result.Metadata);
        }
    }
}
