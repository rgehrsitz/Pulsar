using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Pulsar.RuleDefinition.Validation;

public class ExpressionValidator
{
    private static readonly HashSet<string> AllowedFunctions = new()
    {
        "min",
        "max",
        "sqrt",
        "abs",
        "round",
    };

    private static readonly HashSet<string> ComparisonOperators = new()
    {
        ">",
        ">=",
        "<",
        "<=",
        "==",
        "!=",
    };

    public (bool isValid, HashSet<string> dataSources, List<string> errors) ValidateExpression(
        string expression
    )
    {
        var errors = new List<string>();
        var dataSources = new HashSet<string>();

        if (string.IsNullOrWhiteSpace(expression))
        {
            errors.Add("Expression cannot be empty");
            return (false, dataSources, errors);
        }

        // Extract and validate functions first
        var functionMatches = Regex.Matches(expression, @"(\w+)\s*\((.*?)\)");
        var modifiedExpression = expression;

        foreach (Match match in functionMatches)
        {
            var functionName = match.Groups[1].Value.ToLower();
            var arguments = match.Groups[2].Value.Split(',').Select(arg => arg.Trim()).ToList();

            if (!AllowedFunctions.Contains(functionName))
            {
                errors.Add($"Function '{functionName}' is not allowed");
                continue;
            }

            if (arguments.Count == 0 || arguments.All(string.IsNullOrWhiteSpace))
            {
                errors.Add($"Function '{functionName}' requires arguments");
                continue;
            }

            // Extract data sources from function arguments
            foreach (var arg in arguments.Where(a => !string.IsNullOrWhiteSpace(a)))
            {
                if (!double.TryParse(arg, out _))
                {
                    dataSources.Add(arg);
                }
            }

            // Replace function call with placeholder
            modifiedExpression = modifiedExpression.Replace(match.Value, "X");
        }

        // Check for invalid operator sequences
        if (
            Regex.IsMatch(modifiedExpression, @"[<>=!]{2,}(?![=])")
            || // Catches >>> or <<<
            Regex.IsMatch(modifiedExpression, @"[><]\s+[><]")
        ) // Catches > > or < <
        {
            errors.Add("Invalid operator sequence");
            return (false, dataSources, errors);
        }

        // Check for invalid start/end
        if (modifiedExpression.StartsWith("*") || modifiedExpression.StartsWith("/"))
        {
            errors.Add("Expression cannot start with * or /");
            return (false, dataSources, errors);
        }

        if (Regex.IsMatch(modifiedExpression, @"[<>=!+\-*/]\s*$"))
        {
            errors.Add("Expression cannot end with an operator");
            return (false, dataSources, errors);
        }

        // Extract remaining data sources (non-function arguments)
        var remainingDataSources = Regex
            .Matches(modifiedExpression, @"[a-zA-Z_]\w*")
            .Select(m => m.Value)
            .Where(v => !AllowedFunctions.Contains(v.ToLower()) && v != "X");

        dataSources.UnionWith(remainingDataSources);

        // Only require comparison operator for non-function expressions
        if (functionMatches.Count == 0)
        {
            var hasComparisonOperator = ComparisonOperators.Any(op =>
                modifiedExpression.Contains(op)
            );
            if (!hasComparisonOperator)
            {
                errors.Add("Expression must contain a comparison operator");
            }
        }

        return (!errors.Any(), dataSources, errors);
    }
}
