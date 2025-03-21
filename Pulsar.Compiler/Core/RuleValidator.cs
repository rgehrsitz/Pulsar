// File: Pulsar.Compiler/Core/RuleValidator.cs

using Microsoft.Extensions.Logging; // For MS logging
using Pulsar.Compiler.Models;
using Serilog; // For Serilog logging

// Added to enable AddSerilog extension

namespace Pulsar.Compiler.Core
{
    public static class RuleValidator
    {
        // Explicitly use Serilog.ILogger to avoid ambiguity
        private static readonly Serilog.ILogger _logger = LoggingConfig.GetLogger();

        public static ValidationResult Validate(RuleDefinition rule)
        {
            try
            {
                _logger.Debug("Validating rule: {RuleName}", rule.Name);

                if (string.IsNullOrEmpty(rule.Name))
                {
                    _logger.Error("Rule name is empty");
                    return new ValidationResult
                    {
                        IsValid = false,
                        Errors = new[] { "Rule name cannot be empty" },
                    };
                }

                var errors = new List<string>();

                if (string.IsNullOrEmpty(rule.Description))
                {
                    _logger.Warning("Rule {RuleName} is missing description", rule.Name);
                }

                // Validate actions
                if (rule.Actions == null || rule.Actions.Count == 0)
                {
                    _logger.Error("Rule {RuleName} has no actions", rule.Name);
                    errors.Add("Rule must have at least one action");
                }
                else
                {
                    foreach (var action in rule.Actions)
                    {
                        ValidateAction(action, rule.Name, errors);
                    }
                }

                // Validate conditions
                if (rule.Conditions != null)
                {
                    ValidateConditionGroup(rule.Conditions, rule.Name, errors);
                }

                // NEW: Validate for circular dependencies using DependencyAnalyzer
                // Create a Microsoft.Extensions.Logging logger from our Serilog logger
                var loggerFactory = LoggerFactory.Create(builder => builder.AddSerilog(_logger));
                var msLogger = loggerFactory.CreateLogger<DependencyAnalyzer>();
                var analyzer = new DependencyAnalyzer(logger: msLogger);
                var depResult = analyzer.ValidateDependencies(new List<RuleDefinition> { rule });

                _logger.Debug(
                    "Dependency analysis result for rule {RuleName}: IsValid={IsValid}, Cycles={Cycles}",
                    rule.Name,
                    depResult.IsValid,
                    string.Join(
                        " | ",
                        depResult.CircularDependencies.Select(cycle => string.Join(" -> ", cycle))
                    )
                );

                if (!depResult.IsValid)
                {
                    errors.Add(
                        "Circular dependency detected in rule: "
                            + rule.Name
                            + " ("
                            + string.Join(" -> ", depResult.CircularDependencies.First())
                            + ")"
                    );
                }

                bool isValid = errors.Count == 0;
                _logger.Debug("Rule validation completed. IsValid: {IsValid}", isValid);

                return new ValidationResult { IsValid = isValid, Errors = errors.ToArray() };
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error validating rule {RuleName}", rule.Name);
                return new ValidationResult
                {
                    IsValid = false,
                    Errors = new[] { $"Validation error: {ex.Message}" },
                };
            }
        }

        private static void ValidateAction(
            ActionDefinition action,
            string ruleName,
            List<string> errors
        )
        {
            if (action == null)
            {
                errors.Add($"Rule '{ruleName}' has a null action");
                return;
            }

            switch (action)
            {
                case SetValueAction setValue:
                    if (string.IsNullOrWhiteSpace(setValue.Key))
                    {
                        errors.Add($"Rule '{ruleName}' has a SetValue action with an empty key");
                    }
                    break;
                case SendMessageAction sendMessage:
                    if (string.IsNullOrWhiteSpace(sendMessage.Channel))
                    {
                        errors.Add(
                            $"Rule '{ruleName}' has a SendMessage action with an empty channel"
                        );
                    }
                    if (string.IsNullOrWhiteSpace(sendMessage.Message))
                    {
                        errors.Add(
                            $"Rule '{ruleName}' has a SendMessage action with an empty message"
                        );
                    }
                    break;
                default:
                    errors.Add(
                        $"Rule '{ruleName}' has an unsupported action type: {action.GetType().Name}"
                    );
                    break;
            }
        }

        private static void ValidateConditionGroup(
            ConditionGroup conditions,
            string ruleName,
            List<string> errors
        )
        {
            if (conditions.All != null)
            {
                foreach (var condition in conditions.All)
                {
                    ValidateCondition(condition, ruleName, errors);
                }
            }

            if (conditions.Any != null)
            {
                foreach (var condition in conditions.Any)
                {
                    ValidateCondition(condition, ruleName, errors);
                }
            }

            if (
                (conditions.All == null || !conditions.All.Any())
                && (conditions.Any == null || !conditions.Any.Any())
            )
            {
                errors.Add($"Rule '{ruleName}' has an empty condition group");
            }
        }

        private static void ValidateCondition(
            ConditionDefinition condition,
            string ruleName,
            List<string> errors
        )
        {
            if (condition == null)
            {
                errors.Add($"Rule '{ruleName}' has a null condition");
                return;
            }

            switch (condition)
            {
                case ComparisonCondition comparison:
                    if (string.IsNullOrWhiteSpace(comparison.Sensor))
                    {
                        errors.Add(
                            $"Rule '{ruleName}' has a comparison condition with an empty sensor"
                        );
                    }
                    break;
                case ThresholdOverTimeCondition threshold:
                    if (string.IsNullOrWhiteSpace(threshold.Sensor))
                    {
                        errors.Add(
                            $"Rule '{ruleName}' has a threshold condition with an empty sensor"
                        );
                    }
                    if (threshold.Duration <= 0)
                    {
                        errors.Add(
                            $"Rule '{ruleName}' has a threshold condition with an invalid duration"
                        );
                    }
                    break;
                case ExpressionCondition expression:
                    if (string.IsNullOrWhiteSpace(expression.Expression))
                    {
                        errors.Add(
                            $"Rule '{ruleName}' has an expression condition with an empty expression"
                        );
                    }
                    break;
                default:
                    errors.Add(
                        $"Rule '{ruleName}' has an unsupported condition type: {condition.GetType().Name}"
                    );
                    break;
            }
        }

