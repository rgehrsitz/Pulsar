using System.Collections.Generic;
using Pulsar.RuleDefinition.Models;

namespace Pulsar.RuleDefinition.Validation;

/// <summary>
/// Result of rule validation, including any errors and ordered rules if validation succeeds
/// </summary>
public class ValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public List<string> Errors { get; }
    public RuleDefinitionModel? Rule { get; }

    public ValidationResult(List<string>? errors = null, RuleDefinitionModel? rule = null)
    {
        Errors = errors ?? new List<string>();
        Rule = rule;
    }

    public void AddError(string error)
    {
        Errors.Add(error);
    }

    public void Merge(ValidationResult other)
    {
        if (other == null) return;
        Errors.AddRange(other.Errors);
    }
}
