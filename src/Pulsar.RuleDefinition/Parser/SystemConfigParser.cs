using System;
using System.IO;
using System.Linq;
using Pulsar.RuleDefinition.Models;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Pulsar.RuleDefinition.Parser;

/// <summary>
/// Parser for system configuration files
/// </summary>
public class SystemConfigParser
{
    private readonly IDeserializer _deserializer;

    public SystemConfigParser()
    {
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
    }

    /// <summary>
    /// Parse a system configuration file from a string
    /// </summary>
    /// <param name="yaml">YAML string containing system configuration</param>
    /// <returns>Parsed SystemConfig object</returns>
    /// <exception cref="ArgumentException">If the YAML is invalid or missing required fields</exception>
    public SystemConfig Parse(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            throw new ArgumentException("YAML content cannot be empty");
        }

        try
        {
            var config = _deserializer.Deserialize<SystemConfig>(yaml);
            if (config == null)
            {
                throw new ArgumentException("Invalid YAML: failed to deserialize to SystemConfig");
            }

            // Validate the config immediately after deserialization
            if (config.Version <= 0)
                throw new ArgumentException("Version must be a positive integer");

            if (config.ValidSensors == null || config.ValidSensors.Count == 0)
                throw new ArgumentException("Valid sensors list cannot be empty");

            foreach (var sensor in config.ValidSensors)
            {
                if (string.IsNullOrWhiteSpace(sensor))
                    throw new ArgumentException("Sensor names cannot be empty or whitespace");
            }

            // Check for duplicate sensors
            var duplicates = config
                .ValidSensors.GroupBy(x => x)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);

            if (duplicates.Any())
                throw new ArgumentException(
                    $"Duplicate sensor names found: {string.Join(", ", duplicates)}"
                );

            return config;
        }
        catch (YamlException ex)
        {
            throw new ArgumentException($"Invalid YAML format: {ex.Message}", ex);
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Invalid YAML content: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Parse a system configuration file from a file path
    /// </summary>
    /// <param name="filePath">Path to the YAML file</param>
    /// <returns>Parsed SystemConfig object</returns>
    /// <exception cref="ArgumentException">If the file is invalid or missing required fields</exception>
    /// <exception cref="FileNotFoundException">If the file does not exist</exception>
    public SystemConfig ParseFile(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"System configuration file not found: {filePath}");

        var yaml = File.ReadAllText(filePath);
        return Parse(yaml);
    }
}
