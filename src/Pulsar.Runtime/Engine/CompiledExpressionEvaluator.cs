using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging;
using Pulsar.RuleDefinition.Models;
using Pulsar.RuleDefinition.Models.Conditions;

namespace Pulsar.Runtime.Engine;

/// <summary>
/// Evaluates expression-based conditions using compiled C# code
/// </summary>
public class CompiledExpressionEvaluator : IConditionEvaluator
{
    private readonly ConcurrentDictionary<
        string,
        Func<IDictionary<string, double>, bool>
    > _compiledExpressions = new();
    private readonly ILogger _logger;

    public CompiledExpressionEvaluator(ILogger logger)
    {
        _logger = logger;
    }

    public async Task<bool> EvaluateAsync(
        ConditionWrapperDefinition condition,
        IDictionary<string, double> data
    )
    {
        if (condition.Expression is not ExpressionConditionDefinition expressionCondition)
        {
            throw new ArgumentException(
                $"Expected ExpressionConditionDefinition but got {condition.Expression?.GetType().Name ?? "null"}"
            );
        }

        var expression = expressionCondition.Expression;
        var evaluator =
            _compiledExpressions.GetOrAdd(
                expression,
                expr =>
                {
                    try
                    {
                        return CompileExpression(expr);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to compile expression: {Expression}", expr);
                        throw;
                    }
                }
            );

        try
        {
            return evaluator(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to evaluate expression: {Expression} with data: {Data}",
                expression,
                string.Join(", ", data.Select(kvp => $"{kvp.Key}={kvp.Value}"))
            );
            throw;
        }
    }
}
