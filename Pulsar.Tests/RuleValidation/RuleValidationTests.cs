using System;
using System.Collections.Generic;
using Xunit;
using Pulsar.Compiler.Models;
using Serilog;
using Pulsar.Compiler;
using Pulsar.Compiler.Core;

namespace Pulsar.Tests.RuleValidation
{
    public class RuleValidationTests
    {
        private readonly ILogger _logger;

        public RuleValidationTests()
        {
            _logger = Pulsar.Tests.TestUtilities.LoggingConfig.GetLogger();
        }

        [Fact]
        public void DetailedErrorProduced_ForMissingMandatoryFields()
        {
            // Arrange: Create a rule missing mandatory fields
            var rule = new RuleDefinition
            {
                // Intentionally leave required fields empty
            };

            // Act: Validate the rule
            var result = RuleValidator.Validate(rule);

            // Assert: Expect validation to fail with detailed errors
            Assert.False(result.IsValid, "Validation should fail for incomplete rules.");
            Assert.NotNull(result.Errors);
            Assert.NotEmpty(result.Errors);
            Assert.Contains(
                "Rule name cannot be empty",
                result.Errors[0],
                StringComparison.OrdinalIgnoreCase
            );
        }

        [Fact]
        public void ValidationSucceeds_ForValidRuleFormat()
        {
            // Arrange: Provide a well-formed rule
            var rule = new RuleDefinition
            {
                Name = "TestRule",
                Description = "A test rule with all mandatory fields",
                Conditions = new ConditionGroup(),
                Actions = new List<ActionDefinition>
                {
                    new SetValueAction { Key = "output", Value = 1.0 }
                }
            };

            // Act: Validate the rule
            var result = RuleValidator.Validate(rule);

            // Assert: Expect validation to succeed
            Assert.True(result.IsValid, "Validation should succeed for a complete rule.");
            Assert.Empty(result.Errors);
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
            var result = RuleValidator.Validate(rule);

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
            var result = RuleValidator.Validate(rule);

            // Assert
            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Errors);

            _logger.Debug("Empty rule validation test completed successfully");
        }
    }
}
