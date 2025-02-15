// File: Pulsar.Tests/RuleValidation/RuleValidationTests.cs

using System;
using System.Collections.Generic;
using Xunit;
using Pulsar.Tests.TestUtilities; // Updated namespace
using Pulsar.Compiler.Models;
using Serilog;

namespace Pulsar.Tests.RuleValidation
{
    public class RuleValidationTests
    {
        private readonly ILogger _logger;

        public RuleValidationTests()
        {
            _logger = LoggingConfig.GetLogger();
        }

        [Fact]
        public void DetailedErrorProduced_ForMissingMandatoryFields()
        {
            // Arrange: Create a rule input missing mandatory fields
            string ruleContent = "// rule missing mandatory fields";

            // Act: Validate the rule
            var result = RuleValidator.Validate(ruleContent);

            // Assert: Expect validation to fail with detailed errors
            Assert.False(result.IsValid, "Validation should fail for incomplete rules.");
            Assert.NotNull(result.Errors);
            Assert.NotEmpty(result.Errors);
            Assert.Contains(
                "missing mandatory",
                result.Errors[0],
                StringComparison.OrdinalIgnoreCase
            );
        }

        [Fact]
        public void MetadataExtracted_ForValidRuleFormat()
        {
            // Arrange: Provide a well-formed rule input
            string ruleContent = "// valid rule with all mandatory fields and extra metadata";

            // Act: Validate the rule
            var result = RuleValidator.Validate(ruleContent);

            // Assert: Expect validation to succeed and metadata to be extracted
            Assert.True(result.IsValid, "Validation should succeed for a complete rule.");
            Assert.False(string.IsNullOrEmpty(result.Metadata));
        }

        [Fact]
        public void Validation_ValidRule_Succeeds()
        {
            _logger.Debug("Running ValidRule validation test");
            
            // Arrange
            var rule = new RuleDefinition
            {
                Name = "TestRule",
                Description = "A valid test rule",
                Conditions = new ConditionGroup(),
                Actions = new List<ActionDefinition>
                {
                    new SetValueAction { Key = "output", Value = 1.0 }
                }
            };

            // Act
            var result = TestUtilities.RuleValidator.Validate(rule);

            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
            
            _logger.Debug("Valid rule validation test completed successfully");
        }

        [Fact]
        public void Validation_EmptyRule_Fails()
        {
            _logger.Debug("Running EmptyRule validation test");
            
            // Arrange
            var rule = new RuleDefinition();

            // Act
            var result = TestUtilities.RuleValidator.Validate(rule);

            // Assert
            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Errors);
            
            _logger.Debug("Empty rule validation test completed successfully");
        }
    }
}
