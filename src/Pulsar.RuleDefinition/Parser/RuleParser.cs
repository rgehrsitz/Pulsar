using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Pulsar.RuleDefinition.Models;
using Pulsar.RuleDefinition.Models.Actions;
using Pulsar.RuleDefinition.Models.Conditions;
using Serilog;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Pulsar.RuleDefinition.Parser;

/// <summary>
/// Provides functionality to parse rule definition YAML files into strongly-typed objects
/// </summary>
public class RuleParser
{
    private readonly ILogger _logger;
    private readonly YamlDotNet.Serialization.IDeserializer _deserializer;
    private readonly Pulsar.RuleDefinition.Models.SystemConfig _config;

    public RuleParser(Pulsar.RuleDefinition.Models.SystemConfig config)
    {
        _config = config;
        _logger = Log.ForContext<RuleParser>();
        _deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
            .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.UnderscoredNamingConvention.Instance)
            .WithTypeConverter(new Pulsar.RuleDefinition.Parser.ConditionTypeConverter())
            .WithTypeConverter(new Pulsar.RuleDefinition.Parser.ActionTypeConverter())
            .Build();
    }

    /// <summary>
    /// Parses a YAML string containing rule definitions into a RuleSetDefinition object
    /// </summary>
    /// <param name="yamlContent">The YAML content to parse</param>
    /// <returns>A RuleSetDefinition object representing the parsed rules</returns>
    /// <exception cref="System.ArgumentException">If the YAML is invalid or missing required fields</exception>
    public Pulsar.RuleDefinition.Models.RuleSetDefinition ParseRules(string yamlContent)
    {
        if (string.IsNullOrWhiteSpace(yamlContent))
        {
            throw new System.ArgumentException("YAML content cannot be empty");
        }

        try
        {
            var result = _deserializer.Deserialize<Pulsar.RuleDefinition.Models.RuleSetDefinition>(yamlContent);
            if (result == null)
            {
                throw new System.ArgumentException(
                    "Invalid YAML: failed to deserialize to RuleSetDefinition"
                );
            }

            // Validate required fields
            if (result.Rules == null || !result.Rules.Any())
            {
                throw new System.ArgumentException("Rules section is missing or empty");
            }

            // Validate each rule
            foreach (var rule in result.Rules)
            {
                ValidateRuleDefinition(rule);
            }

            _logger.Debug("Successfully parsed {RuleCount} rules", result.Rules.Count);
            return result;
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            _logger.Error(ex, "Failed to parse rules YAML");
            throw new System.ArgumentException($"Invalid YAML format: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Parses a YAML file containing rule definitions into a RuleSetDefinition object
    /// </summary>
    /// <param name="filePath">Path to the YAML file</param>
    /// <returns>A RuleSetDefinition object representing the parsed rules</returns>
    public Pulsar.RuleDefinition.Models.RuleSetDefinition ParseRulesFromFile(string filePath)
    {
        if (!System.IO.File.Exists(filePath))
        {
            throw new System.IO.FileNotFoundException($"Rule file not found: {filePath}");
        }

        _logger.Debug("Reading rules from file: {FilePath}", filePath);
        var yamlContent = System.IO.File.ReadAllText(filePath);
        return ParseRules(yamlContent);
    }

    private void ValidateRuleDefinition(Pulsar.RuleDefinition.Models.RuleDefinitionModel rule)
    {
        if (string.IsNullOrWhiteSpace(rule.Name))
        {
            throw new System.ArgumentException("Rule name is required");
        }

        ValidateConditionGroup(rule.Conditions, rule.Name);
    }

    private void ValidateConditionGroup(Pulsar.RuleDefinition.Models.ConditionGroupDefinition? group, string ruleName)
    {
        if (group == null)
        {
            throw new ArgumentException($"Rule '{ruleName}' has no conditions");
        }

        if ((group.All == null || !group.All.Any()) && (group.Any == null || !group.Any.Any()))
        {
            throw new ArgumentException($"Rule '{ruleName}' has no conditions");
        }

        var conditions = group.All ?? group.Any;
        foreach (var wrapper in conditions!)
        {
            ValidateCondition(wrapper.Condition, ruleName);
        }
    }

    private void ValidateCondition(Condition condition, string ruleName)
    {
        switch (condition)
        {
            case ComparisonConditionDefinition comparison:
                ValidateComparisonCondition(comparison, ruleName);
                break;
            case ThresholdOverTimeConditionDefinition threshold:
                ValidateThresholdOverTimeCondition(threshold, ruleName);
                break;
            case ExpressionConditionDefinition expression:
                ValidateExpressionCondition(expression, ruleName);
                break;
            default:
                throw new ArgumentException($"Unknown condition type: {condition.GetType().Name}");
        }
    }

    private void ValidateAction(RuleAction action, string ruleName)
    {
        if (action.SetValue != null)
        {
            ValidateSetValueAction(action.SetValue, ruleName);
        }
        else if (action.SendMessage != null)
        {
            ValidateSendMessageAction(action.SendMessage, ruleName);
        }
        else
        {
            throw new ArgumentException($"Rule '{ruleName}' has an action with no type specified");
        }
    }

    private void ValidateSetValueAction(SetValueAction action, string ruleName)
    {
        if (string.IsNullOrWhiteSpace(action.Key))
        {
            throw new ArgumentException($"Rule '{ruleName}' has a set_value action with no key specified");
        }

        if (action.Value == null && string.IsNullOrWhiteSpace(action.ValueExpression))
        {
            throw new ArgumentException(
                $"Rule '{ruleName}' has a set_value action with neither value nor value_expression specified"
            );
        }
    }

    private void ValidateSendMessageAction(SendMessageAction action, string ruleName)
    {
        if (string.IsNullOrWhiteSpace(action.Channel))
        {
            throw new ArgumentException(
                $"Rule '{ruleName}' has a send_message action with no channel specified"
            );
        }

        if (string.IsNullOrWhiteSpace(action.Message))
        {
            throw new ArgumentException(
                $"Rule '{ruleName}' has a send_message action with no message specified"
            );
        }
    }

    private void ValidateComparisonCondition(ComparisonConditionDefinition condition, string ruleName)
    {
        if (string.IsNullOrWhiteSpace(condition.DataSource))
        {
            throw new System.ArgumentException(
                $"Rule '{ruleName}' has comparison condition with missing sensor"
            );
        }

        if (!_config.ValidSensors.Contains(condition.DataSource))
        {
            throw new System.ArgumentException(
                $"Rule '{ruleName}' references invalid sensor: {condition.DataSource}"
            );
        }

        if (string.IsNullOrWhiteSpace(condition.Operator))
        {
            throw new System.ArgumentException(
                $"Rule '{ruleName}' has comparison condition with missing operator"
            );
        }

        if (!IsValidOperator(condition.Operator))
        {
            throw new System.ArgumentException(
                $"Rule '{ruleName}' has invalid comparison operator: {condition.Operator}"
            );
        }
    }

    private void ValidateThresholdOverTimeCondition(
        ThresholdOverTimeConditionDefinition condition,
        string ruleName
    )
    {
        if (string.IsNullOrWhiteSpace(condition.DataSource))
        {
            throw new System.ArgumentException(
                $"Rule '{ruleName}' has threshold condition with missing sensor"
            );
        }

        if (!_config.ValidSensors.Contains(condition.DataSource))
        {
            throw new System.ArgumentException(
                $"Rule '{ruleName}' references invalid sensor: {condition.DataSource}"
            );
        }

        if (condition.Duration.TotalMilliseconds <= 0)
        {
            throw new System.ArgumentException(
                $"Rule '{ruleName}' has threshold condition with invalid duration: {condition.Duration.TotalMilliseconds}ms"
            );
        }
    }

    private void ValidateExpressionCondition(ExpressionConditionDefinition condition, string ruleName)
    {
        if (string.IsNullOrWhiteSpace(condition.Expression))
        {
            throw new System.ArgumentException(
                $"Rule '{ruleName}' has expression condition with missing expression"
            );
        }

        // TODO: Add expression validation
        // This would validate:
        // 1. Expression syntax
        // 2. Referenced sensors exist in _config.ValidSensors
        // 3. Operators and functions are valid
    }

    private bool IsValidOperator(string op)
    {
        return op switch
        {
            ">" or ">=" or "<" or "<=" or "==" or "!=" => true,
            _ => false
        };
    }
}
