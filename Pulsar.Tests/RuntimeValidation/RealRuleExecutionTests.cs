// File: Pulsar.Tests/RuntimeValidation/RealRuleExecutionTests.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Pulsar.Tests.RuntimeValidation
{
    [Trait("Category", "RuntimeValidation")]
    public class RealRuleExecutionTests : IClassFixture<RuntimeValidationFixture>
    {
        private readonly ITestOutputHelper _output;
        private readonly RuntimeValidationFixture _fixture;
        
        public RealRuleExecutionTests(ITestOutputHelper output, RuntimeValidationFixture fixture)
        {
            _output = output;
            _fixture = fixture;
            fixture.Logger.LogInformation("RealRuleExecutionTests initialized");
        }
        
        [Fact]
        public void DummyTest_Always_Succeeds()
        {
            // This is just a placeholder test to ensure the infrastructure is working
            Assert.True(true);
        }
        
        [Fact]
        public async Task SimpleRule_ValidInput_ParsesCorrectly()
        {
            // For our initial validation test, let's focus on validating that the rule parsing works correctly
            // We'll test the build process separately once we get this part working
            
            // Arrange
            var ruleFile = Path.Combine(Directory.GetCurrentDirectory(), "RuntimeValidation", "simple-rule.yaml");
            _fixture.Logger.LogInformation("Using rule file: {RuleFile}", ruleFile);
            Assert.True(File.Exists(ruleFile), $"Rule file should exist at {ruleFile}");
            
            // Create a parser directly to test parsing
            var parser = new Pulsar.Compiler.Parsers.DslParser();
            var validSensors = new List<string> { "input:a", "input:b", "input:c", "output:sum", "output:complex" };
            
            // Act
            var content = await File.ReadAllTextAsync(ruleFile);
            var rules = parser.ParseRules(content, validSensors, Path.GetFileName(ruleFile));
            
            // Assert
            Assert.NotEmpty(rules);
            _fixture.Logger.LogInformation("Successfully parsed {Count} rules", rules.Count);
            
            // Validate the first rule has the expected structure
            var rule = rules.First();
            Assert.NotNull(rule);
            Assert.NotNull(rule.Name);
            Assert.NotNull(rule.Conditions);
            Assert.NotEmpty(rule.Actions);
            
            // Log the rule structure
            _fixture.Logger.LogInformation("Rule name: {Name}", rule.Name);
            _fixture.Logger.LogInformation("Rule description: {Description}", rule.Description);
            
            _fixture.Logger.LogInformation("Test completed successfully");
        }
    }
}