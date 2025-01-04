using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Pulsar.RuleDefinition.Models;
using Pulsar.RuleDefinition.Models.Conditions;
using Pulsar.RuleDefinition.Models.Actions;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Pulsar.RuleDefinition.Parser;

/// <summary>
/// Provides functionality to parse rule definition YAML files into strongly-typed objects
/// </summary>
public class RuleParser
{
    private readonly YamlDotNet.Serialization.IDeserializer _deserializer;
    private readonly Pulsar.RuleDefinition.Models.SystemConfig _config;

    public RuleParser(Pulsar.RuleDefinition.Models.SystemConfig config)
    {
        _config = config;
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
                ValidateRule(rule);
            }

            return result;
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
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

        var yamlContent = System.IO.File.ReadAllText(filePath);
        return ParseRules(yamlContent);
    }

    private void ValidateRule(Pulsar.RuleDefinition.Models.Rule rule)
    {
        if (string.IsNullOrWhiteSpace(rule.Name))
        {
            throw new System.ArgumentException("Rule name is required");
        }

        if (rule.Conditions == null)
        {
            throw new System.ArgumentException($"Rule '{rule.Name}' is missing conditions");
        }

        if (rule.Actions == null || !rule.Actions.Any())
        {
            throw new System.ArgumentException($"Rule '{rule.Name}' is missing actions");
        }

        // Validate conditions
        ValidateConditionGroup(rule.Conditions, rule.Name);

        // Validate actions
        foreach (var action in rule.Actions)
        {
            ValidateAction(action, rule.Name);
        }
    }

    private void ValidateConditionGroup(Pulsar.RuleDefinition.Models.ConditionGroup? group, string ruleName)
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

    private void ValidateCondition(Pulsar.RuleDefinition.Models.Conditions.Condition condition, string ruleName)
    {
        switch (condition)
        {
            case Pulsar.RuleDefinition.Models.Conditions.ComparisonCondition comp:
                ValidateComparisonCondition(comp, ruleName);
                break;
            case Pulsar.RuleDefinition.Models.Conditions.ThresholdOverTimeCondition threshold:
                ValidateThresholdOverTimeCondition(threshold, ruleName);
                break;
            case Pulsar.RuleDefinition.Models.Conditions.ExpressionCondition expr:
                ValidateExpressionCondition(expr, ruleName);
                break;
            default:
                throw new System.ArgumentException(
                    $"Rule '{ruleName}' has unknown condition type: {condition.GetType().Name}"
                );
        }
    }

    private void ValidateComparisonCondition(Pulsar.RuleDefinition.Models.Conditions.ComparisonCondition condition, string ruleName)
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
        Pulsar.RuleDefinition.Models.Conditions.ThresholdOverTimeCondition condition,
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

        if (condition.DurationMs <= 0)
        {
            throw new System.ArgumentException(
                $"Rule '{ruleName}' has threshold condition with invalid duration: {condition.DurationMs}ms"
            );
        }
    }

    private void ValidateExpressionCondition(Pulsar.RuleDefinition.Models.Conditions.ExpressionCondition condition, string ruleName)
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

    private void ValidateAction(Pulsar.RuleDefinition.Models.RuleAction action, string ruleName)
    {
        // Each action must have exactly one of SetValue or SendMessage
        if ((action.SetValue == null && action.SendMessage == null) ||
            (action.SetValue != null && action.SendMessage != null))
        {
            throw new System.ArgumentException($"Rule '{ruleName}' has an invalid action: must have exactly one of set_value or send_message");
        }

        if (action.SetValue != null)
        {
            ValidateSetValueAction(action.SetValue, ruleName);
        }
        else // action.SendMessage must be non-null here
        {
            ValidateSendMessageAction(action.SendMessage!, ruleName);
        }
    }

    private void ValidateSetValueAction(Pulsar.RuleDefinition.Models.Actions.SetValueAction action, string ruleName)
    {
        if (string.IsNullOrWhiteSpace(action.Key))
        {
            throw new System.ArgumentException($"Rule '{ruleName}' has set_value action with missing key");
        }

        if (!_config.ValidSensors.Contains(action.Key))
        {
            throw new System.ArgumentException(
                $"Rule '{ruleName}' tries to set invalid sensor: {action.Key}"
            );
        }

        if (action.Value == null && string.IsNullOrWhiteSpace(action.ValueExpression))
        {
            throw new System.ArgumentException(
                $"Rule '{ruleName}' has set_value action with neither value nor value_expression"
            );
        }

        if (action.Value != null && !string.IsNullOrWhiteSpace(action.ValueExpression))
        {
            throw new System.ArgumentException(
                $"Rule '{ruleName}' has set_value action with both value and value_expression"
            );
        }

        // TODO: Add expression validation for value_expression
    }

    private void ValidateSendMessageAction(Pulsar.RuleDefinition.Models.Actions.SendMessageAction action, string ruleName)
    {
        if (string.IsNullOrWhiteSpace(action.Channel))
        {
            throw new System.ArgumentException(
                $"Rule '{ruleName}' has send_message action with missing channel"
            );
        }

        if (string.IsNullOrWhiteSpace(action.Message))
        {
            throw new System.ArgumentException(
                $"Rule '{ruleName}' has send_message action with missing message"
            );
        }
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
