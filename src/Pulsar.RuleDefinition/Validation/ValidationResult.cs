using System.Collections.Generic;
using Pulsar.RuleDefinition.Models;

namespace Pulsar.RuleDefinition.Validation;

/// <summary>
/// Result of rule validation, including any errors and ordered rules if validation succeeds
/// </summary>
public class ValidationResult
{
    public List<ValidationError> Errors { get; set; } = new();
    public List<Rule>? OrderedRules { get; set; }
    public bool IsValid => !Errors.Any();

    public ValidationResult()
    {
        Errors = new List<ValidationError>();
    }

    public ValidationResult(IEnumerable<ValidationError> errors)
    {
        Errors = new List<ValidationError>(errors);
    }
}
