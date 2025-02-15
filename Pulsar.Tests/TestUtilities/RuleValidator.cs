using System;
using System.Collections.Generic;
using Pulsar.Compiler.Models;
using Pulsar.Compiler;
using Serilog;

namespace Pulsar.Tests.TestUtilities
{
    public static class RuleValidator
    {
        private static readonly ILogger _logger = LoggingConfig.GetLogger();

        public static ValidationResult Validate(RuleDefinition rule)
        {
            try
            {
                _logger.Debug("Validating rule: {RuleName}", rule.Name);

                if (string.IsNullOrEmpty(rule.Name))
                {
                    _logger.Error("Rule name is empty");
                    return new ValidationResult { IsValid = false, Errors = new[] { "Rule name cannot be empty" } };
                }

                var errors = new List<string>();

                if (string.IsNullOrEmpty(rule.Description))
                {
                    _logger.Warning("Rule {RuleName} is missing description", rule.Name);
                }

                if (rule.Conditions == null)
                {
                    _logger.Error("Rule {RuleName} has no conditions", rule.Name);
                    errors.Add("Rule must have at least one condition");
                }

                if (rule.Actions == null || rule.Actions.Count == 0)
                {
                    _logger.Error("Rule {RuleName} has no actions", rule.Name);
                    errors.Add("Rule must have at least one action");
                }

                bool isValid = errors.Count == 0;
                _logger.Debug("Rule validation completed. IsValid: {IsValid}", isValid);

                return new ValidationResult
                {
                    IsValid = isValid,
                    Errors = errors.ToArray()
                };
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error validating rule {RuleName}", rule.Name);
                return new ValidationResult
                {
                    IsValid = false,
                    Errors = new[] { $"Validation error: {ex.Message}" }
                };
            }
        }
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string[] Errors { get; set; } = Array.Empty<string>();
    }
}