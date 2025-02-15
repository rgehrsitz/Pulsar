// File: Pulsar.Tests/Parsing/RuleParsingTests.cs

using System;
using System.Collections.Generic;
using Xunit;
using Pulsar.Tests.TestUtilities;
using Serilog;
using Pulsar.Compiler;

namespace Pulsar.Tests.Parsing
{
    public class RuleParsingTests
    {
        private readonly ILogger _logger;

        public RuleParsingTests()
        {
            _logger = LoggingConfig.GetLogger();
        }

        [Fact]
        public void Parsing_ValidRule_Succeeds()
        {
            _logger.Debug("Starting valid rule parsing test");

            // Arrange
            var ruleContent = @"
                name: TestRule
                description: A test rule
                conditions:
                  - sensor: temp
                    operator: '>'
                    value: 30
                actions:
                  - type: setValue
                    key: alert
                    value: 1";

            // Act
            var result = RuleParser.Parse(ruleContent);

            // Assert
            Assert.True(result.IsValid);
            Assert.NotNull(result.Rule);
            Assert.Empty(result.Errors);

            _logger.Debug("Valid rule parsing test completed successfully");
        }

        [Fact]
        public void Parsing_InvalidRule_ReturnsErrors()
        {
            _logger.Debug("Starting invalid rule parsing test");

            // Arrange
            var ruleContent = "invalid rule content";

            // Act
            var result = RuleParser.Parse(ruleContent);

            // Assert
            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Errors);
            Assert.Null(result.Rule);

            _logger.Debug("Invalid rule parsing test completed with expected errors");
        }
    }
}