        public static ValidationResult ValidateRules(IEnumerable<RuleDefinition> rules)
        {
            try
            {
                _logger.Debug("Validating rule dependencies");

                var errors = new List<string>();
                var outputSensors = new Dictionary<string, string>();
                var circularDependencies = new HashSet<string>();

                // Validate individual rules first
                foreach (var rule in rules)
                {
                    var result = Validate(rule);
                    if (!result.IsValid)
                    {
                        errors.AddRange(result.Errors);
                    }
                }

                // Check for duplicate output sensors
                foreach (var rule in rules)
                {
                    foreach (var action in rule.Actions.OfType<SetValueAction>())
                    {
                        if (outputSensors.TryGetValue(action.Key, out var existingRule))
                        {
                            errors.Add(
                                $"Sensor '{action.Key}' is written by multiple rules: '{existingRule}' and '{rule.Name}'"
                            );
                        }
                        outputSensors[action.Key] = rule.Name;
                    }
                }

                // Check for circular dependencies
                foreach (var rule in rules)
                {
                    CheckCircularDependencies(
                        rule,
                        rules.ToList(),
                        outputSensors,
                        new HashSet<string>(),
                        circularDependencies
                    );
                }

                if (circularDependencies.Any())
                {
                    errors.Add(
                        $"Circular dependencies detected in rules: {string.Join(", ", circularDependencies)}"
                    );
                }

                bool isValid = errors.Count == 0;
                _logger.Debug("Rule dependency validation completed. IsValid: {IsValid}", isValid);

                return new ValidationResult { IsValid = isValid, Errors = errors.ToArray() };
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error validating rule dependencies");
                return new ValidationResult
                {
                    IsValid = false,
                    Errors = new[] { $"Validation error: {ex.Message}" },
                };
            }
        }

        private static void CheckCircularDependencies(
            RuleDefinition rule,
            List<RuleDefinition> allRules,
            Dictionary<string, string> outputSensors,
            HashSet<string> visited,
            HashSet<string> circularDependencies
        )
        {
            if (visited.Contains(rule.Name))
            {
                circularDependencies.Add(rule.Name);
                return;
            }

            visited.Add(rule.Name);

            var dependencies = GetRuleDependencies(rule, outputSensors);
            foreach (var dependencyRule in allRules.Where(r => dependencies.Contains(r.Name)))
            {
                CheckCircularDependencies(
                    dependencyRule,
                    allRules,
                    outputSensors,
                    visited,
                    circularDependencies
                );
            }

            visited.Remove(rule.Name);
        }

        private static HashSet<string> GetRuleDependencies(
            RuleDefinition rule,
            Dictionary<string, string> outputSensors
        )
        {
            var dependencies = new HashSet<string>();

            if (rule.Conditions != null)
            {
                if (rule.Conditions.All != null)
                {
                    foreach (var condition in rule.Conditions.All)
                    {
                        AddConditionDependencies(condition, outputSensors, dependencies);
                    }
                }

                if (rule.Conditions.Any != null)
                {
                    foreach (var condition in rule.Conditions.Any)
                    {
                        AddConditionDependencies(condition, outputSensors, dependencies);
                    }
                }
            }

            return dependencies;
        }

        private static void AddConditionDependencies(
            ConditionDefinition condition,
            Dictionary<string, string> outputSensors,
            HashSet<string> dependencies
        )
        {
            switch (condition)
            {
                case ComparisonCondition comparison:
                    if (outputSensors.TryGetValue(comparison.Sensor, out var rule))
                        dependencies.Add(rule);
                    break;
                case ThresholdOverTimeCondition threshold:
                    if (outputSensors.TryGetValue(threshold.Sensor, out rule))
                        dependencies.Add(rule);
                    break;
                case ExpressionCondition expression:
                    foreach (var sensor in ExtractSensorsFromExpression(expression.Expression))
                    {
                        if (outputSensors.TryGetValue(sensor, out rule))
                            dependencies.Add(rule);
                    }
                    break;
            }
        }

        private static HashSet<string> ExtractSensorsFromExpression(string expression)
        {
            var sensors = new HashSet<string>();
            if (string.IsNullOrWhiteSpace(expression))
                return sensors;

            var sensorPattern = @"\b([a-zA-Z_][a-zA-Z0-9_]*)\b";
            var matches = System.Text.RegularExpressions.Regex.Matches(expression, sensorPattern);

            foreach (var match in matches)
            {
                var potentialSensor = match.ToString();
                if (!IsMathFunction(potentialSensor))
                {
                    sensors.Add(potentialSensor);
                }
            }

            return sensors;
        }

        private static bool IsMathFunction(string? functionName)
        {
            if (string.IsNullOrWhiteSpace(functionName))
                return false;

            var mathFunctions = new HashSet<string>
            {
                "Sin",
                "Cos",
                "Tan",
                "Log",
                "Exp",
                "Sqrt",
                "Abs",
            };
            return mathFunctions.Contains(functionName);
        }
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string[] Errors { get; set; } = Array.Empty<string>();
    }
}
