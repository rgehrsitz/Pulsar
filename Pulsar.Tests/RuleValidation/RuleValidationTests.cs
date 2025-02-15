// File: Pulsar.Tests/RuleValidation/RuleValidationTests.cs

using System;
using System.Collections.Generic;
using Xunit;
using Pulsar.Tests.TestUtilities; // Updated namespace

namespace Pulsar.Tests.RuleValidation
{
    public class RuleValidationTests
    {
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
    }
}
