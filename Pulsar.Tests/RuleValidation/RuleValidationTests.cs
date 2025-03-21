// File: Pulsar.Tests/RuleValidation/RuleValidationTests.cs

using Pulsar.Compiler.Core;
using Pulsar.Compiler.Models;
using Serilog;

namespace Pulsar.Tests.RuleValidation
{
    public class RuleValidationTests
    {
        private readonly ILogger _logger = Pulsar.Tests.TestUtilities.LoggingConfig.ToSerilogLogger(
            Pulsar.Tests.TestUtilities.LoggingConfig.GetLogger()
        );

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
            var rule = new RuleDefinition
            {
                Name = "ValidRule",
                Conditions = new ConditionGroup // Changed from 'Conditions' to 'ConditionGroup'
                {
                    All = new List<ConditionDefinition>
                    {
                        new ComparisonCondition
                        {
                            Sensor = "Temperature",
                            Operator = ComparisonOperator.GreaterThan, // Changed operator from string to enum value
                            Value = 20,
                        },
                    },
                    Any = new List<ConditionDefinition>(),
                },
                Actions = new List<ActionDefinition>
                {
                    new SetValueAction { ValueExpression = "Temperature" },
                },
            };

            var analyzer = new DependencyAnalyzer();
            var result = analyzer.ValidateDependencies(new List<RuleDefinition> { rule });
            Assert.True(result.IsValid, "Expected validation to succeed for complete rule format.");
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
                Conditions = new ConditionGroup
                {
                    All = new List<ConditionDefinition>
                    {
                        new ComparisonCondition
                        {
                            Sensor = "Dummy",
                            Operator = ComparisonOperator.EqualTo, // Updated from 'Equals'
                            Value = 1,
                        },
                    },
                },
                Actions = new List<ActionDefinition>
                {
                    new SetValueAction { Key = "output", Value = 1.0 },
                },
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
