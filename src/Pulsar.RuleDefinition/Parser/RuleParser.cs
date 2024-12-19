using System;
using System.IO;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Pulsar.RuleDefinition.Models;

namespace Pulsar.RuleDefinition.Parser;

/// <summary>
/// Provides functionality to parse rule definition YAML files into strongly-typed objects
/// </summary>
public class RuleParser
{
    private readonly IDeserializer _deserializer;

    public RuleParser()
    {
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .WithTypeConverter(new ConditionTypeConverter())
            .Build();
    }

    /// <summary>
    /// Parses a YAML string containing rule definitions into a RuleSetDefinition object
    /// </summary>
    /// <param name="yamlContent">The YAML content to parse</param>
    /// <returns>A RuleSetDefinition object representing the parsed rules</returns>
    /// <exception cref="ArgumentException">If the YAML is invalid or missing required fields</exception>
    public RuleSetDefinition ParseRules(string yamlContent)
    {
        if (string.IsNullOrWhiteSpace(yamlContent))
        {
            throw new ArgumentException("YAML content cannot be empty");
        }

        try
        {
            var result = _deserializer.Deserialize<RuleSetDefinition>(yamlContent);
            if (result == null)
            {
                throw new ArgumentException("Invalid YAML: failed to deserialize to RuleSetDefinition");
            }
            return result;
        }
        catch (YamlException ex)
        {
            throw new ArgumentException($"Invalid YAML format: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Invalid YAML content: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Parses a YAML file containing rule definitions into a RuleSetDefinition object
    /// </summary>
    /// <param name="filePath">Path to the YAML file</param>
    /// <returns>A RuleSetDefinition object representing the parsed rules</returns>
    /// <exception cref="FileNotFoundException">If the file doesn't exist</exception>
    /// <exception cref="ArgumentException">If the YAML is invalid or missing required fields</exception>
    public RuleSetDefinition ParseRulesFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Rule definition file not found", filePath);
        }

        var yamlContent = File.ReadAllText(filePath);
        return ParseRules(yamlContent);
    }
}

/// <summary>
/// Exception thrown when rule parsing fails
/// </summary>
public class RuleParsingException : Exception
{
    public RuleParsingException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
