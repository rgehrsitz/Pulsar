// File: Pulsar.Tests/CompilerTests/BuildTimeOrchestratorTests.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Moq;
using Pulsar.Compiler.Models;
using Serilog;
using Xunit;
using Xunit.Abstractions;
using Pulsar.Compiler.Build;
using Pulsar.Compiler.Parsers;
using System.Linq;

namespace Pulsar.Tests.CompilerTests
{
  public class BuildTimeOrchestratorTests : IDisposable
  {
    private readonly string _testRulesDir;
    private readonly string _testOutputDir;
    private readonly Mock<ILogger> _loggerMock;
    private readonly ITestOutputHelper _output;
    private readonly BuildTimeOrchestrator _orchestrator;
    private readonly DslParser _parser;
    private readonly ICodeGenerator _codeGenerator;

    public BuildTimeOrchestratorTests(ITestOutputHelper output)
    {
      _output = output;
      _testRulesDir = Path.Combine(Path.GetTempPath(), $"PulsarTestRules_{Guid.NewGuid()}");
      _testOutputDir = Path.Combine(Path.GetTempPath(), $"PulsarTestOutput_{Guid.NewGuid()}");
      Directory.CreateDirectory(_testRulesDir);
      Directory.CreateDirectory(_testOutputDir);

      _loggerMock = new Mock<ILogger>();
      _parser = new DslParser();
      _codeGenerator = new DefaultCodeGenerator();

      var systemConfig = new SystemConfig
      {
        ValidSensors = new List<string>
                {
                    "temperature_f",
                    "temperature_c",
                    "humidity",
                    "pressure"
                }
      };

      var buildConfig = new BuildConfig
      {
        MaxRulesPerFile = 2,
        MaxLinesPerFile = 100,
        GroupParallelRules = true
      };

      _orchestrator = new BuildTimeOrchestrator(
          _loggerMock.Object,
          systemConfig,
          buildConfig,
          _codeGenerator,
          _parser);
    }

    public void Dispose()
    {
      try
      {
        if (Directory.Exists(_testRulesDir))
          Directory.Delete(_testRulesDir, true);
        if (Directory.Exists(_testOutputDir))
          Directory.Delete(_testOutputDir, true);
      }
      catch (Exception ex)
      {
        _output.WriteLine($"Warning: Cleanup failed: {ex.Message}");
      }
    }

    [Fact]
    public async Task ProcessRulesDirectory_SingleValidRule_GeneratesCorrectOutput()
    {
      // Arrange
      var ruleContent = @"
rules:
  - name: 'TemperatureConversion'
    conditions:
      all:
        - condition:
            type: comparison
            sensor: 'temperature_f'
            operator: '>'
            value: -459.67
    actions:
      - set_value:
          key: 'temperature_c'
          value_expression: '(temperature_f - 32) * 5/9'";

      var rulePath = Path.Combine(_testRulesDir, "temp_conversion.yaml");
      await File.WriteAllTextAsync(rulePath, ruleContent);

      // Act
      var result = await _orchestrator.ProcessRulesDirectory(_testRulesDir, _testOutputDir);

      // Assert
      Assert.NotNull(result);
      Assert.NotEmpty(result.GeneratedFiles);
      Assert.Contains(result.GeneratedFiles, f => Path.GetFileName(f).Contains("RuleGroup"));
      Assert.Contains(result.GeneratedFiles, f => Path.GetFileName(f) == "rules.manifest.json");

      // Verify manifest contains rule info
      Assert.Single(result.Manifest.Rules);
      Assert.Contains("TemperatureConversion", result.Manifest.Rules.Keys);

      // Verify file content
      var ruleFile = result.GeneratedFiles.FirstOrDefault(f => f.Contains("TemperatureConversion"));
      Assert.NotNull(ruleFile);
      var content = await File.ReadAllTextAsync(ruleFile);
      Assert.Contains("temperature_f", content);
      Assert.Contains("temperature_c", content);
    }

    [Fact]
    public async Task ProcessRulesDirectory_MultipleRules_GroupsCorrectly()
    {
      // Arrange
      var ruleContent = @"
rules:
  - name: 'Rule1'
    conditions:
      all:
        - condition:
            type: comparison
            sensor: 'temperature_f'
            operator: '>'
            value: 100
    actions:
      - set_value:
          key: 'temperature_c'
          value: 1
  - name: 'Rule2'
    conditions:
      all:
        - condition:
            type: comparison
            sensor: 'humidity'
            operator: '>'
            value: 80
    actions:
      - set_value:
          key: 'pressure'
          value: 1
  - name: 'Rule3'
    conditions:
      all:
        - condition:
            type: comparison
            sensor: 'pressure'
            operator: '<'
            value: 1000
    actions:
      - set_value:
          key: 'temperature_c'
          value: 0";

      var rulePath = Path.Combine(_testRulesDir, "multiple_rules.yaml");
      await File.WriteAllTextAsync(rulePath, ruleContent);

      // Act
      var result = await _orchestrator.ProcessRulesDirectory(_testRulesDir, _testOutputDir);

      // Assert
      Assert.NotNull(result);

      // Should have multiple rule group files due to MaxRulesPerFile = 2
      var ruleGroups = result.GeneratedFiles.Where(f => Path.GetFileName(f).Contains("RuleGroup")).ToList();
      Assert.Equal(2, ruleGroups.Count); // 3 rules split into 2 groups

      // Verify manifest
      Assert.Equal(3, result.Manifest.Rules.Count);
      Assert.Contains("Rule1", result.Manifest.Rules.Keys);
      Assert.Contains("Rule2", result.Manifest.Rules.Keys);
      Assert.Contains("Rule3", result.Manifest.Rules.Keys);
    }

    [Fact]
    public async Task ProcessRulesDirectory_InvalidRule_ThrowsValidationException()
    {
      // Arrange
      var invalidRuleContent = @"
rules:
  - name: 'InvalidRule'
    conditions:
      all:
        - condition:
            type: comparison
            sensor: 'invalid_sensor'
            operator: '>'
            value: 100
    actions:
      - set_value:
          key: 'output'
          value: 1";

      var rulePath = Path.Combine(_testRulesDir, "invalid_rule.yaml");
      await File.WriteAllTextAsync(rulePath, invalidRuleContent);

      // Act & Assert
      var ex = await Assert.ThrowsAsync<InvalidOperationException>(
          () => _orchestrator.ProcessRulesDirectory(_testRulesDir, _testOutputDir)
      );
      Assert.Contains("invalid_sensor", ex.Message);

      // Updated to match actual logger call
      _loggerMock.Verify(
          x => x.Error(
              It.IsAny<Exception>(),
              It.Is<string>(msg => msg.Contains("Invalid sensors or keys found in {File}")),
              It.Is<string>(path => path.Contains("invalid_rule.yaml"))
          ),
          Times.Once
      );
    }
  }
}