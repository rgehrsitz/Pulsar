// File: Pulsar.Tests/Compilation/CompilationTests.cs

using System;
using System.Collections.Generic;
using Xunit;
using Pulsar.Tests.TestUtilities; // Updated namespace

namespace Pulsar.Tests.Compilation
{
    public class CompilationTests
    {
        [Fact]
        public void Compilation_Succeeds_WithValidRules()
        {
            // Arrange: Provide a sample valid rule input
            string[] rules = new string[] { "valid rule content" };

            // Act: Call the rule compiler
            var result = RuleCompiler.Compile(rules);

            // Assert: Validate that compilation succeeds
            Assert.True(result.IsSuccess, "Expected compilation to succeed with valid rules.");
            Assert.NotNull(result.SourceFiles);
            Assert.NotEmpty(result.SourceFiles);
            Assert.False(string.IsNullOrEmpty(result.SourceMap));
        }

        [Fact]
        public void Compilation_Fails_WithErrors()
        {
            // Arrange: Provide a sample invalid rule input
            string[] rules = new string[] { "invalid rule content" };

            // Act: Compile the rules
            var result = RuleCompiler.Compile(rules);

            // Assert: Validate that compilation fails and produces detailed errors
            Assert.False(result.IsSuccess, "Expected compilation to fail with invalid rules.");
            Assert.NotNull(result.Errors);
            Assert.NotEmpty(result.Errors);
        }
    }
}
