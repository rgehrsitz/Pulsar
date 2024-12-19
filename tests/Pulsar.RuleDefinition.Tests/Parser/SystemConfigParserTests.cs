using System;
using System.IO;
using Pulsar.RuleDefinition.Parser;
using Xunit;

namespace Pulsar.RuleDefinition.Tests.Parser;

public class SystemConfigParserTests
{
    private readonly SystemConfigParser _parser;

    public SystemConfigParserTests()
    {
        _parser = new SystemConfigParser();
    }

    [Fact]
    public void Parse_ValidConfig_ReturnsConfig()
    {
        // Arrange
        var yaml = @"
version: 1
valid_sensors:
  - temperature
  - humidity
  - pressure";

        // Act
        var config = _parser.Parse(yaml);

        // Assert
        Assert.Equal(1, config.Version);
        Assert.Equal(3, config.ValidSensors.Count);
        Assert.Contains("temperature", config.ValidSensors);
        Assert.Contains("humidity", config.ValidSensors);
        Assert.Contains("pressure", config.ValidSensors);
    }

    [Fact]
    public void Parse_InvalidYaml_ThrowsException()
    {
        // Arrange
        var yaml = @"
version: 1
valid_sensors:
  - temperature
  humidity";  // Invalid YAML indentation

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _parser.Parse(yaml));
    }

    [Fact]
    public void Parse_MissingVersion_ThrowsException()
    {
        // Arrange
        var yaml = @"
valid_sensors:
  - temperature
  - humidity";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _parser.Parse(yaml));
    }

    [Fact]
    public void Parse_InvalidVersion_ThrowsException()
    {
        // Arrange
        var yaml = @"
version: 0
valid_sensors:
  - temperature
  - humidity";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _parser.Parse(yaml));
    }

    [Fact]
    public void Parse_MissingValidSensors_ThrowsException()
    {
        // Arrange
        var yaml = @"
version: 1";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _parser.Parse(yaml));
    }

    [Fact]
    public void Parse_EmptySensorName_ThrowsException()
    {
        // Arrange
        var yaml = @"
version: 1
valid_sensors:
  - temperature
  - """"
  - humidity";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _parser.Parse(yaml));
    }

    [Fact]
    public void ParseFile_ValidFile_ReturnsConfig()
    {
        // Arrange
        var filePath = Path.Combine("TestData", "system_config.yaml");

        // Act
        var config = _parser.ParseFile(filePath);

        // Assert
        Assert.Equal(1, config.Version);
        Assert.Contains("temperature", config.ValidSensors);
        Assert.Contains("humidity", config.ValidSensors);
        Assert.Contains("pressure", config.ValidSensors);
    }

    [Fact]
    public void ParseFile_FileNotFound_ThrowsException()
    {
        // Arrange
        var filePath = "nonexistent.yaml";

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => _parser.ParseFile(filePath));
    }
}
