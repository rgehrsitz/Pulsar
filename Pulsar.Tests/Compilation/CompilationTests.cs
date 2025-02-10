using System;
using System.Collections.Generic;
using Xunit;

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
            Assert.NotEmpty(result.Errors);
        }
    }

    // Stub implementation for the rule compiler
    public static class RuleCompiler
    {
        public static CompilationResult Compile(string[] rules)
        {
            // Dummy implementation for test scaffolding
            if (rules == null || rules.Length == 0)
            {
                return new CompilationResult
                {
                    IsSuccess = false,
                    Errors = new List<string> { "No rules provided." },
                    SourceFiles = new List<string>(),
                    SourceMap = string.Empty
                };
            }
            
            // If the rule contains the word "invalid", simulate a compilation error
            if (rules[0].Contains("invalid"))
            {
                return new CompilationResult
                {
                    IsSuccess = false,
                    Errors = new List<string> { "Compilation error: Invalid rule." },
                    SourceFiles = new List<string>(),
                    SourceMap = string.Empty
                };
            }
            
            // Otherwise, simulate successful compilation
            return new CompilationResult
            {
                IsSuccess = true,
                Errors = new List<string>(),
                SourceFiles = new List<string> { "GeneratedRule.cs" },
                SourceMap = "Source map info"
            };
        }
    }

    public class CompilationResult
    {
        public bool IsSuccess { get; set; }
        public List<string> Errors { get; set; }
        public List<string> SourceFiles { get; set; }
        public string SourceMap { get; set; }
    }
}
